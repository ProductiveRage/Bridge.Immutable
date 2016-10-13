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
	public class IAmImmutableAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "IAmImmutable";
		private const string Category = "Design";
		public static DiagnosticDescriptor MayNotHavePublicNonReadOnlyFieldsRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutableFieldsMayNotBePublicAndMutableMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
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
		public static DiagnosticDescriptor MayNotHavePublicSettersRule = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutablePropertiesMayNotHavePublicSetterMessageFormat)),
			Category,
			DiagnosticSeverity.Error,
			isEnabledByDefault: true
		);
		public static DiagnosticDescriptor ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod = new DiagnosticDescriptor(
			DiagnosticId,
			GetLocalizableString(nameof(Resources.IAmImmutableAnalyserTitle)),
			GetLocalizableString(nameof(Resources.IAmImmutableValidationShouldNotBePerformedInConstructor)),
			Category,
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(
					MayNotHavePublicNonReadOnlyFieldsRule,
					MustHaveSettersOnPropertiesWithGettersAccessRule,
					MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule,
					ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod
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

			// Only bother looking this up (which is relatively expensive) if we know that we have to
			var classImplementIAmImmutable = new Lazy<bool>(() => CommonAnalyser.ImplementsIAmImmutable(context.SemanticModel.GetDeclaredSymbol(classDeclaration)));
			var publicMutableFields = classDeclaration.ChildNodes()
				.OfType<FieldDeclarationSyntax>()
				.Where(field => field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)))
				.Where(field => !field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));
			foreach (var publicMutableField in publicMutableFields)
			{
				if (classImplementIAmImmutable.Value)
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MayNotHavePublicNonReadOnlyFieldsRule,
						publicMutableField.GetLocation(),
						string.Join(", ", publicMutableField.Declaration.Variables.Select(variable => variable.Identifier.Text))
					));
				}
			}

			// When the "With" methods updates create new instances, the existing instance is cloned and the target property updated - the constructor is not called on the
			// new instance, which means that any validation in there is bypassed. I don't think that there's sufficient information available at runtime (in JavaScript) to
			// create a new instance by calling the constructor instead of using this approach so, instead, validation is not allowed in the constructor - only "CtorSet"
			// calls are acceptable with an optional "Validate" call that may appear at the end of the constructor. If this "Validate" method exists then it will be called
			// after each "With" call in order to allow validation to be performed after each property update. The "Validate" method must have no parameters but may have
			// any accessibility (private probably makes most sense).
			var constructorsThatShouldUseValidateMethodIfClassImplementsIAmImmutable = new List<ConstructorDeclarationSyntax>();
			var instanceConstructors = classDeclaration.ChildNodes()
				.OfType<ConstructorDeclarationSyntax>()
				.Where(constructor => (constructor.Body != null)) // If the code is in an invalid state then the Body property might be null - safe to ignore
				.Where(constructor => !constructor.Modifiers.Any(modifier => modifier.Kind() == SyntaxKind.StaticKeyword));
			foreach (var instanceConstructor in instanceConstructors)
			{
				var constructorShouldUseValidateMethodIfClassImplementsIAmImmutable = false;
				var constructorChildNodes = instanceConstructor.Body.ChildNodes().ToArray();
				foreach (var node in constructorChildNodes.Select((childNode, i) => new { Node = childNode, IsLastNode = i == (constructorChildNodes.Length - 1) }))
				{
					var expressionStatement = node.Node as ExpressionStatementSyntax;
					if (expressionStatement == null)
					{
						constructorShouldUseValidateMethodIfClassImplementsIAmImmutable = true;
						break;
					}
					var invocation = expressionStatement.Expression as InvocationExpressionSyntax;
					if (invocation != null)
					{
						if (InvocationIsCtorSetCall(invocation, context) || (node.IsLastNode && InvocationIsAllowableValidateCall(invocation, context)))
							continue;
					}
					constructorShouldUseValidateMethodIfClassImplementsIAmImmutable = true;
				}
				if (constructorShouldUseValidateMethodIfClassImplementsIAmImmutable)
					constructorsThatShouldUseValidateMethodIfClassImplementsIAmImmutable.Add(instanceConstructor);
			}
			if (constructorsThatShouldUseValidateMethodIfClassImplementsIAmImmutable.Any())
			{
				if (classImplementIAmImmutable.Value)
				{
					foreach (var constructorThatShouldUseValidateMethod in constructorsThatShouldUseValidateMethodIfClassImplementsIAmImmutable)
					{
						context.ReportDiagnostic(Diagnostic.Create(
							ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod,
							constructorThatShouldUseValidateMethod.GetLocation()
						));
					}
				}
			}

			// This is likely to be the most expensive work (since it requires lookup of other symbols elsewhere in the solution, whereas the
			// logic below only look at code in the current file) so only perform it when required (leave it as null until we absolutely need
			// to know whether the current class implements IAmImmutable or not)
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

				// If property.ExpressionBody is an ArrowExpressionClauseSyntax then it's C# 6 syntax for a read-only property that returns
				// a value (which is different to a readonly auto-property, which introduces a backing field behind the scenes, this syntax
				// doesn't introduce a new backing field, it returns an expression). In this case, there won't be an AccessorList (it will
				// be null).
				Diagnostic errorIfAny;
				if (property.ExpressionBody is ArrowExpressionClauseSyntax)
				{
					errorIfAny = Diagnostic.Create(
						MustHaveSettersOnPropertiesWithGettersAccessRule,
						property.GetLocation(),
						property.Identifier.Text
					);
				}
				else
				{
					var getterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
					var setterIfDefined = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
					if ((getterIfDefined != null) && (setterIfDefined == null))
					{
						// If getterIfDefined is non-null but has a null Body then it's an auto-property getter, in which case not having
						// a setter is allowed since it means that it's a read-only auto-property (for which Bridge will create a property
						// setter for in the JavaScript)
						if (getterIfDefined.Body != null)
						{
							errorIfAny = Diagnostic.Create(
								MustHaveSettersOnPropertiesWithGettersAccessRule,
								property.GetLocation(),
								property.Identifier.Text
							);
						}
						else
							continue;
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
					else if ((setterIfDefined != null) && IsPublic(property) && !IsPrivateOrProtected(setterIfDefined))
					{
						errorIfAny = Diagnostic.Create(
							MayNotHavePublicSettersRule,
							setterIfDefined.GetLocation(),
							property.Identifier.Text
						);
					}
					else
						continue;
				}
				
				// Enountered a potential error if the current class implements IAmImmutable - so find out whether it does or not (if it
				// doesn't then no further work is required and we can exit the entire process early)
				if (!classImplementIAmImmutable.Value)
					return;
				context.ReportDiagnostic(errorIfAny);
			}
		}

		private static bool IsPublic(PropertyDeclarationSyntax property)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));

			return property.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword));
		}

		private static bool IsPrivateOrProtected(AccessorDeclarationSyntax propertyAccessor)
		{
			if (propertyAccessor == null)
				throw new ArgumentNullException(nameof(propertyAccessor));

			return
				propertyAccessor.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PrivateKeyword)) ||
				propertyAccessor.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ProtectedKeyword));
		}

		private static bool InvocationIsCtorSetCall(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
		{
			if (invocation == null)
				throw new ArgumentNullException(nameof(invocation));

			var lastExpressionToken = invocation.Expression.GetLastToken();
			if ((lastExpressionToken == null) || (lastExpressionToken.Text != "CtorSet"))
				return false;

			// Note: Don't need to do any work to ensure that this is a VALID "CtorSet" call since the CtorSetCallAnalyzer will do that
			var ctorSetMethod = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			return
				(ctorSetMethod != null) &&
				(ctorSetMethod.ContainingAssembly != null) &&
				(ctorSetMethod.ContainingAssembly.Name == CommonAnalyser.AnalyserAssemblyName);
		}

		private static bool InvocationIsAllowableValidateCall(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
		{
			if (invocation == null)
				throw new ArgumentNullException(nameof(invocation));

			var lastExpressionToken = invocation.Expression.GetLastToken();
			if ((lastExpressionToken == null) || (lastExpressionToken.Text != "Validate"))
				return false;

			var validateMethod = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			return
				(validateMethod != null) &&
				!validateMethod.Parameters.Any() &&
				!CommonAnalyser.HasDisallowedAttribute(validateMethod);
		}

		private static LocalizableString GetLocalizableString(string nameOfLocalizableResource)
		{
			return new LocalizableResourceString(nameOfLocalizableResource, Resources.ResourceManager, typeof(Resources));
		}
	}
}
