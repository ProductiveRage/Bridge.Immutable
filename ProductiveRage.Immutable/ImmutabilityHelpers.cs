using System;
using Bridge;

namespace ProductiveRage.Immutable
{
	public static class ImmutabilityHelpers
	{
		private delegate void PropertySetter(object source, object newPropertyValue, bool ignoreAnyExistingLock);
		private readonly static PropertySetterCache Cache = new PropertySetterCache();

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
		[IgnoreGeneric]
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
		/// There are analysers to ensure that the IAmImmutable.With extension method is only called with lambdas that match the required format (the lambdas must be a simple
		/// property access for a property that has a getter and setter and that doesn't have any special translation rules applied via Bridge attributes). However, sometimes
		/// it is useful to be able to pass references to these lambdas around, which is problematic with the analyser that checks the propertyIdentifier argument of all calls
		/// to the With method. To workaround this, a property identifier reference may be created using this method and then passed into the With method - note that all of the
		/// same validation rules are applied to GetProperty as to With, so it must still be a simple property-access lambda (but now a lambda reference may be created once and
		/// shared or passed around). Note that the source argument here is only present so that this may exist as an extension method and to make the code more succinct when
		/// working with an IAmImmutable reference already (it will be possible to use type inference to save having to explicitly specify the T and TPropertyValue type params
		/// but the returned PropertyIdentifier will not be tied to the source instance).
		/// </summary>
		[IgnoreGeneric]
		public static PropertyIdentifier<T, TPropertyValue> GetProperty<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier) where T : IAmImmutable
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return new PropertyIdentifier<T, TPropertyValue>(propertyIdentifier);
		}

		/// <summary>
		/// There are analysers to ensure that the IAmImmutable.With extension method is only called with lambdas that match the required format (the lambdas must be a simple
		/// property access for a property that has a getter and setter and that doesn't have any special translation rules applied via Bridge attributes). However, sometimes
		/// it is useful to be able to pass references to these lambdas around, which is problematic with the analyser that checks the propertyIdentifier argument of all calls
		/// to the With method. To workaround this, a property identifier reference may be created using this method and then passed into the With method - note that all of the
		/// same validation rules are applied to GetProperty as to With, so it must still be a simple property-access lambda (but now a lambda reference may be created once and
		/// shared or passed around).
		/// </summary>
		[IgnoreGeneric]
		public static PropertyIdentifier<T, TPropertyValue> GetProperty<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier) where T : IAmImmutable
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return new PropertyIdentifier<T, TPropertyValue>(propertyIdentifier);
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type and a new value to set for that property - it will try to
		/// clone the source reference and then change the value of the indicated property on the new reference. The same restrictions that apply to "CtorSet" apply here (in
		/// terms of the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to follow a naming convention), if they are not met then
		/// an exception will be thrown. Note that if the new property value is the same as the current property value on the source reference then this process will be skipped
		/// and the source reference will be passed straight back out. The new property value may not be null - if the property must be nullable then it should have a type
		/// wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		[IgnoreGeneric]
		public static T With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier, TPropertyValue value) where T : IAmImmutable
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
			ValidateAfterUpdateIfValidateMethodDefined(update);
			return update;
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type and a lambda that will receive the current value and return
		/// a new one. It will try to clone the source reference and then change the value of the indicated property on the new reference. The same restrictions that apply to
		/// "CtorSet" apply here (in terms of the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to follow a naming convention),
		/// if they are not met then an exception will be thrown. An exception will also be thrown if the valueUpdater delegate returns null - if the property must be nullable
		/// then it should have a type wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value). Note
		/// that if the new property value is the same as the current property value on the source reference then no clone will be performed and the source reference will be
		/// passed straight back out.
		/// </summary>
		[IgnoreGeneric]
		public static T With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier, Func<TPropertyValue, TPropertyValue> valueUpdater) where T : IAmImmutable
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");
			if (valueUpdater == null)
				throw new ArgumentNullException("valueUpdater");

			// Try to get the setter delegate first since this will validate the propertyIdentifier
			var setter = GetSetter(source, propertyIdentifier);

			// Ensure that the value has actually changed, otherwise return the source reference straight back out
			var currentValue = propertyIdentifier(source);
			var newValue = valueUpdater(currentValue);
			if (newValue == null)
				throw new Exception("The specified valueUpdater returned null, which is invalid (if this is a property that may sometimes not have a value then it should be of type Optional)");
			if (newValue.Equals(currentValue))
				return source;

			var update = Clone(source);
			setter(update, newValue, ignoreAnyExistingLock: true);
			ValidateAfterUpdateIfValidateMethodDefined(update);
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
		[IgnoreGeneric]
		[Obsolete("The Set class is now obsolete, it has been replaced by NonNullList - the are currently implicit casts between them but Set will be removed in a future version of the library")]
		public static T With<T, TPropertyElement>(this T source, Func<T, Set<TPropertyElement>> propertyIdentifier, uint index, TPropertyElement value) where T : IAmImmutable
		{
			// Set and NonNullList have the interface so we can safely cast from
			//   Func<T, Set<TPropertyElement>>
			// to
			//   Func<T, NonNullList<TPropertyElement>>
			// which we'll do with a Script.Write call
			return source.With(Script.Write<Func<T, NonNullList<TPropertyElement>>>("propertyIdentifier"), index, value);
		}

		/// <summary>
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type that is a NonNullList, an index that must exist within the
		/// current value for the specified property on the source reference and a new value to set for that property - it will try to clone the source reference and then change
		/// the value of the element at the specified index on the indicated property on the new reference. The same restrictions that apply to "CtorSet" apply here (in terms
		/// of the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to follow a naming convention), if they are not met then an
		/// exception will be thrown. Note that if the new value is the same as the current value then this process will be skipped and the source reference will be passed
		/// straight back out. The new property value may not be null - if the property must be nullable then it should have a type wrapped in an Optional struct, which will
		/// ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		[IgnoreGeneric]
		public static T With<T, TPropertyElement>(this T source, Func<T, NonNullList<TPropertyElement>> propertyIdentifier, uint index, TPropertyElement value) where T : IAmImmutable
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
		/// This will take a source reference, a lambda that identifies the getter of a property on the source type that is a NonNullList, an index that must exist within the
		/// current value for the specified property on the source reference and a delegate that takes the current value for that list item and returns a replacement. The same
		/// restrictions that apply to "CtorSet" apply here (in terms of the propertyIdentifier having to be a simple property retrieval and of the getter / setter having to
		/// follow a naming convention), if they are not met then an exception will be thrown. Note that if the new value is the same as the current value then this process
		/// will be skipped and the source reference will be passed straight back out. The new property value may not be null - if the property must be nullable then it should
		/// have a type wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		[IgnoreGeneric]
		public static T With<T, TPropertyElement>(
			this T source,
			Func<T, NonNullList<TPropertyElement>> propertyIdentifier,
			uint index,
			Func<TPropertyElement, TPropertyElement> valueUpdater)
				where T : IAmImmutable
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (propertyIdentifier == null)
				throw new ArgumentNullException(nameof(propertyIdentifier));
			if (valueUpdater == null)
				throw new ArgumentNullException(nameof(valueUpdater));

			var currentList = propertyIdentifier(source);
			if (index >= currentList.Count)
				throw new ArgumentOutOfRangeException(nameof(index));

			var currentValue = currentList[index];
			var updatedValue = valueUpdater(currentValue);
			if (updatedValue == null)
				throw new Exception($"The specified {nameof(valueUpdater)} returned null, which is invalid (if this is a property that may sometimes not have a value then it should be of type Optional)");

			// If the new value is the same as the current value then no change is required (the With and SetValue calls below would work this out but they would have to do
			// a little bit of work to come to the same conclusion - we may as well drop out now)
			if (updatedValue.Equals(currentValue))
				return source;

			return source.With(
				propertyIdentifier,
				currentList.SetValue(index, updatedValue)
			);
		}

		/// <summary>
		/// This will take a source reference and a lambda that identifies the getter of a property on the source type and it will try to return a lambda that will take a
		/// new value for the specified property and return a new instance of the source reference, with the property on the new instance set to the provided value. This
		/// is like a partial application of the With method that takes a value argument as well as a source and propertyIdentifier. The same restrictions apply as for
		/// "CtorSet" and the other "With" implementation - the propertyIdentifier must be a simple property retrieval and the property's getter and setter may not
		/// use a Bridge [Name] attribute. The returned lambda will throw an exception if called with a null value - if the property must be nullable then it should
		/// have a type wrapped in an Optional struct, which will ensure that "value" itself will not be null (though it may represent a "missing" value).
		/// </summary>
		[IgnoreGeneric]
		public static Func<TPropertyValue, T> With<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier) where T : IAmImmutable
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
				ValidateAfterUpdateIfValidateMethodDefined(update);
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
		[IgnoreGeneric]
		[Obsolete("The Set class is now obsolete, it has been replaced by NonNullList - the are currently implicit casts between them but Set will be removed in a future version of the library")]
#pragma warning disable CS0618 // Ignore the fact that Set is obsolete here (I'm aware, hence the Obsolete atttribute on this method)
		public static Func<TPropertyElement, T> With<T, TPropertyElement>(this T source, Func<T, Set<TPropertyElement>> propertyIdentifier, uint index) where T : IAmImmutable
#pragma warning restore CS0618
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
				ValidateAfterUpdateIfValidateMethodDefined(update);
				return update;
			};
		}

		[IgnoreGeneric]
		private static T Clone<T>(T source)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			if (IsObjectLiteral(source))
			{
				T objectLiteralClone = Script.Write<T>("{}");
				/*@for (var i in source) {
					objectLiteralClone[i] = source[i];
				}*/
				return objectLiteralClone;
			}

			// Although Bridge uses defineProperty to configure properties on classes now, rather than using a custom version based around getter and setter methods, it still
			// does has some funky business - the get and set methods in the properties look in an $init object recorded against the object, which contains the property values.
			// So, in order to clone an object, we need to create a new one based upon the same prototype and then we need to create an $init object on that new reference and
			// copy all of the values over from the old to the new. Then we need to copy the "_{PropertyName}__Lock" values over that this library puts on references (to be
			// honest, they could possibly be removed - the offer runtime protection that CtorSet is not used outside of a constructor and there is an analyser for that).
			T clone = Script.Write<T>("Object.create(Object.getPrototypeOf(source))");
			/*@
			clone.$init = {};
			for (var name in source.$init) {
				if (source.$init.hasOwnProperty(name)) {
					clone.$init[name] = source.$init[name];
				}
			}
			for (var name in source) {
				if (source.hasOwnProperty(name) && (name.substr(0, 2) === "__") && (name.substr(-5) === "_Lock")) {
					clone[name] = source[name];
				}
			}
			*/
			return clone;
		}

		/// <summary>
		/// This will get the setter delegate from cache if available - if not then it will construct a new setter, push it into the cache and then return it
		/// </summary>
		[IgnoreGeneric]
		private static PropertySetter GetSetter<T, TPropertyValue>(this T source, Func<T, TPropertyValue> propertyIdentifier)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			// The strange "(object)" cast before GetType is called is required due to a new-to-15.7.0 bug that causes GetType calls to fail if the method is generic and has
			// the [IgnoreGeneric] attribute applied to it and if the GetType target reference is one of the method's generic type arguments. By casting it to object, the
			// issue is avoided. See http://forums.bridge.net/forum/bridge-net-pro/bugs/3343 for more details.
			var cacheKey = GetCacheKey(((object)source).GetType().FullName, GetFunctionStringRepresentation(propertyIdentifier));

			PropertySetter setter;
			if (Cache.TryGetValue(cacheKey, out setter))
				return setter;

			setter = ConstructSetter(source, propertyIdentifier);
			Cache.Set(cacheKey, setter);
			return setter;
		}

		[IgnoreGeneric]
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
			// 2016-08-04 DWR: There are some additional forms that should be supported; for example, Firefox may report "use strict" as part of the function content -
			//   function (_) { "use strict"; return _.getName(); }
			// .. and code coverage tools may inject other content before the getter is called -
			//   function (_) { coverageFramework.track("MyClass.cs", 18); return _.getName(); }
			// .. as such, the function format matching has been relaxed to allow a section to be ignored before the getter call. I contempled removing this entirely since
			// there is an analyser to ensure that only valid properties are referenced in the C# code but this change seemed minor and could be useful if a project included
			// IAmImmutable implementations that disabled the analyser (or that were built in VS2013 or earlier). If there are any further problems then I may reconsider.
			var singleArgumentNameForPropertyIdentifier = GetFunctionSingleArgumentName(propertyIdentifier);

			// In case there are any new lines in the additional content that is supported before the getter call (see 2016-08-04 notes above), we need to look for "any
			// character" that includes line returns and so use "[.\s\S]*" instead of just ".*" (see http://trentrichardson.com/2012/07/13/5-must-know-javascript-regex-tips/).
			// It might be cleaner to use the C# RegEx which is fully supported by Bridge (but wasn't when this code was first written).
			var argumentName = GetFunctionSingleArgumentName(propertyIdentifier);

			// If an IAmImmutable type is also decorated with [ObjectLiteral] then instances won't actually have real getter and setter methods, they will just have raw
			// properties. There are some hoops to jump through to combine IAmImmutable and [ObjectLiteral] (the constructor won't be called and so CtorSet can't be used
			// to initialise the instance) but if this combination is required then the "With" method may still be used by identifying whether the current object is a
			// "plain object" and working directly on the property value if so.
			var isObjectLiteral = IsObjectLiteral(source);
			if (isObjectLiteral)
			{
				var objectLiteralRegExSegments = new[] {
					AsRegExSegment(string.Format(
						"function ({0}) {{",
						argumentName
					)),
					AsRegExSegment(string.Format(
						"return {0}.",
						argumentName
					)),
					AsRegExSegment("; }")
				};
				var objectLiteralExpectedFunctionFormatMatcher = new JsRegex(
					string.Join("([.\\s\\S]*?)", objectLiteralRegExSegments)
				);
				var objectLiteralPropertyIdentifierStringContent = GetNormalisedFunctionStringRepresentation(propertyIdentifier);
				var objectLiteralPropertyNameMatchResults = objectLiteralExpectedFunctionFormatMatcher.Exec(objectLiteralPropertyIdentifierStringContent);
				if (objectLiteralPropertyNameMatchResults == null)
					throw new ArgumentException("The specified propertyIdentifier function did not match the expected format - must be a simple property access for an [ObjectLiteral], such as \"function(_) { return _.name; }\", rather than \"" + objectLiteralPropertyIdentifierStringContent + "\"");

				// If the target is an [ObjectLiteral] then just set the property name on the target, don't try to call a setter (since it won't be defined)
				var objectLiteralPropertyName = objectLiteralPropertyNameMatchResults[objectLiteralPropertyNameMatchResults.Length - 1];
				return (target, newValue, ignoreAnyExistingLock) =>
				{
					Script.Write("target[{0}] = {1};", objectLiteralPropertyName, newValue);
				};
			}

			// Note: A property specified directly on the target type will be specified using the simple format "function (_) { return _.Name; }" (before Bridge 16, there
			// were custom getter and setter functions - eg. "function (_) { return _.getName(); }" - but now it uses ES5 properties and so the simpler version appears in
			// the generated JavaScript. If the target property is specified via an interface cast then an alias property name will be used (in case there is a property on
			// the target type with the same name as the interface property and then the interface property is implemented explicitly) - it will look something like
			// "function (_) { return _.Example$IHaveName$Name; }" and we need to correctly handle that below too.
			var regExSegments = new[] {
				AsRegExSegment(string.Format(
					"function ({0}) {{",
					argumentName
				)),
				AsRegExSegment(string.Format(
					"return {0}.",
					argumentName
				)),
				AsRegExSegment("; }")
			};
			var expectedFunctionFormatMatcher = new JsRegex(
				string.Join("([.\\s\\S]*?)", regExSegments)
			);
			var propertyIdentifierStringContent = GetNormalisedFunctionStringRepresentation(propertyIdentifier);
			var propertyNameMatchResults = expectedFunctionFormatMatcher.Exec(propertyIdentifierStringContent);
			if (propertyNameMatchResults == null)
				throw new ArgumentException("The specified propertyIdentifier function did not match the expected format - must be a simple property get, such as \"function(_) { return _.Name; }\", rather than \"" + propertyIdentifierStringContent + "\"");

			var typeAliasPrefix = propertyNameMatchResults[propertyNameMatchResults.Length - 2]; ;
			var propertyName = propertyNameMatchResults[propertyNameMatchResults.Length - 1];

			var propertyDescriptorIfDefined = TryToGetPropertyDescriptor(source, propertyName);
			if (propertyDescriptorIfDefined == null)
				throw new ArgumentException("Failed to find expected property \"" + propertyName + "\" (could not retrieve PropertyDescriptor)");

			var setter = propertyDescriptorIfDefined.OptionalSetter;
			if (Script.Write<bool>("!setter"))
				throw new ArgumentException("Failed to retrieve expected property setter for \"" + propertyName + "\"");
			var hasExpectedSetter = Script.Write<bool>("(typeof(setter) === \"function\") && (setter.length === 1)");
			if (!hasExpectedSetter)
				throw new ArgumentException("Property setter does not match expected format (single argument function) for \"" + propertyName + "\"");

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

		private static bool IsObjectLiteral(object source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return Script.Write<bool>("Bridge.isPlainObject({0})", source);
		}

		private static PropertyDescriptor TryToGetPropertyDescriptor(object source, string name)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException($"Null/blank {nameof(name)} specified");

			// Based upon code at http://code.fitness/post/2016/01/javascript-enumerate-methods.html
			/*@
			var proto = Object.getPrototypeOf(source);
			while (proto)
			{
				var descriptor = Object.getOwnPropertyDescriptor(proto, name);
				if (descriptor) {
					return descriptor;
				}
				proto = Object.getPrototypeOf(proto);
			}
			*/
			return null;
		}

		[External]
		[ObjectLiteral(ObjectCreateMode.Plain)]
		private sealed class PropertyDescriptor
		{
			[Name("get")]
			public Func<object> OptionalGetter { get; }
			[Name("set")]
			public Action<object> OptionalSetter { get; }
		}

		private static string AsRegExSegment(string value)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			return EscapeForReg(value).Replace(" ", "[ ]?").Replace(";", ";?");
		}

		[IgnoreGeneric]
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
			return propertyIdentifierString.Substring(argumentListStartsAt + 1, argumentListEndsAt - (argumentListStartsAt + 1));
		}

		[IgnoreGeneric]
		private static int GetFunctionArgumentCount<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return Script.Write<int>("propertyIdentifier.length");
		}

		/// <summary>
		/// If there is an argument-less Validate method on the instance then call that (this is to make up for the fact that the constructor is not called after properties are updated)
		/// </summary>
		[IgnoreGeneric]
		private static void ValidateAfterUpdateIfValidateMethodDefined<T>(T source)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			/*@
			var validate = source.validate;
			if (validate && (typeof(validate) === "function") && (validate.length === 0)) {
				validate.apply(source);
			}
			*/
		}

		[IgnoreGeneric]
		private static string GetFunctionStringRepresentation<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			return Script.Write<string>("propertyIdentifier.toString()");
		}

		// See http://stackoverflow.com/a/9924463 for more details
		private readonly static JsRegex STRIP_COMMENTS = Script.Write<JsRegex>(
			@"/(\/\/.*$)|(\/\*[\s\S]*?\*\/)|(\s*=[^,\)]*(('(?:\\'|[^'\r\n])*')|(""(?:\\""|[^""\r\n])*""))|(\s*=[^,\)]*))/mg"
		);
		private readonly static JsRegex WHITESPACE_SEGMENTS = Script.Write<JsRegex>(
			@"/\s+/g"
		);
		[IgnoreGeneric]
		private static string GetNormalisedFunctionStringRepresentation<T, TPropertyValue>(Func<T, TPropertyValue> propertyIdentifier)
		{
			if (propertyIdentifier == null)
				throw new ArgumentNullException("propertyIdentifier");

			var content = GetFunctionStringRepresentation(propertyIdentifier);
			content = Replace(content, STRIP_COMMENTS, "");
			content = Replace(content, WHITESPACE_SEGMENTS, "");
			return content.Trim();
		}

		// Courtesy of http://stackoverflow.com/a/3561711
		private readonly static JsRegex ESCAPE_FOR_REGEX = Script.Write<JsRegex>(
			@"/[-\/\\^$*+?.()|[\]{}]/g"
		);
		private static string EscapeForReg(string value)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			// Courtesy of http://stackoverflow.com/a/3561711
			var matcher = ESCAPE_FOR_REGEX;
			return Script.Write<string>(@"value.replace(matcher, '\\$&')");
		}

		private static string GetCacheKey(string sourceClassName, string propertyIdentifierFunctionString)
		{
			return sourceClassName + "\n" + propertyIdentifierFunctionString;
		}

		private static string Replace(string source, JsRegex regEx, string replaceWith)
		{
			return Script.Write<string>("{0}.replace({1}, {2})", source, regEx, replaceWith);
		}

		[External]
		[Name("RegExp")]
		private sealed class JsRegex
		{
			public JsRegex(string pattern) { }

			[Name("exec")]
			public extern string[] Exec(string s);
		}

		private sealed class PropertySetterCache
		{
			private Object _cache;
			public PropertySetterCache()
			{
				_cache = Script.Write<object>("{}");
			}

			public void Set(string cacheKey, PropertySetter value)
			{
				Script.Write("{0}[{1}] = {2}", _cache, cacheKey, value);
			}

			public bool TryGetValue(string cacheKey, out PropertySetter setter)
			{
				if (!Script.Write<bool>("{0}.hasOwnProperty({1})", _cache, cacheKey))
				{
					setter = null;
					return false;
				}
				setter = Script.Write<PropertySetter>("{0}[{1}]", _cache, cacheKey);
				return true;
			}
		}
	}
}
