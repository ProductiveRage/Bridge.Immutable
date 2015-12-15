using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CtorSetCallAnalyzer : DiagnosticAnalyzer
	{
		private const string BridgeAssemblyName = "Bridge";
		private const string CtorSetAssemblyName = "ProductiveRage.Immutable";

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
			|| (ctorSetMethod.ContainingAssembly.Name != CtorSetAssemblyName))
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
				// all is well until we DO get valid content, rather cause an NRE below
				return;
			}

			SimpleNameSyntax tagetNameIfSimpleLambdaExpression;
			if (propertyRetrieverArgument.Expression.Kind() != SyntaxKind.SimpleLambdaExpression)
				tagetNameIfSimpleLambdaExpression = null;
			else
			{
				var propertyRetrieverExpression = (SimpleLambdaExpressionSyntax)propertyRetrieverArgument.Expression;
				if (propertyRetrieverExpression.Body.Kind() != SyntaxKind.SimpleMemberAccessExpression)
					tagetNameIfSimpleLambdaExpression = null;
				else
					tagetNameIfSimpleLambdaExpression = ((MemberAccessExpressionSyntax)propertyRetrieverExpression.Body).Name;
			}
			if (tagetNameIfSimpleLambdaExpression == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SimplePropertyAccessorArgumentAccessRule,
					context.Node.GetLocation()
				));
				return;
			}

			var target = context.SemanticModel.GetSymbolInfo(tagetNameIfSimpleLambdaExpression).Symbol;
			if (target == null)
			{
				// We won't be able to retrieve a Symbol "if the given expression did not bind successfully to a single symbol" - this means
				// that the code is not in a complete state. We can only identify errors when everything is properly written and consistent.
				return;
			}
			var property = target as IPropertySymbol;
			if (property == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SimplePropertyAccessorArgumentAccessRule,
					context.Node.GetLocation()
				));
				return;
			}

			if (property.GetMethod == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SimplePropertyAccessorArgumentAccessRule,
					context.Node.GetLocation()
				));
				return;
			}
			if (HasDisallowedAttribute(property.GetMethod))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					BridgeAttributeAccessRule,
					context.Node.GetLocation()
				));
				return;
			}

			if (property.SetMethod == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					SimplePropertyAccessorArgumentAccessRule,
					context.Node.GetLocation()
				));
				return;
			}
			if (HasDisallowedAttribute(property.SetMethod))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					BridgeAttributeAccessRule,
					context.Node.GetLocation()
				));
				return;
			}
		}

		private static bool HasDisallowedAttribute(ISymbol symbol)
		{
			if (symbol == null)
				throw new ArgumentNullException(nameof(symbol));

			// I originally intended to just fail getters or setters with a [Name] attribute, but then I realised that [Template] does something
			// very similar and that [Ignore] would result in the method not being emitted at all.. at the end of the day, I don't think that
			// ANY of the Bridge attributes (since they are all about changing the translation behaviour) should be allowed
			return symbol.GetAttributes().Any(a => a.AttributeClass.ContainingNamespace.Name == BridgeAssemblyName);
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
