using System;
using Bridge;

namespace ProductiveRage.Immutable
{
	// Borrowed from https://github.com/AArnott/ImmutableObjectGraph - but tweaked slightly: changing the initialisation logic to treat a
	// null value the same as Missing, since I think this is more logical (I can't see why there should be a way to say that a value is
	// missing AND a way to say that this value is not missing but that it is null; surely they indicate the same thing) and added
	// equality logic.
	[Immutable]
	public struct Optional<T> : IEquatable<Optional<T>>
	{
		public Optional(T value) : this(value, value != null) { }
		private Optional(T value, bool isDefined)
		{
			// Need to check both isDefined and whether the value is null when deciding whether the instace's IsDefined should be set -
			// if the Missing value is used for a non-reference type (such as int) then value will be non-null (since non-reference
			// types CAN'T be null) and isDefined will be false, so the final IsDefined value should be false. If an instance is
			// declared through the public constructor (or through a cast or through the static generic For function) and T is
			// a reference type then IsDefined only be set to true if the specified value is not null (since there is no point
			// in saying IsDefined: true but value: null, if a null value is desired then IsDefined should be false!)
			IsDefined = isDefined && (value != null);
			Value = value;
		}

		/// <summary>
		/// Gets an instance that indicates the value was not specified.
		/// </summary>
		public static Optional<T> Missing { get { return _missing; } }
		private static Optional<T> _missing = new Optional<T>(default(T), false);

		// 2017-06-14 DWR: Due to the way that the Bridge interpretation of JSON.NET works (and because it doesn't support [JsonConstructor]),
		// these properties can't be readonly (getter-only) - the private setters are required so that the deserialisation process can work. That
		// is the only time that these values should be manipulated on an existing instance - for all other times, they may be considered readonly
		// readonly and the properties to have no setter.

		/// <summary>
		/// Gets a value indicating whether the value was specified.
		/// </summary>
		public bool IsDefined { get; private set; }

		/// <summary>
		/// Gets the specified value, or the default value for the type if <see cref="IsDefined"/> is <c>false</c>.
		/// </summary>
		public T Value { get; private set; }

		public T GetValueOrDefault(T defaultValue)
		{
			return IsDefined ? Value : defaultValue;
		}

		/// <summary>
		/// If this Optional instance has a value then the value will be transformed using the specified mapper. If this instance
		/// does not have a value or if the mapper returns null then a Missing Optional-of-TResult will be returned.
		/// </summary>
		public Optional<TResult> Map<TResult>(Func<T, TResult> mapper)
		{
			if (mapper == null)
				throw new ArgumentNullException(nameof(mapper));

			if (IsDefined)
			{
				var newValue = mapper(Value);
				if (newValue == null)
					return Optional<TResult>.Missing;

				// 2017-10-16 DWR: Don't try to use this if-new-and-old-values-are-the-same-and-TResult-is-the-same-as-T-then-return-this logic if T is DateTime because the implementation of DateTime
				// is such that it will return true from Equals if two DateTimes have the same date and time but different time zones (which seems crazy to me since they're not representing the same
				// value - but the behaviour is consistent with .NET). I'm on the fence about removing this micro-optimisation entirely - all that it saves is a new reference to a Bridge representation
				// of a struct (in Bridge, each struct object is really a regular object instance made to look like a .NET struct whereas .NET structs are inherently different and are not tracked by
				// the garbage collector and so trying to "reuse" a struct instance in .NET would not make any sense).
				var newValueEqualsOldValue = (typeof(T) != typeof(DateTime)) && newValue.Equals(Value);
				if ((typeof(TResult) == typeof(T)) && newValueEqualsOldValue)
				{
					// If the destination type is the same as the current type and the new value is the same as the existing value
					// then just return this instance immediately, rather than creating a new issue. We can't perform a cast because
					// the compiler will complain.
					return Script.Write<Optional<TResult>>("this");
				}
				return newValue;
			}

			// Don't need to worry about returning new instances here, the "Missing" value is shared across all Optional<T> instances
			return Optional<TResult>.Missing;
		}

		/// <summary>
		/// Implicitly wraps the specified value as an Optional.
		/// </summary>
		public static implicit operator Optional<T>(T value)
		{
			return (value == null) ? _missing : new Optional<T>(value);
		}

		public static bool operator ==(Optional<T> x, Optional<T> y)
		{
			return x.Equals(y);
		}

		public static bool operator !=(Optional<T> x, Optional<T> y)
		{
			return !(x == y);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Optional<T>))
				return false;
			return Equals((Optional<T>)obj);
		}

		public bool Equals(Optional<T> other)
		{
			if (!IsDefined && !other.IsDefined)
				return true;
			else if (!IsDefined || !other.IsDefined)
				return false;
			return Value.Equals(other.Value);
		}

		public override int GetHashCode()
		{
			return IsDefined ? Value.GetHashCode() : 0; // Choose zero for no-value to be consistent with the framework Nullable type
		}

		public override string ToString()
		{
			return IsDefined ? Value.ToString() : "{Missing}";
		}
	}

	public static class Optional
	{
		public static Optional<T> For<T>(T value)
		{
			return value;
		}
	}
}