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
		public const string DiagnosticId = "CtorSet";
		private const string Category = "Design";
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
		public static DiagnosticDescriptor IndirectTargetAccessorAccessRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.CtorDirectPropertyTargetAccessorArgumentMessageFormat)),
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
		public static DiagnosticDescriptor PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.TPropertyValueNotSpecificEnough)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor MethodParameterWithoutPropertyIdentifierAttributeRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.CtorAnalyserTitle)),
			GetLocalizableString(nameof(Resources.MethodParameterWithoutPropertyIdentifierAttribute)),
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
					IndirectTargetAccessorAccessRule,
					SimplePropertyAccessorArgumentAccessRule,
					BridgeAttributeAccessRule,
					PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule,
					MethodParameterWithoutPropertyIdentifierAttributeRule
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

			if ((invocation.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text != "CtorSet")
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

			// If the CtorSet method signature called is one with a TPropertyValue generic type argument then get that type. We need to pass
			// this to the GetPropertyRetrieverArgumentStatus method so that it can ensure that we are not casting the property down to a
			// less specific type, which would allow an instance of that less specific type to be set as a property value. For example, if
			// within a constructor of an IAmImmutable class that has a "Name" property of type string then the following should not be
			// allowed:
			//
			//   this.CtorSet(_ => _.Name, new object());
			//
			// This will compile (TPropertyValue willl be inferred as "Object") but we don't want to allow it since it will result in the
			// Name property being assigned a non-string reference.
			var typeArguments = ctorSetMethod.TypeParameters.Zip(ctorSetMethod.TypeArguments, (genericTypeParam, type) => new { Name = genericTypeParam.Name, Type = type });
			var propertyValueTypeIfKnown = typeArguments.FirstOrDefault(t => t.Name == "TPropertyValue")?.Type;

			IPropertySymbol propertyIfSuccessfullyRetrieved;
			switch (CommonAnalyser.GetPropertyRetrieverArgumentStatus(propertyRetrieverArgument, context, propertyValueTypeIfKnown, out propertyIfSuccessfullyRetrieved))
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
					// propertyIfSuccessfullyRetrieved and propertyValueTypeIfKnown will both be non-null if PropertyIsOfMoreSpecificTypeThanSpecificValueType was returned
					// (since it would not be possible to ascertain that that response is appropriate without being able to compare the two values)
					context.ReportDiagnostic(Diagnostic.Create(
						PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule,
						invocation.GetLocation(),
						propertyIfSuccessfullyRetrieved.GetMethod.ReturnType, // This will always have a value if we got PropertyIsOfMoreSpecificTypeThanSpecificValueType back
						propertyValueTypeIfKnown.Name
					));
					return;

				case CommonAnalyser.PropertyValidationResult.MethodParameterWithoutPropertyIdentifierAttribute:
					context.ReportDiagnostic(Diagnostic.Create(
						MethodParameterWithoutPropertyIdentifierAttributeRule,
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
