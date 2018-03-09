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
		public static DiagnosticDescriptor EmptyConstructorRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserEmptyConstructorMessageFormat)),
			Category,
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor OutOfSyncConstructorRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutableAutoPopulatorAnalyserOutOfSyncConstructorMessageFormat)),
			Category,
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(EmptyConstructorRule); } }

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

				var constructorArgumentsToCheckFor = GetConstructorArgumentsThatAreNotPassedToBaseConstructor(constructor);
				if (!constructorArgumentsToCheckFor.Any())
					continue;

				// 2018-03-09 DWR: Previously, this analyser/codefix only looked for empty constructors (the idea being that you would write just an
				// empty constructor and its arguments would be used to populate the rest of the class) but now there is support for adding a new
				// argument to an existing class and having the codefix fill in whatever is missing - we need to detect the two different scenarios
				// and raise a rule that is appropriate to whichever has occured (if either)
				Diagnostic diagnosticToRaise;
				if (!constructor.Body.ChildNodes().Any())
					diagnosticToRaise = Diagnostic.Create(EmptyConstructorRule, constructor.GetLocation(), classDeclaration.Identifier.Text);
				else if (GetConstructorArgumentNamesThatAreNotAccountedFor(constructor).Any())
					diagnosticToRaise = Diagnostic.Create(OutOfSyncConstructorRule, constructor.GetLocation(), classDeclaration.Identifier.Text);
				else
					continue;

				// If the class doesn't implement IAmImmutable then we don't need to consider this constructor or any other constructor on it. It may
				// require looking at other files (if this class derives from another class, which implements IAmImmutable), though, so it makes sense
				// to only do this check if the constructor otherwise looks promising.
				if (!CommonAnalyser.ImplementsIAmImmutable(context.SemanticModel.GetDeclaredSymbol(classDeclaration)))
					return;

				context.ReportDiagnostic(diagnosticToRaise);
			}
		}

		public static IEnumerable<ParameterSyntax> GetConstructorArgumentsThatAreNotPassedToBaseConstructor(ConstructorDeclarationSyntax constructor)
		{
			if (constructor == null)
				throw new ArgumentNullException(nameof(constructor));

			var constructorArguments = constructor.ParameterList.Parameters
				.AsEnumerable()
				.Where(constructorArgument => !string.IsNullOrWhiteSpace(constructorArgument.Identifier.Text));
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

		public static IEnumerable<ParameterSyntax> GetConstructorArgumentNamesThatAreNotAccountedFor(ConstructorDeclarationSyntax constructor)
		{
			if (constructor == null)
				throw new ArgumentNullException(nameof(constructor));

			if (constructor.Body == null) // This implies incomplete content - there's no point trying to analyse it until it compiles
				return Enumerable.Empty<ParameterSyntax>();

			// Really, we should be using context to do symbol lookups here to ensure that identifiers that we think relate to constructor arguments really do, instead of guessing (which is
			// kind of what's happening below where we check whether the parent is a MemberAccessExpressionSyntax or not) but I think that this approach should be reliable enough and it's
			// cheaper not to have to do full symbol lookups. If it turns out that there are some edge cases encountered in the future then we can always change to do it properly
			var constructorArgumentsToCheckFor = GetConstructorArgumentsThatAreNotPassedToBaseConstructor(constructor);
			if (!constructorArgumentsToCheckFor.Any())
				return Enumerable.Empty<ParameterSyntax>();
			var constructorArgumentNamesThatAppearToBeUsed = constructor.Body.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(i => !(i.Parent is MemberAccessExpressionSyntax)) // Ignore member access (eg. the "_ => _.Id" in "this.CtorSet(_ => _.Id, id)")
				.Select(i => i.Identifier.Text);
			return constructorArgumentsToCheckFor.Where(a => !constructorArgumentNamesThatAppearToBeUsed.Contains(a.Identifier.Text));
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
