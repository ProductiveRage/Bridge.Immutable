using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProductiveRage.Immutable.Analyser
{
	public static class CommonAnalyser
	{
		public const string AnalyserAssemblyName = "ProductiveRage.Immutable";
		private const string BridgeAssemblyName = "Bridge";

		public static PropertyValidationResult GetPropertyRetrieverArgumentStatus(ArgumentSyntax propertyRetrieverArgument, SyntaxNodeAnalysisContext context)
		{
			if (propertyRetrieverArgument == null)
				throw new ArgumentNullException(nameof(propertyRetrieverArgument));

			SimpleNameSyntax targetNameIfSimpleLambdaExpression;
			if (propertyRetrieverArgument.Expression.Kind() != SyntaxKind.SimpleLambdaExpression)
				return PropertyValidationResult.NotSimpleLambdaExpression;
			else
			{
				var propertyRetrieverExpression = (SimpleLambdaExpressionSyntax)propertyRetrieverArgument.Expression;
				if (propertyRetrieverExpression.Body.Kind() != SyntaxKind.SimpleMemberAccessExpression)
					return PropertyValidationResult.NotSimpleLambdaExpression;
				targetNameIfSimpleLambdaExpression = ((MemberAccessExpressionSyntax)propertyRetrieverExpression.Body).Name;
			}

			var target = context.SemanticModel.GetSymbolInfo(targetNameIfSimpleLambdaExpression).Symbol;
			if (target == null)
			{
				// We won't be able to retrieve a Symbol "if the given expression did not bind successfully to a single symbol" - this means
				// that the code is not in a complete state. We can only identify errors when everything is properly written and consistent.
				return PropertyValidationResult.UnableToConfirmOrDeny;
			}

			var property = target as IPropertySymbol;
			if (property == null)
				return PropertyValidationResult.LambdaDoesNotTargetProperty;

			if (property.GetMethod == null)
				return PropertyValidationResult.MissingGetter;
			if (HasDisallowedAttribute(property.GetMethod))
				return PropertyValidationResult.GetterHasBridgeAttributes;

			if (property.SetMethod == null)
			{
				// If the class is in a referenced assembly then we won't be able to inspect it's setter if it's private (the metadata from
				// that assembly will not declare the presence of the private setter).
				// TODO: May need to complement this process (and try to address its limitations) by adding an analyser around IAmImmutable
				// implementations to ensure that they only use properties that follow the expected pattern (all gettable properties to also
				// have setters and for neither the getter nor setter to have Bridge attributes).
				if (property.Locations.Any(l => l.IsInMetadata))
					return PropertyValidationResult.UnableToConfirmOrDeny;
				return PropertyValidationResult.MissingSetter;
			}
			if (HasDisallowedAttribute(property.SetMethod))
				return PropertyValidationResult.SetterHasBridgeAttributes;

			return PropertyValidationResult.Ok;
		}

		public enum PropertyValidationResult
		{
			Ok,

			NotSimpleLambdaExpression,
			LambdaDoesNotTargetProperty,

			MissingGetter,
			MissingSetter,
			GetterHasBridgeAttributes,
			SetterHasBridgeAttributes,

			UnableToConfirmOrDeny
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
	}
}
