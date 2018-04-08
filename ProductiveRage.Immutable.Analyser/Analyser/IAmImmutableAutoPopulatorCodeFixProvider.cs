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
			// If there's a Validate method that should be called at the end of the constructor then ensure that it's invoked at the end of the auto-populated
			// constructor (the Validate method - if there is one that meets the requirements of being a method with zero arguments) is automatically called after
			// any With call but needs to be explicitly called from the constructor. Note: It would only make sense for the method to be an instance method (since
			// it can't validate the state of an instance if it's a static method) but the JavaScript doesn't (can't) check this and so, for consistency, we should
			// not restrict ourselves to only instance methods here.
			var classDeclaration = constructorDeclaration.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();
			var validateMethodIfDefined = IAmImmutableAnalyzer.TryToGetValidateMethodThatThisClassMustCall(classDeclaration);

			// Add the CtorSet calls to the constructor
			var constructorArgumentNamesThatAppearToBeUsed = constructorDeclaration.Body.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(i => !(i.Parent is MemberAccessExpressionSyntax)) // Ignore member access (eg. the "_ => _.Id" in "this.CtorSet(_ => _.Id, id)")
				.Select(i => i.Identifier.Text);
			var newConstructorBody = constructorDeclaration.Body.AddStatements(
				IAmImmutableAutoPopulatorAnalyzer.GetConstructorArgumentNamesThatAreNotAccountedFor(constructorDeclaration)
					.Select(argument => GeneratorCtorSetCall(GetPropertyName(argument.Identifier.Text), argument.Identifier.Text))
					.ToArray()
			);
			if (validateMethodIfDefined != null)
			{
				var existingValidateCalls = newConstructorBody.Statements
					.OfType<ExpressionStatementSyntax>()
					.Where(expression =>
					{
						var invocationExpression = expression.Expression as InvocationExpressionSyntax;
						if (invocationExpression == null)
							return false;
						var identifier = invocationExpression.Expression as IdentifierNameSyntax;
						if (identifier == null)
							return false;
						return identifier.Identifier.Text == validateMethodIfDefined.Identifier.Text;
					});
				if (existingValidateCalls.Any())
				{
					var updatedStatements = newConstructorBody.Statements;
					foreach (var existingValidateCall in existingValidateCalls)
						updatedStatements = updatedStatements.Remove(existingValidateCall);
					updatedStatements = updatedStatements.AddRange(existingValidateCalls);
					newConstructorBody = newConstructorBody.WithStatements(updatedStatements);
				}
				else
					newConstructorBody = newConstructorBody.AddStatements(GetValidateCall(validateMethodIfDefined.Identifier.Text));
			}
			var populatedConstructor = constructorDeclaration.WithBody(newConstructorBody);

			// Add properties to the class that correspond to the constructor arguments (ignoring any properties that are already declared - there aren't
			// expected to be any because the quickest way now to generate an IAmImmutable implementation is to use this code fix on a constructor, to
			// populate the class with minimum manual labour, but if some of the properties have already been added for whatever reason then that's
			// fine, they will be ignored.
			var namesOfPropertiesDefinedOnClass = classDeclaration.ChildNodes()
				.OfType<PropertyDeclarationSyntax>()
				.Where(property => property.ExplicitInterfaceSpecifier == null) // Don't consider explicitly-implemented interface properties
				.Select(property => property.Identifier.Text)
				.ToArray();
			var propertiesToAdd = IAmImmutableAutoPopulatorAnalyzer.GetConstructorArgumentsThatAreNotPassedToBaseConstructor(constructorDeclaration)
				.Select(constructorArgument => new { Argument = constructorArgument, PropertyName = GetPropertyName(constructorArgument.Identifier.Text) })
				.Where(argumentDetails => !namesOfPropertiesDefinedOnClass.Contains(argumentDetails.PropertyName))
				.Select(argumentDetails =>
					SyntaxFactory.PropertyDeclaration(
						argumentDetails.Argument.Type,
						argumentDetails.PropertyName
					)
					.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
					.AddAccessorListAccessors(
						// 2016-09-21 DWR: Used to emit property getters of the form "{ get; private set; }" here since Bridge didn't support C# 6 syntax..
						// but now it does (since 15.0) and so we can go for the stricter and more succinct "{ get; }" readonly auto-property format
						SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
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

		private static ExpressionStatementSyntax GetValidateCall(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException($"Null/blank {nameof(name)} specified");

			return SyntaxFactory.ExpressionStatement(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.IdentifierName(name),
					SyntaxFactory.ArgumentList()
				)
			);
		}
	}
}
