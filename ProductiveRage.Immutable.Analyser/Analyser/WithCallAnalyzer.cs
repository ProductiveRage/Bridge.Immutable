using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class WithCallAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "With";
		public const string Category = "Design";
		public static DiagnosticDescriptor SimplePropertyAccessorArgumentAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.WithAnalyserTitle)),
			GetLocalizableString(nameof(Resources.WithSimplePropertyAccessorArgumentMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor IndirectTargetAccessorAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.WithAnalyserTitle)),
			GetLocalizableString(nameof(Resources.WithDirectPropertyTargetAccessorArgumentMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor BridgeAttributeAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.WithAnalyserTitle)),
			GetLocalizableString(nameof(Resources.WithBridgeAttributeMessageFormat)),
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
					BridgeAttributeAccessRule
				);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForIllegalWithCall, SyntaxKind.InvocationExpression);
		}

		private void LookForIllegalWithCall(SyntaxNodeAnalysisContext context)
		{
			var invocation = context.Node as InvocationExpressionSyntax;
			if (invocation == null)
				return;

			var lastExpressionToken = invocation.Expression.GetLastToken();
			if ((lastExpressionToken == null) || (lastExpressionToken.Text != "With"))
				return;

			var withMethod = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if ((withMethod == null)
			|| (withMethod.ContainingAssembly == null)
			|| (withMethod.ContainingAssembly.Name != CommonAnalyser.AnalyserAssemblyName))
				return;

			// The GetSymbolInfo call above does some magic so that when the With method is called as extension then it its parameters list
			// excludes the "this" parameter but when it's NOT called as an extension method then it DOES have the "this" parameter in the
			// list. So the signature
			//
			//   T With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier, TPropertyValue value)
			//
			// may be identified as the "withMethod" reference above and be described as having three arguments if it's called as
			//
			//   ImmutabilityHelpers.With(x, _ => _.Id, 123)
			//
			// but described as only have two arguments if it's called as
			//
			//   x.With(_ => _.Id, 123)
			// 
			// This means that we need to look at the withMethod's Parameters set to work out which argument in the current expression's
			// argument list is the property identifier / property retriever that we're interested in validating
			var indexOfPropertyIdentifierArgument = withMethod.Parameters
				.Select((p, i) => new { Index = i, Parameter = p })
				.Where(p => p.Parameter.Name  == "propertyIdentifier")
				.Single()
				.Index;

			// Confirm that the propertyRetriever is a simple lambda (eg. "_ => _.Id")
			var propertyRetrieverArgument = invocation.ArgumentList.Arguments[indexOfPropertyIdentifierArgument];
			switch (CommonAnalyser.GetPropertyRetrieverArgumentStatus(propertyRetrieverArgument, context))
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
				case CommonAnalyser.PropertyValidationResult.MissingSetter:
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
			}
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
