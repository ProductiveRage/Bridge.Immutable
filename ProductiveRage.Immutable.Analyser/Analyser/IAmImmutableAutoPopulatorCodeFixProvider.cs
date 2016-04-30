using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProductiveRage.Immutable.Analyser
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IAmImmutableAutoPopulatorCodeFixProvider)), Shared]
	public sealed class IAmImmutableAutoPopulatorCodeFixProvider : CodeFixProvider
	{
		private const string title = "Populate class from constructor";

		public sealed override ImmutableArray<string> FixableDiagnosticIds
		{
			get { return ImmutableArray.Create(IAmImmutableAutoPopulatorAnalyzer.DiagnosticId); }
		}

		public sealed override FixAllProvider GetFixAllProvider()
		{
			// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
			return WellKnownFixAllProviders.BatchFixer;
		}

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			// Find the constructor (and its parent class) identified by the diagnostic..
			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var constructorDeclaration = root.FindToken(diagnosticSpan.Start).Parent as ConstructorDeclarationSyntax;
			if (constructorDeclaration == null)
				return;

			// Register a code action that will invoke the fix
			context.RegisterCodeFix(
				CodeAction.Create(
					title: title,
					createChangedDocument: cancellationToken => PopulateConstructor(context.Document, constructorDeclaration, cancellationToken),
					equivalenceKey: title
				),
				diagnostic
			);
		}

		private async Task<Document> PopulateConstructor(Document document, ConstructorDeclarationSyntax constructorDeclaration, CancellationToken cancellationToken)
		{
			// Get all of the arguments of that constructor that are not passed to a base constructor
			var constructorArguments = IAmImmutableAutoPopulatorAnalyzer.GetConstructorArgumentsThatAreNotPassedToBaseConstructor(constructorDeclaration);

			// Add the CtorSet calls to the constructor
			var populatedConstructor = constructorDeclaration.WithBody(
				constructorDeclaration.Body.AddStatements(
						constructorArguments
						.Select(constructorArgument => GeneratorCtorSetCall(
							GetPropertyName(constructorArgument.Identifier.Text),
							constructorArgument.Identifier.Text
						))
						.ToArray()
				)
			);

			// Add properties to the class that correspond to the constructor arguments (ignoring any properties that are already declared - there aren't
			// expected to be any because the quickest way now to generate an IAmImmutable implementation is to use this code fix on a constructor, to
			// populate the class with minimum manual labour, but if some of the properties have already been added for whatever reason then that's
			// fine, they will be ignored.
			var classDeclaration = constructorDeclaration.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();
			var namesOfPropertiesDefinedOnClass = classDeclaration.ChildNodes()
				.OfType<PropertyDeclarationSyntax>()
				.Where(property => property.ExplicitInterfaceSpecifier == null) // Don't consider explicitly-implemented interface properties
				.Select(property => property.Identifier.Text)
				.ToArray();
			var propertiesToAdd = constructorArguments
				.Select(constructorArgument => new { Argument = constructorArgument, PropertyName = GetPropertyName(constructorArgument.Identifier.Text) })
				.Where(argumentDetails => !namesOfPropertiesDefinedOnClass.Contains(argumentDetails.PropertyName))
				.Select(argumentDetails =>
					SyntaxFactory.PropertyDeclaration(
						argumentDetails.Argument.Type,
						argumentDetails.PropertyName
					)
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddAccessorListAccessors(
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
						SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
					)
				);

			// Return a new document that replaces the current class definition with the auto-populated one
			// - If there is no "using ProductiveRage.Immutable" statement then insert one of them, otherwise the this.CtorSet calls will fail as those
			//   are calls to extension methods in the "ProductiveRage.Immutable" namespace
			var populatedClass = classDeclaration
				.ReplaceNode(constructorDeclaration, populatedConstructor)
				.AddMembers(propertiesToAdd.ToArray());
			var root = await document
				.GetSyntaxRootAsync(cancellationToken)
				.ConfigureAwait(false);
			root = root.ReplaceNode(classDeclaration, populatedClass);
			var usingDirectives = ((CompilationUnitSyntax)root).Usings;
			if (!usingDirectives.Any(usingDirective => (usingDirective.Alias == null) && (usingDirective.Name.ToFullString() == CommonAnalyser.AnalyserAssemblyName)))
			{
				root = ((CompilationUnitSyntax)root).WithUsings( // Courtesy of http://stackoverflow.com/a/17677024
					usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(CommonAnalyser.AnalyserAssemblyName)))
				);
			}
			return document.WithSyntaxRoot(root);
		}

		private static string GetPropertyName(string constructorArgumentName)
		{
			if (string.IsNullOrWhiteSpace(constructorArgumentName))
				throw new ArgumentException($"Null/blank {nameof(constructorArgumentName)} specified");

			return constructorArgumentName.Substring(0, 1).ToUpper() + constructorArgumentName.Substring(1);
		}

		private static ExpressionStatementSyntax GeneratorCtorSetCall(string propertyName, string constructorArgumentName)
		{
			if (string.IsNullOrWhiteSpace(propertyName))
				throw new ArgumentException($"Null/blank {nameof(propertyName)} specified");
			if (string.IsNullOrWhiteSpace(constructorArgumentName))
				throw new ArgumentException($"Null/blank {nameof(constructorArgumentName)} specified");

			return SyntaxFactory.ExpressionStatement(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.ThisExpression(),
						SyntaxFactory.IdentifierName("CtorSet")
					),
					SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] {
					SyntaxFactory.Argument(
						SyntaxFactory.SimpleLambdaExpression(
							SyntaxFactory.Parameter(SyntaxFactory.Identifier("_")),
							SyntaxFactory.MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								SyntaxFactory.IdentifierName("_"),
								SyntaxFactory.IdentifierName(propertyName)
							)
						)
					),
					SyntaxFactory.Argument(
						SyntaxFactory.IdentifierName(constructorArgumentName)
					)
					}))
				)
			);
		}
	}
}
