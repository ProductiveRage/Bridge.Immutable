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

		public static PropertyValidationResult GetPropertyRetrieverArgumentStatus(
			ArgumentSyntax propertyRetrieverArgument,
			SyntaxNodeAnalysisContext context,
			ITypeSymbol propertyValueTypeIfKnown,
			bool allowReadOnlyProperties,
			out IPropertySymbol propertyIfSuccessfullyRetrieved)
		{
			if (propertyRetrieverArgument == null)
				throw new ArgumentNullException(nameof(propertyRetrieverArgument));

			// Most of the validation here ensures that an argument is passed is an explicit property getter lamba (eg. "_ => _.Name") but there
			// are times where it would be helpful to be able to share a lambda reference (or pass one into a method that will then pass it to a
			// With call) and so we don't want to ONLY support the explicit lambda formats. To enable that, there is a PropertyIdentifier<T, TProp>
			// class that may be cast to a Func<T, TProp> which means that the lambda validation need not apply (note that the lambda validation
			// WILL be applied to the PropertyIdentifier<T, TProp> initialisations, so validation may not be bypassed in this manner - it's just
			// moved around a bit)
			// - Note: We don't have to worry about propertyValueTypeIfKnown here because the validation around ensuring that we don't use too
			//   loose of a type will have been done when the PropertyIdentifier<T, TPropertyValue> was initialised 
			if (IsPropertyIdentifierReference(propertyRetrieverArgument.Expression, context))
			{
				propertyIfSuccessfullyRetrieved = null;
				return PropertyValidationResult.Ok;
			}

			// An alternative to generating and passing round PropertyIdentifier<T, TPropertyValue> references is to mark method arguments that are
			// of the the appropriate lambda forms with the [PropertyIdentifier] attribute - then the parameter may be passed into a With or CtorSet
			// method (since there will have been validation elsewhere to ensure that the argument passed to [PropertyIdentifier] parameter meets
			// the criteria checked for below)
			// - Note: We don't have to worry about propertyValueTypeIfKnown for the same reason as we don't above; the validation will have been
			//   applied at the point at which the [PropertyIdentifier] argument was provided
			bool isNotPropertyIdentifierButIsMethodParameterOfDelegateType;
			if (IsPropertyIdentifierArgument(propertyRetrieverArgument.Expression, context, out isNotPropertyIdentifierButIsMethodParameterOfDelegateType))
			{
				propertyIfSuccessfullyRetrieved = null;
				return PropertyValidationResult.Ok;
			}
			else if (isNotPropertyIdentifierButIsMethodParameterOfDelegateType)
			{
				// If it the property identifier is a method argument value and it's a delegate but it doesn't have the [PropertyIdentifier] attribute
				// on it then it seems likely that the user has just forgotten it (or is not aware of it) so, instead of showing the more generic
				// NotSimpleLambdaExpression warning, allow the analyser to show a more helpful message.
				propertyIfSuccessfullyRetrieved = null;
				return PropertyValidationResult.MethodParameterWithoutPropertyIdentifierAttribute;
			}

			SimpleNameSyntax targetNameIfSimpleLambdaExpression;
			if (propertyRetrieverArgument.Expression.Kind() != SyntaxKind.SimpleLambdaExpression)
			{
				propertyIfSuccessfullyRetrieved = null;
				return PropertyValidationResult.NotSimpleLambdaExpression;
			}
			else
			{
				var propertyRetrieverExpression = (SimpleLambdaExpressionSyntax)propertyRetrieverArgument.Expression;
				if (propertyRetrieverExpression.Body.Kind() != SyntaxKind.SimpleMemberAccessExpression)
				{
					propertyIfSuccessfullyRetrieved = null;
					return PropertyValidationResult.NotSimpleLambdaExpression;
				}

				var memberAccess = (MemberAccessExpressionSyntax)propertyRetrieverExpression.Body;
				if (memberAccess.Expression.Kind() != SyntaxKind.IdentifierName)
				{
					// The lambda must be of the form "_ => _.Name" and not "_ => ((ISomething)_).Name" or anything like that. This ensures
					// that all public gettable properties on the type can be checked for a setter while also allowing it to implement other
					// interfaces which don't follow these rules, so long as those interfaces are explicitly implemented. If it was acceptable
					// to cast the lambda target then this would not be possible.
					propertyIfSuccessfullyRetrieved = null;
					return PropertyValidationResult.IndirectTargetAccess;
				}

				targetNameIfSimpleLambdaExpression = ((MemberAccessExpressionSyntax)propertyRetrieverExpression.Body).Name;
			}

			var target = context.SemanticModel.GetSymbolInfo(targetNameIfSimpleLambdaExpression).Symbol;
			if (target == null)
			{
				// We won't be able to retrieve a Symbol "if the given expression did not bind successfully to a single symbol" - this means
				// that the code is not in a complete state. We can only identify errors when everything is properly written and consistent.
				{
					propertyIfSuccessfullyRetrieved = null;
					return PropertyValidationResult.UnableToConfirmOrDeny;
				}
			}

			propertyIfSuccessfullyRetrieved = target as IPropertySymbol;
			if (propertyIfSuccessfullyRetrieved == null)
				return PropertyValidationResult.LambdaDoesNotTargetProperty;

			if (propertyIfSuccessfullyRetrieved.GetMethod == null)
				return PropertyValidationResult.MissingGetter;
			if (HasDisallowedAttribute(propertyIfSuccessfullyRetrieved.GetMethod))
				return PropertyValidationResult.GetterHasBridgeAttributes;

			var hasReadOnlyAttribute = propertyIfSuccessfullyRetrieved.GetAttributes().Any(
				a => a.AttributeClass.ToString() == AnalyserAssemblyName + ".ReadOnlyAttribute"
			);
			if (!allowReadOnlyProperties && hasReadOnlyAttribute)
				return PropertyValidationResult.IsReadOnly;

			// Note about looking for a setter: Previously, it was required that a property have a getter AND a setter, though it was fine for
			// that setter to be private (in fact, it SHOULD be private if the containing class is not to have modifiable instances). This check
			// had to be relaxed for properties in classes in referenced assemblies since the meta data for the external assembly would not declare
			// the presence of a private setter. Now that (as of September 2016) Bridge supports C# 6 syntax, we also have to deal with the case
			// where a setter may not be present in classes that we have access to the source code for at this point. We COULD do extra work to
			// try to ensure that this only happens if the getter has no body (meaning that it must be a readonly auto-property, if it has a
			// getter with no body and has no setter) but I think that it makes more sense to just skip the check altogether - it's skipped for
			// referenced assemblies because the assumption is that the IAmImmutable analyser would pick up any invalid classes in the project
			// project in which those classes were written, so we can do the same here (yes, someone could bypass that by disabling the analyser
			// or using Visual Studio pre-2015 or one of any number of other means) but there are lots of ways to get around the type system if
			// you're creative when considering a Bridge project since JavaScript is so malleable (so it doesn't seem worth going mad trying to
			// make it impossible to circumvent, it's fine just to make it so that there's clearly some shenanigans going on and that everything
			// will work if there isn't).
			if ((propertyIfSuccessfullyRetrieved.SetMethod != null) && HasDisallowedAttribute(propertyIfSuccessfullyRetrieved.SetMethod))
				return PropertyValidationResult.SetterHasBridgeAttributes;

			// Ensure that the property value is at least as specific a type as the target property. For example, if the target property is of
			// type string and we know that the value that the code wants to set that property to be is an object then we need to nip that in
			// the bud. This is, unfortunately, quite an easy situation to fall into - if, for example, "x" is an instance of an IAmImmutable-
			// implementing class that has a "Name" property that is a string then the following will compile
			//
			//   x = x.With(_ => _.Name, new object());
			//
			// Although the lambda "_ => _.Name" is a Func<Whatever, string> it may also be interpreted as a Func<Whatever, object> if the
			// "TPropertyValue" generic type argument of the With<T, TPropertyValue> is inferred (or explicitly specified) as object.
			if ((propertyValueTypeIfKnown != null) && !(propertyValueTypeIfKnown is IErrorTypeSymbol))
			{
				if (!IsEqualToOrInheritsFrom(propertyValueTypeIfKnown, propertyIfSuccessfullyRetrieved.GetMethod.ReturnType))
					return PropertyValidationResult.PropertyIsOfMoreSpecificTypeThanSpecificValueType;
			}
			return PropertyValidationResult.Ok;
		}

		public enum PropertyValidationResult
		{
			Ok,

			NotSimpleLambdaExpression,
			LambdaDoesNotTargetProperty,
			IndirectTargetAccess,

			MissingGetter,
			GetterHasBridgeAttributes,
			SetterHasBridgeAttributes,
			IsReadOnly,

			PropertyIsOfMoreSpecificTypeThanSpecificValueType,

			MethodParameterWithoutPropertyIdentifierAttribute,

			UnableToConfirmOrDeny
		}

		public static bool HasDisallowedAttribute(IMethodSymbol symbol)
		{
			if (symbol == null)
				throw new ArgumentNullException(nameof(symbol));

			// I originally intended to just fail getters or setters with a [Name] attribute, but then I realised that [Template] does something
			// very similar and that [Ignore] would result in the method not being emitted at all.. at the end of the day, I don't think that
			// ANY of the Bridge attributes (since they are all about changing the translation behaviour) should be allowed
			return symbol.GetAttributes().Any(a => a.AttributeClass.ContainingNamespace.Name == BridgeAssemblyName);
		}

		public static bool ImplementsIAmImmutable(INamedTypeSymbol classOrInterfaceSymbol)
		{
			if (classOrInterfaceSymbol == null)
				throw new ArgumentNullException(nameof(classOrInterfaceSymbol));

			return
				(classOrInterfaceSymbol.ToString() == AnalyserAssemblyName + ".IAmImmutable") ||
				((classOrInterfaceSymbol.BaseType != null) && ImplementsIAmImmutable(classOrInterfaceSymbol.BaseType)) ||
				classOrInterfaceSymbol.Interfaces.Any(i => ImplementsIAmImmutable(i));
		}

		private static bool IsPropertyIdentifierReference(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
		{
			if (expression == null)
				throw new ArgumentNullException(nameof(expression));

			var typeOfExpression = context.SemanticModel.GetTypeInfo(expression).Type as INamedTypeSymbol;
			if (typeOfExpression == null)
				return false; // Most likely typeOfExpression is an IErrorTypeSymbol if it's not an INamedTypeSymbol

			// We just need to confirm the type name, the namespace and the number of generic type parameters to confirm that it is the
			// PropertyIdentifier type (we can't load the type into the analyser AND into the Bridge project being analysed, so we have
			// to do this sort of guesswork - it's possible that someone could create their own PropertyIdentifier and put it in the
			// same namespace, but they wouldn't be able to do it while having precisely two type params)
			if (typeOfExpression.TypeArguments.Length != 2)
				return false;

			return
				(typeOfExpression.Name == "PropertyIdentifier") &&
				(typeOfExpression.ContainingAssembly != null) &&
				(typeOfExpression.ContainingAssembly.Name == AnalyserAssemblyName);
		}

		private static bool IsPropertyIdentifierArgument(ExpressionSyntax expression, SyntaxNodeAnalysisContext context, out bool isNotPropertyIdentifierButIsMethodParameterOfDelegateType)
		{
			if (expression == null)
				throw new ArgumentNullException(nameof(expression));

			// Does the expression reference a parameter of the current method?
			var parameter = context.SemanticModel.GetSymbolInfo(expression).Symbol as IParameterSymbol;
			if (parameter == null)
			{
				isNotPropertyIdentifierButIsMethodParameterOfDelegateType = false;
				return false;
			}

			// Does this parameter have the [PropertyIdentifier] attribute? If so, exit now - we'll not bother trying to ensure that the
			// parameter is of an appropriate delegate type, that task should be handled elsewhere (in the PropertyIdentifierAttributeAnalyzer)
			if (HasPropertyIdentifierAttribute(parameter))
			{
				isNotPropertyIdentifierButIsMethodParameterOfDelegateType = false;
				return true;
			}

			// Does this parameter appear in an anonymous method call where the lambda is passed into a method as a delegate and the parameter on the
			// delegate is marked as [PropertyIdentifier]? (Same applies as above; we presume that if [PropertyIdentifier] is present then it's safe
			// to use, the PropertyIdentifierAttributeAnalyzer is responsible for making sure of that).
			if (IsExpressionParameterInDelegateThatIsMarkedAsPropertyIdentifier(parameter, context))
			{
				isNotPropertyIdentifierButIsMethodParameterOfDelegateType = false;
				return true;
			}

			// If it's not a [PropertyIdentifier] parameter but the parameter IS a delegate then return this information - it will allow us to
			// generate more useful warnings (such as "you are trying to specify a delegate-type method parameter value, did you mean to use the
			// [PropertyIdentifier] attriute?" instead of just saying "must be a simple property-accessing lambda").
			var parameterTypeNamedSymbol = parameter.Type as INamedTypeSymbol;
			isNotPropertyIdentifierButIsMethodParameterOfDelegateType =
				(parameterTypeNamedSymbol != null) &&
				(parameterTypeNamedSymbol.DelegateInvokeMethod != null) &&
				!parameterTypeNamedSymbol.DelegateInvokeMethod.ReturnsVoid;
			return false;
		}

		private static bool IsExpressionParameterInDelegateThatIsMarkedAsPropertyIdentifier(IParameterSymbol parameter, SyntaxNodeAnalysisContext context)
		{
			if (parameter == null)
				throw new ArgumentNullException(nameof(parameter));

			// This next bit is a bit of a nightmare! If we have a delegate that has a parameter that is identified as [PropertyIdentifier] - eg.
			//
			//   public delegate void MyDelegate([PropertyIdentifier] Func<SomethingWithAnId, int> propertyIdentifier, string something);
			//
			// then we might want use the [PropertyIdentifier] parameter in a "With" call. Something like this:
			//
			//   UpdateProperty((property, something) => x.With(property, 456));
			//
			// where UpdateProperty is a function taking a MyDelegate reference -
			//
			//   private static void UpdateProperty(MyDelegate property)
			//   {
			//     property(_ => _.Id, ""abc"");
			//   }
			//
			// Elsewhere we will ensure that the lamba passed as the [PropertyIdentifier] value is of the correct form and here we just need to ensure that the "property" value being passed to
			// "With" has [PropertyIdentifier] on it. The problem is that Roslyn identifies the "(property, something) => x.With(property, 456)" lambda only as an
			//
			//   Action<Func<SomethingWithAnId, int>, string>
			//
			// and NOT as a
			//
			//   MyDelegate
			//
			// and so we can't easily see that we're passing that lambda in somewhere that it IS a MyDelegate and so we can't easily see that it will have to meet the [PropertyIdentifier] restrictions.
			// Instead, we have to do some more work - we need to look at the method that the lambda is being passed to and see if IT'S method signature specifies the lambda as a delegate and, if so,
			// whether the parameter of that lambda is marked as [PropertyIdentifier] in the delegate's definition.

			// Try to get the place in the source code where the parameter is declared (not just where it's being used but where it is first declared)..
			var parameterDeclaringSyntax = parameter.DeclaringSyntaxReferences[0].GetSyntax() as ParameterSyntax;
			if ((parameterDeclaringSyntax == null) || (parameter.DeclaringSyntaxReferences.Count() != 1))
				return false;

			// .. and confirm that the place at which it is declared is as a parameter to an anonymous method (if not then we're not interested in it)
			var containingAnonymousMethod = parameter.ContainingSymbol as IMethodSymbol;
			if ((containingAnonymousMethod == null) || (containingAnonymousMethod.MethodKind != MethodKind.AnonymousFunction) || (containingAnonymousMethod.DeclaringSyntaxReferences.Count() != 1))
				return false;

			// Work out which parameter of the anonymous method it is that we're looking at (if we're mapping the anonymous method onto a User-defined delegate then we need to ensure that the parameter
			// we're looking at is one of the ones with [PropertyIdentifier] since there may be parameters with it and parameters without it)
			int parameterIndexInDelegate;
			var anonymousMethodSyntax = containingAnonymousMethod.DeclaringSyntaxReferences[0].GetSyntax();
			var parenthesizedLambdaMethodSyntax = anonymousMethodSyntax as ParenthesizedLambdaExpressionSyntax; // Anonymous method with brackets around the zero, single or multiple parameters
			if (parenthesizedLambdaMethodSyntax != null)
			{
				var indexedParameter = parenthesizedLambdaMethodSyntax.ParameterList.Parameters
					.Select((p, i) => new { Parameter = p, Index = i })
					.FirstOrDefault(indexedParam => indexedParam.Parameter == parameterDeclaringSyntax);
				parameterIndexInDelegate = (indexedParameter == null) ? -1 : indexedParameter.Index;
			}
			else
			{
				var nonParenthesizedLambdaMethodSyntax = anonymousMethodSyntax as SimpleLambdaExpressionSyntax; // Anonymous method with a single parameter and no brackets around it
				if (nonParenthesizedLambdaMethodSyntax != null)
					parameterIndexInDelegate = 0;
				else
					return false;
			}

			// We're only supporting the case where the lambda is passed directly into a method - this probably means that there are SOME cases where [PropertyIdentifier] isn't recognised quite
			// right (and so potentially false positives of the "you are not allowed to do this" type) but this is still an improvement. As such, we're presuming that the anonymous method is being
			// passed to a method, in which case the anonymous method's parent will be an argument syntax whose parent will be an argument list syntax whose parent will be a method invocation).
			var arg = anonymousMethodSyntax.Parent as ArgumentSyntax;
			if (arg == null)
				return false;
			var argList = arg.Parent as ArgumentListSyntax;
			if (argList == null)
				return false;
			var invocation = argList.Parent as InvocationExpressionSyntax;
			if (invocation == null)
				return false;

			// The anonymous method may be one of multiple arguments passed to the target method - we need to known which one it is so that we can work out what type it is (whether it is a User-
			// defined delegate)
			var indexedArgument = argList.Arguments.Select((a, i) => new { Argument = a, Index = i }).FirstOrDefault(indexedArg => indexedArg.Argument == arg);
			if (indexedArgument == null)
				return false;

			// Get the target method that is being called (that the anonymous method is being passed in as an argument for)
			var invocationExpressionSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if ((invocationExpressionSymbol == null) || (indexedArgument.Index >= invocationExpressionSymbol.Parameters.Length)) // Length check just in case we're analysing invalid C#
				return false;

			// Get the parameter of the method that we're passing the anonymous method in as the value for and determine whether it's a delegate or not
			var invocationTargetParameterType = invocationExpressionSymbol.Parameters[indexedArgument.Index].Type;
			var delegateInfo = (invocationTargetParameterType as INamedTypeSymbol)?.DelegateInvokeMethod;
			if ((delegateInfo == null) || (parameterIndexInDelegate >= delegateInfo.Parameters.Length)) // Length check just in case we're analysing invalid C#
				return false;

			// If it IS a delegate then we just need to check whether the parameter that we're providing with the original "parameter" reference has [PropertyIdentifier] on it or not! Phew!
			return HasPropertyIdentifierAttribute(delegateInfo.Parameters[parameterIndexInDelegate]);
		}

		public static bool HasPropertyIdentifierAttribute(IParameterSymbol parameter)
		{
			if (parameter == null)
				throw new ArgumentNullException(nameof(parameter));

			var attributes = parameter.GetAttributes();
			return attributes.Any(a =>
				(a.AttributeClass.Name == "PropertyIdentifierAttribute") &&
				(a.AttributeClass.ContainingAssembly != null) &&
				(a.AttributeClass.ContainingAssembly.Name == AnalyserAssemblyName)
			);
		}

		private static bool IsEqualToOrInheritsFrom(ITypeSymbol symbol, ITypeSymbol baseTypeSymbol) // Based on http://stackoverflow.com/a/28247330/3813189
		{
			if (symbol.AllInterfaces.Any(i => AreEqual(i, baseTypeSymbol)))
				return true;
			while (true)
			{
				if (AreEqual(symbol, baseTypeSymbol))
					return true;
				if (symbol.BaseType != null)
				{
					symbol = symbol.BaseType;
					continue;
				}
				break;
			}
			return false;
		}

		private static bool AreEqual(ITypeSymbol x, ITypeSymbol y)
		{
			if (x == null)
				throw new ArgumentNullException(nameof(x));
			if (y == null)
				throw new ArgumentNullException(nameof(y));

			return x.ToString() == y.ToString();
		}
	}
}
