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
		public const string Category = "Design";
		public static DiagnosticDescriptor MustHaveSettersOnPropertiesWithGettersAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			"IAmImmutable: Must have a setter on a property if it has a getter", // TODO GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			"Property '{0}' must have a setter since it has a getter and is on a class that implements IAmImmutable", // TODO GetLocalizableString(nameof(Resources.IAmImmutablePropertiesMustHaveSettersMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			"IAmImmutable: May not have Bridge attributes on properties with getters", // TODO GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			"IAmImmutable: May not have Bridge attributes on properties with getters ({0})", // TODO GetLocalizableString(nameof(Resources.IAmImmutablePropertiesMustNotHaveBridgeAttributesMessageFormat)),
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

			foreach (var property in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>())
			{
				var getterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
				var setterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
				if ((getterIfDefined != null) && (setterIfDefined == null))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MustHaveSettersOnPropertiesWithGettersAccessRule,
						property.GetLocation(),
						property.Identifier.Text
					));
					continue;
				}
				if ((getterIfDefined != null) && CommonAnalyser.HasDisallowedAttribute(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(context.SemanticModel, getterIfDefined)))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule,
						getterIfDefined.GetLocation(),
						property.Identifier.Text
					));
				}
				if ((setterIfDefined != null) && CommonAnalyser.HasDisallowedAttribute(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(context.SemanticModel, setterIfDefined)))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule,
						setterIfDefined.GetLocation(),
						property.Identifier.Text
					));
				}
			}
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
