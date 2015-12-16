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
	public class WithCallAnalyzer : DiagnosticAnalyzer
	{
		private const string BridgeAssemblyName = "Bridge";
		private const string WitAssemblyName = "ProductiveRage.Immutable";

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
			|| (withMethod.ContainingAssembly.Name != WitAssemblyName))
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
