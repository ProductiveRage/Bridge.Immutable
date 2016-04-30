using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class IAmImmutableAutoPopulatorAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "IAmImmutableAutoPopulator";
		private const string Category = "Design";
		public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserMessageFormat)),
			Category,
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForEmptyConstructorsThatHaveArgumentsOnIAmImmutableImplementations, SyntaxKind.ClassDeclaration);
		}

		private void LookForEmptyConstructorsThatHaveArgumentsOnIAmImmutableImplementations(SyntaxNodeAnalysisContext context)
		{
			var classDeclaration = context.Node as ClassDeclarationSyntax;
			if (classDeclaration == null)
				return;

			// If it cheaper to look at symbols in the current file than to have to look elsewhere. So, firstly, just check whether the constructor
			// looks like it may or may not be applicable - if there are no constructor arguments (that aren't passed to a base constructor) or if
			// the constructor is already populated then do nothing. If there ARE constructor arguments that are not accounted for and the constructor
			// body is empty, then we need to do more analysis.
			foreach (var constructor in classDeclaration.ChildNodes().OfType<ConstructorDeclarationSyntax>())
			{
				if (constructor.Body == null) // This implies incomplete content - there's no point trying to analyse it until it compiles
					continue;
				if (constructor.Body.ChildNodes().Any())
					continue;
				if (!GetConstructorArgumentsThatAreNotPassedToBaseConstructor(constructor).Any())
					continue;

				// If the class doesn't implement IAmImmutable then we don't need to consider this constructor or any other constructor on it. It may
				// require looking at other files (if this class derives from another class, which implements IAmImmutable), though, so it makes sense
				// to only do this check if the constructor otherwise looks promising.
				if (!CommonAnalyser.ImplementsIAmImmutable(context.SemanticModel.GetDeclaredSymbol(classDeclaration)))
					return;

				context.ReportDiagnostic(Diagnostic.Create(
					Rule,
					constructor.GetLocation(),
					classDeclaration.Identifier.Text
				));
			}
		}

		public static IEnumerable<ParameterSyntax> GetConstructorArgumentsThatAreNotPassedToBaseConstructor(ConstructorDeclarationSyntax constructor)
		{
			if (constructor == null)
				throw new ArgumentNullException(nameof(constructor));

			var constructorArguments = constructor.ParameterList.Parameters.AsEnumerable();
			if (!constructorArguments.Any())
				return constructorArguments;

			// Ignore parameters that are passed to a base constructor
			if (constructor.Initializer != null)
			{
				var argumentsUsedInBaseConstructor = constructor.Initializer.ArgumentList.Arguments
					.Where(baseConstructorArgument => baseConstructorArgument.Expression is IdentifierNameSyntax)
					.Select(baseConstructorArgument => ((IdentifierNameSyntax)baseConstructorArgument.Expression).Identifier.Text)
					.ToArray();
				constructorArguments = constructorArguments
					.Where(constructorArgument => !argumentsUsedInBaseConstructor.Contains(constructorArgument.Identifier.Text));
			}
			return constructorArguments;
		}
		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
