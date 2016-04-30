using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class IAmImmutableAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "IAmImmutable";
		private const string Category = "Design";
		public static DiagnosticDescriptor MustHaveSettersOnPropertiesWithGettersAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutablePropertiesMustHaveSettersMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutablePropertiesMustNotHaveBridgeAttributesMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(
					MustHaveSettersOnPropertiesWithGettersAccessRule,
					MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule
				);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForIllegalIAmImmutableImplementations, SyntaxKind.ClassDeclaration);
		}

		private void LookForIllegalIAmImmutableImplementations(SyntaxNodeAnalysisContext context)
		{
			var classDeclaration = context.Node as ClassDeclarationSyntax;
			if (classDeclaration == null)
				return;

			// This is likely to be the most expensive work (since it requires lookup of other symbols elsewhere in the solution, whereas the
			// logic below only look at code in the current file) so only perform it when required (leave it as null until we absolutely need
			// to know whether the current class implements IAmImmutable or not)
			bool? classImplementIAmImmutable = null;
			foreach (var property in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>())
			{
				if (property.ExplicitInterfaceSpecifier != null)
				{
					// Since CtorSet and With can not target properties that are not directly accessible through a reference to the
					// IAmImmutable-implementing type (because "_ => _.Name" is acceptable as a property retriever but not something
					// like "_ => ((IWhatever)_).Name") if a property is explicitly implemented for a base interface then the rules
					// below need not be applied to it.
					continue;
				}

				Diagnostic errorIfAny;
				var getterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
				var setterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
				if ((getterIfDefined != null) && (setterIfDefined == null))
				{
					errorIfAny = Diagnostic.Create(
						MustHaveSettersOnPropertiesWithGettersAccessRule,
						property.GetLocation(),
						property.Identifier.Text
					);
				}
				else if ((getterIfDefined != null) && CommonAnalyser.HasDisallowedAttribute(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(context.SemanticModel, getterIfDefined)))
				{
					errorIfAny = Diagnostic.Create(
						MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule,
						getterIfDefined.GetLocation(),
						property.Identifier.Text
					);
				}
				else if ((setterIfDefined != null) && CommonAnalyser.HasDisallowedAttribute(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(context.SemanticModel, setterIfDefined)))
				{
					errorIfAny = Diagnostic.Create(
						MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule,
						setterIfDefined.GetLocation(),
						property.Identifier.Text
					);
				}
				else
					continue;
				
				// Enountered a potential error if the current class implements IAmImmutable - so find out whether it does or not (if it
				// doesn't then no further work is required and we can exit the entire process early)
				if (classImplementIAmImmutable == null)
					classImplementIAmImmutable = CommonAnalyser.ImplementsIAmImmutable(context.SemanticModel.GetDeclaredSymbol(classDeclaration));
				if (!classImplementIAmImmutable.Value)
					return;
				context.ReportDiagnostic(errorIfAny);
			}
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
