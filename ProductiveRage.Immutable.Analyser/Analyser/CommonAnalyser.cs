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
			ITypeSymbol propertyValueTypeIfKnown)
		{
			if (propertyRetrieverArgument == null)
				throw new ArgumentNullException(nameof(propertyRetrieverArgument));

			// Most of the validation here ensures that an argument is passed is an explicit property getter lamba (eg. "_ => _.Name") but there
			// are times where it would be helpful to be able to share a lambda reference (or pass one into a method that will then pass it to a
			// With call) and so we don't want to ONLY support the explicit lambda formats. To enable that, there is a PropertyIdentifier<T, TProp>
			// class that may be cast to a Func<T, TProp> which means that the lambda validation need not apply (note that the lambda validation
			// WILL be applied to the PropertyIdentifier<T, TProp> initialisations, so validation may not be bypassed in this manner - it's just
			// moved around a bit)
			if (IsPropertyIdentifierReference(propertyRetrieverArgument.Expression, context))
				return PropertyValidationResult.Ok;

			SimpleNameSyntax targetNameIfSimpleLambdaExpression;
			if (propertyRetrieverArgument.Expression.Kind() != SyntaxKind.SimpleLambdaExpression)
				return PropertyValidationResult.NotSimpleLambdaExpression;
			else
			{
				var propertyRetrieverExpression = (SimpleLambdaExpressionSyntax)propertyRetrieverArgument.Expression;
				if (propertyRetrieverExpression.Body.Kind() != SyntaxKind.SimpleMemberAccessExpression)
					return PropertyValidationResult.NotSimpleLambdaExpression;

				var memberAccess = (MemberAccessExpressionSyntax)propertyRetrieverExpression.Body;
				if (memberAccess.Expression.Kind() != SyntaxKind.IdentifierName)
				{
					// The lambda must be of the form "_ => _.Name" and not "_ => ((ISomething)_).Name" or anything like that. This ensures
					// that all public gettable properties on the type can be checked for a setter while also allowing it to implement other
					// interfaces which don't follow these rules, so long as those interfaces are explicitly implemented. If it was acceptable
					// to cast the lambda target then this would not be possible.
					return PropertyValidationResult.IndirectTargetAccess;
				}

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
			if ((property.SetMethod != null) && HasDisallowedAttribute(property.SetMethod))
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
				if (!IsEqualToOrInheritsFrom(propertyValueTypeIfKnown, property.GetMethod.ReturnType))
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

			PropertyIsOfMoreSpecificTypeThanSpecificValueType,

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
				(classOrInterfaceSymbol.ToString() == CommonAnalyser.AnalyserAssemblyName + ".IAmImmutable") ||
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
				(typeOfExpression.ContainingAssembly.Name == CommonAnalyser.AnalyserAssemblyName);
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
