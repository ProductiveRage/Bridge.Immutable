using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CtorSetCallAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "CtorSet";
		public const string Category = "Design";
		public static DiagnosticDescriptor SimpleMemberAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.CtorSimpleMemberAccessRuleMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor ConstructorRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.CtorMayOnlyBeCalledWithConstructorMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor SimplePropertyAccessorArgumentAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.CtorSimplePropertyAccessorArgumentMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor BridgeAttributeAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.CtorBridgeAttributeMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(
					SimpleMemberAccessRule,
					ConstructorRule,
					SimplePropertyAccessorArgumentAccessRule,
					BridgeAttributeAccessRule
				);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(LookForIllegalCtorSetCall, SyntaxKind.InvocationExpression);
		}

		private void LookForIllegalCtorSetCall(SyntaxNodeAnalysisContext context)
		{
			var invocation = context.Node as InvocationExpressionSyntax;
			if (invocation == null)
				return;

			var lastExpressionToken = invocation.Expression.GetLastToken();
			if ((lastExpressionToken == null) || (lastExpressionToken.Text != "CtorSet"))
				return;

			var ctorSetMethod = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if ((ctorSetMethod == null)
			|| (ctorSetMethod.ContainingAssembly == null)
			|| (ctorSetMethod.ContainingAssembly.Name != CommonAnalyser.AnalyserAssemblyName))
				return;

			// A SimpleMemberAccessExpression is a VERY simple "dot access" such as "this.CtorSet(..)"
			// - Anything more complicated is not what is recommended
			// - Anything that IS this simple but that does not target "this" is not what is recommended
			if ((invocation.Expression.Kind() != SyntaxKind.SimpleMemberAccessExpression)
			|| (invocation.Expression.GetFirstToken().Kind() != SyntaxKind.ThisKeyword))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SimpleMemberAccessRule,
					context.Node.GetLocation()
				));
				return;
			}

			// Ensure that the CtorSet call is within a constructor (that's the only place that properties should be set on immutable types)
			var isInsideConstructor = false;
			var ancestor = invocation.Parent;
			while (ancestor != null)
			{
				if (ancestor.Kind() == SyntaxKind.ConstructorDeclaration)
				{
					isInsideConstructor = true;
					break;
				}
				ancestor = ancestor.Parent;
			}
			if (!isInsideConstructor)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					ConstructorRule,
					context.Node.GetLocation()
				));
				return;
			}

			var propertyRetrieverArgument = invocation.ArgumentList.Arguments.FirstOrDefault();
			if (propertyRetrieverArgument == null)
			{
				// If there are no arguments then there should be a compile error and we shouldn't have got here - but better to pretend that
				// all is well until we DO get valid content, rather than cause an NRE below
				return;
			}

			switch (CommonAnalyser.GetPropertyRetrieverArgumentStatus(propertyRetrieverArgument, context))
			{
				case CommonAnalyser.PropertyValidationResult.Ok:
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
