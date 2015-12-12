using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bridge;

namespace ProductiveRage.Immutable
{
	public static class ImmutabilityHelpers
	{
		// Delegates can't be used as types in a dictionary unless they have a Name attribute to map them on to something that exists in JavaScript (until the following bug
		// report is addressed: http://forums.bridge.net/forum/bridge-net-pro/bugs/1205-dictionary-with-tvalue-that-is-a-delegate-fails-at-runtime)
		[Name("Function")]
		private delegate void PropertySetter(object source, object newPropertyValue, bool ignoreAnyExistingLock);
		private readonly static Dictionary<CacheKey, PropertySetter> Cache = new Dictionary<CacheKey, PropertySetter>();

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type and a new value to set that reference's property to - it will
		/// try to set the property to the new value. This requires that the propertyIdentifier is, in fact, a simple lambda to a getter and that a setter exists that follows
		/// the naming convention of the getter (so if the getter is called getName then a setter must exist called setName). If these conditions are not met then an exception
		/// will be thrown. It is not acceptable for any of the arguments to be null - if the property must be nullable then it should have a type wrapped in an Optional
		/// struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value). Once a property has been set once, it may not be
		/// set again - it is "locked". Subsequent attempts to change it will result in an exception being thrown. THIS SHOULD ONLY BE CALLED FROM WITHIN CONSTRUCTORS
		/// AND THOSE CONSTRUCTOR SHOULD EXPLICITLY SET EVERY PROPERTY - that will result in every property being lock into its initial state and any attempt by an
		/// external reference to change property values will result in an exception being thrown. Note: Because this function could be used to set private state
		/// on a reference (if that reference did not lock all of the properties in its constructor) then it will only operate against types that implement the
		/// IAmImmutable interface - this is an empty interface whose only purpose is to identify a class that has been designed to work with this process.
		/// </summary>
		public static void CtorSet<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier, TPropertyValue value) where T : IAmImmutable
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (value == null)
				throw new ArgumentNullException("value");

			var setter = GetSetter(source, propertyIdentifier);
			setter(source, value, ignoreAnyExistingLock: false);
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type and a new value to set for that property - it will try to
		/// clone the source reference and then change the value of the indicated property on the new reference. The same restrictions that apply to "CtorSet" apply here (in
		/// terms of the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to follow a naming convention), if they are not met then
		/// an exception will be thrown. Note that if the new property value is the same as the current property value on the source reference then this process will be skipped
		/// and the source reference will be passed straight back out. The new property value may not be null - if the property must be nullable then it should have a type
		/// wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		public static T With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier, TPropertyValue value)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (value == null)
				throw new ArgumentNullException("value");

			// Try to get the setter delegate first since this will validate the propertyIdentifier
			var setter = GetSetter(source, propertyIdentifier);

			// Ensure that the value has actually changed, otherwise return the source reference straight back out
			var currentValue = propertyIdentifier(source);
			if (value.Equals(currentValue))
				return source;

			var update = Clone(source);
			setter(update, value, ignoreAnyExistingLock: true);
			return update;
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type that is a Set, an index that must exist within the current
		/// value for the specified property on the source reference and a new value to set for that property - it will try to clone the source reference and then change the
		/// value of the element at the specified index on the indicated property on the new reference. The same restrictions that apply to "CtorSet" apply here (in terms of
		/// the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to follow a naming convention), if they are not met then an
		/// exception will be thrown. Note that if the new value is the same as the current value then this process will be skipped and the source reference will be passed
		/// straight back out. The new property value may not be null - if the property must be nullable then it should have a type wrapped in an Optional struct, which will
		/// ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		public static T With<T, TPropertyElement>(this T source, Func<T, Set<TPropertyElement>> propertyIdentifier, uint index, TPropertyElement value)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (value == null)
				throw new ArgumentNullException("value");

			return With(source, propertyIdentifier, propertyIdentifier(source).SetValue(index, value));
		}

		/// <summary>
		/// This will take a source reference and a lambda that identifies the getter of a property on the source type and it will try to return a lambda that will take a
		/// new value for the specified property and return a new instance of the source reference, with the property on the new instance set to the provided value. This
		/// is like a partial application of the With method that takes a value argument as well as a source and propertyIdentifier. The same restrictions apply as for
		/// "CtorSet" and the other "With" implementation - the propertyIdentifier must be a simple property retrieval and the property's getter and setter may not
		/// use a Bridge [Name] attribute. The returned lambda will throw an exception if called with a null value - if the property must be nullable then it should
		/// have a type wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		public static Func<TPropertyValue, T> With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			// Try to get the setter delegate first since this will validate the propertyIdentifier
			var setter = GetSetter(source, propertyIdentifier);
			return value =>
			{
				if (value == null)
					throw new ArgumentNullException("value");

				// Ensure that the value has actually changed, otherwise return the source reference straight back out
				var currentValue = propertyIdentifier(source);
				if (value.Equals(currentValue))
					return source;

				var update = Clone(source);
				setter(update, value, ignoreAnyExistingLock: true);
				return update;
			};
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type that is a Set and an index that must exist within the
		/// current value for the specified property on the source reference. It will try to return a lambda that will take a new value for the specified index within the
		/// specified Set property and return a new instance of the source reference, with that element update. This is like a partial application of the With method that
		/// takes a value argument as well as a source, propertyIdentifier and index. The same restrictions apply as for "CtorSet" and the other "With" implementation -
		/// the propertyIdentifier must be a simple property retrieval and the property's getter and setter may not use a Bridge [Name] attribute (if any of these conditions
		/// are not met then an argument exception will be thrown, as is the case if an invalid index is specified). The returned lambda will throw an exception if called
		/// with a null value - if the property must be nullable then it should have a type wrapped in an Optional struct, which will ensure that "value" itself will not
		/// be null (though it may represent a "missing" value).
		/// </summary>
		public static Func<TPropertyElement, T> With<T, TPropertyElement>(this T source, Func<T, Set<TPropertyElement>> propertyIdentifier, uint index)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (index < 0)
				throw new ArgumentOutOfRangeException("index");

			// Try to get the setter delegate first since this will validate the propertyIdentifier
			var setter = GetSetter(source, propertyIdentifier);

			// Likewise with the index (since this function is only supposed to be used with immutable types, the "currentValue" that we retrieve now
			// should be exactly the same as we would retrieve when the lambda that we return is evaluated, so there should be no possible inconsistency
			// that could arise from retrieving the current property value now rather than when the lambda is called)
			var currentValue = propertyIdentifier(source);
			if (index >= currentValue.Count)
				throw new ArgumentOutOfRangeException("index");

			return value =>
			{
				if (value == null)
					throw new ArgumentNullException("value");

				// Ensure that the value has actually changed, otherwise return the source reference straight back out (the Set class has a condition
				// that ensures that the original Set instance is returned if the new value is the same as the existing value at the specified index)
				var newValue = currentValue.SetValue(index, value);
				if (newValue.Equals(currentValue))
					return source;

				var update = Clone(source);
				setter(update, newValue, ignoreAnyExistingLock: true);
				return update;
			};
		}

		private static T Clone<T>(T source)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			// The simplest way to clone a generic reference seems to be a combination of Object.create and then copying the properties from the source to the clone (copying
			// may not be done from the prototype since there may be instance data that must be carried across)
			T clone = default(T);
			/*@clone = Object.create(source.constructor.prototype);
				for (var i in source) {
					clone[i] = source[i];
				}*/
			return clone;
		}

		/// <summary>
		/// This will get the setter delegate from cache if available - if not then it will construct a new setter, push it into the cache and then return it
		/// </summary>
		private static PropertySetter GetSetter<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			var cacheKey = new CacheKey(source.GetClassName(), GetFunctionStringRepresentation(propertyIdentifier));

			PropertySetter setter;
			if (Cache.TryGetValue(cacheKey, out setter))
				return setter;

			setter = ConstructSetter(source, propertyIdentifier);
			Cache[cacheKey] = setter;
			return setter;
		}

		private static PropertySetter ConstructSetter<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			if (GetFunctionArgumentCount(propertyIdentifier) != 1)
				throw new ArgumentException("The specified propertyIdentifier function must have precisely one argument");

			// Ensure that the propertyIdentifier is of a form similar to
			//   function (_) { return _.getName(); }
			// Note that in minified JavaScript, it may be
			//   function(_){return _.getName()}
			// The way that this is verified is to get a "normalised" version of the string representation of the propertyIdentifier function - this removes any comments
			// and replaces any single whitespace characters (line returns, tabs, whatever) with a space and ensures that any runs of whitespace are reduced to a single
			// character. We compare this (with a reg ex) to an expected format which is a string that is built up to match the first form. This is then tweaked to make
			// the spaces and semi-colon optional (so that it can also match the minified form).
			var singleArgumentNameForPropertyIdentifier = GetFunctionSingleArgumentName(propertyIdentifier);
			var expectedStartOfFunctionContent = string.Format(
				"function ({0}) {{ return {0}.get",
				GetFunctionSingleArgumentName(propertyIdentifier)
			);
			var expectedEndOfFunctionContent = "(); }";
			var expectedFunctionFormatMatcher = new Regex(
				EscapeForReg(expectedStartOfFunctionContent).Replace(" ", "[ ]?").Replace(";", ";?") +
				"(.*)" +
				EscapeForReg(expectedEndOfFunctionContent).Replace(" ", "[ ]?").Replace(";", ";?")
			);
			var propertyIdentifierStringContent = GetNormalisedFunctionStringRepresentation(propertyIdentifier);
			var propertyNameMatchResults = expectedFunctionFormatMatcher.Exec(propertyIdentifierStringContent);
			if (propertyNameMatchResults == null)
				throw new ArgumentException("The specified propertyIdentifier function did not match the expected format - must be a simple property get, such as \"function(_) { return _.getName(); }\", rather than \"" + propertyIdentifierStringContent + "\"");

			var propertyName = propertyNameMatchResults[1];
			var propertySetterName = "set" + propertyName;
			var hasFunctionWithExpectedSetterName = false;
			var hasExpectedSetter = false;
			/*@var setter = source[propertySetterName];
				hasFunctionWithExpectedSetterName = (typeof(setter) === "function");
				if (hasFunctionWithExpectedSetterName) {
					hasExpectedSetter = (setter.length === 1); // Ensure that it takes precisely one argument
				}*/
			if (!hasFunctionWithExpectedSetterName)
				throw new ArgumentException("Failed to find expected property setter \"" + propertySetterName + "\"");
			else if (!hasExpectedSetter)
				throw new ArgumentException("Property setter does not match expected format (single argument): \"" + propertySetterName + "\"");

			return (target, newValue, ignoreAnyExistingLock) =>
			{
				var isLocked = false;
				var propertyLockName = "__" + propertyName + "_Lock";
				/*@isLocked = !ignoreAnyExistingLock && (target[propertyLockName] === true);
					if (!isLocked) {
						setter.apply(target, [newValue]);
						target[propertyLockName] = true;
					}*/
				if (isLocked)
					throw new ArgumentException("This property has been locked - it should only be set within the constructor");
			};
		}

		private static string GetFunctionSingleArgumentName<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (GetFunctionArgumentCount(propertyIdentifier) != 1)
				throw new ArgumentException("The specified propertyIdentifier function must have precisely one argument");

			// Inspired by http://stackoverflow.com/a/9924463
			var propertyIdentifierString = GetNormalisedFunctionStringRepresentation(propertyIdentifier);
			var argumentListStartsAt = propertyIdentifierString.IndexOf("(");
			var argumentListEndsAt = propertyIdentifierString.IndexOf(")");
			return propertyIdentifierString.JsSubstring(argumentListStartsAt + 1, argumentListEndsAt);
		}

		private static int GetFunctionArgumentCount<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return Script.Write<int>("propertyIdentifier.length");
		}

		private static string GetFunctionStringRepresentation<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return Script.Write<string>("propertyIdentifier.toString()");
		}

		// See http://stackoverflow.com/a/9924463 for more details
		private readonly static Regex STRIP_COMMENTS = Script.Write<Regex>(@"/(\/\/.*$)|(\/\*[\s\S]*?\*\/)|(\s*=[^,\)]*(('(?:\\'|[^'\r\n])*')|(""(?:\\""|[^""\r\n])*""))|(\s*=[^,\)]*))/mg");
		private readonly static Regex WHITESPACE_SEGMENTS = Script.Write<Regex>(@"/\s+/g");
		private static string GetNormalisedFunctionStringRepresentation<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return GetFunctionStringRepresentation(propertyIdentifier)
				.Replace(STRIP_COMMENTS, "")
				.Replace(WHITESPACE_SEGMENTS, " ")
				.Trim();
		}

		// Courtesy of http://stackoverflow.com/a/3561711
		private readonly static Regex ESCAPE_FOR_REGEX = Script.Write<Regex>(@"/[-\/\\^$*+?.()|[\]{}]/g");
		private static string EscapeForReg(string value)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			// Courtesy of http://stackoverflow.com/a/3561711
			var matcher = ESCAPE_FOR_REGEX;
			return Script.Write<string>(@"value.replace(matcher, '\\$&')");
		}

		private sealed class CacheKey : IEquatable<CacheKey>
		{
			public CacheKey(string sourceClassName, string propertyIdentifierFunctionString)
			{
				if (sourceClassName == null)
					throw new ArgumentNullException("sourceClassName");
				if (propertyIdentifierFunctionString == null)
					throw new ArgumentNullException("propertyIdentifierFunctionString");

				SourceClassName = sourceClassName;
				PropertyIdentifierFunctionString = propertyIdentifierFunctionString;
			}

			public string SourceClassName { get; private set; }
			public string PropertyIdentifierFunctionString { get; private set; }

			public bool Equals(CacheKey other)
			{
				return
					(other != null) &&
					(other.SourceClassName == SourceClassName) &&
					(other.PropertyIdentifierFunctionString == PropertyIdentifierFunctionString);
			}

			public override bool Equals(object o)
			{
				return Equals(o as CacheKey);
			}

			public override int GetHashCode()
			{
				// Inspired by http://stackoverflow.com/a/263416
				var hash = 17;
				hash = hash ^ (23 + SourceClassName.GetHashCode());
				hash = hash ^ (23 + PropertyIdentifierFunctionString.GetHashCode());
				return hash;
			}
		}
	}
}
