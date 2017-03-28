using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class GetPropertyCallAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "GetProperty";
		public const string Category = "Design";
		public static DiagnosticDescriptor SimplePropertyAccessorArgumentAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.GetPropertyAnalyserTitle)),
			GetLocalizableString(nameof(Resources.GetPropertySimplePropertyAccessorArgumentMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor IndirectTargetAccessorAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.GetPropertyAnalyserTitle)),
			GetLocalizableString(nameof(Resources.GetPropertyDirectPropertyTargetAccessorArgumentMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor BridgeAttributeAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.GetPropertyAnalyserTitle)),
			GetLocalizableString(nameof(Resources.GetPropertyBridgeAttributeMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.GetPropertyAnalyserTitle)),
			GetLocalizableString(nameof(Resources.TPropertyValueNotSpecificEnough)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(
					SimplePropertyAccessorArgumentAccessRule,
					IndirectTargetAccessorAccessRule,
					BridgeAttributeAccessRule,
					PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule
				);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForIllegalGetPropertyCall, SyntaxKind.InvocationExpression);
		}

		private void LookForIllegalGetPropertyCall(SyntaxNodeAnalysisContext context)
		{
			var invocation = context.Node as InvocationExpressionSyntax;
			if (invocation == null)
				return;

			if ((invocation.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text != "GetProperty")
				return;

			var getPropertyMethod = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if ((getPropertyMethod == null)
			|| (getPropertyMethod.ContainingAssembly == null)
			|| (getPropertyMethod.ContainingAssembly.Name != CommonAnalyser.AnalyserAssemblyName))
				return;

			// The GetSymbolInfo call above does some magic so that when the GetProperty method is called as extension then it its parameters
			// list excludes the "this" parameter. See the WithCallAnalyzer for more details about this, the short version is that we need to
			// look at the getPropertyMethod's Parameters set to work out which argument in the current expression's argument list is the
			// property identifier / property retriever that we're interested in validating.
			var indexOfPropertyIdentifierArgument = getPropertyMethod.Parameters
				.Select((p, i) => new { Index = i, Parameter = p })
				.Where(p => p.Parameter.Name  == "propertyIdentifier")
				.Single()
				.Index;

			// See notes in WithCallAnalyzer and CtorSetCallAnalyzer about why it's important that we don't allow down casting of the property
			// type (if a "Name" property is of type string then don't allow the TPropertyValue type argument to be inferred as anything less
			// specific, such as object).
			var typeArguments = getPropertyMethod.TypeParameters.Zip(getPropertyMethod.TypeArguments, (genericTypeParam, type) => new { Name = genericTypeParam.Name, Type = type });
			var propertyValueTypeIfKnown = typeArguments.FirstOrDefault(t => t.Name == "TPropertyValue")?.Type;

			// Confirm that the propertyRetriever is a simple lambda (eg. "_ => _.Id")
			var propertyRetrieverArgument = invocation.ArgumentList.Arguments[indexOfPropertyIdentifierArgument];
			switch (CommonAnalyser.GetPropertyRetrieverArgumentStatus(propertyRetrieverArgument, context, propertyValueTypeIfKnown))
			{
				case CommonAnalyser.PropertyValidationResult.Ok:
				case CommonAnalyser.PropertyValidationResult.UnableToConfirmOrDeny:
					return;

				case CommonAnalyser.PropertyValidationResult.IndirectTargetAccess:
					context.ReportDiagnostic(Diagnostic.Create(
						IndirectTargetAccessorAccessRule,
						propertyRetrieverArgument.GetLocation()
					));
					return;

				case CommonAnalyser.PropertyValidationResult.NotSimpleLambdaExpression:
				case CommonAnalyser.PropertyValidationResult.LambdaDoesNotTargetProperty:
					context.ReportDiagnostic(Diagnostic.Create(
						SimplePropertyAccessorArgumentAccessRule,
						propertyRetrieverArgument.GetLocation()
					));
					return;

				case CommonAnalyser.PropertyValidationResult.MissingGetter:
					context.ReportDiagnostic(Diagnostic.Create(
						SimplePropertyAccessorArgumentAccessRule,
						propertyRetrieverArgument.GetLocation()
					));
					return;

				case CommonAnalyser.PropertyValidationResult.GetterHasBridgeAttributes:
				case CommonAnalyser.PropertyValidationResult.SetterHasBridgeAttributes:
					context.ReportDiagnostic(Diagnostic.Create(
						BridgeAttributeAccessRule,
						propertyRetrieverArgument.GetLocation()
					));
					return;

				case CommonAnalyser.PropertyValidationResult.PropertyIsOfMoreSpecificTypeThanSpecificValueType:
					context.ReportDiagnostic(Diagnostic.Create(
						PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule,
						invocation.GetLocation()
					));
					return;
			}
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
