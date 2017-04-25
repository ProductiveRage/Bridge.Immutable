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
		private readonly T value;
		private readonly bool isDefined;

		public Optional(T value) : this(value, value != null) { }
		private Optional(T value, bool isDefined)
		{
			// Need to check both isDefined and whether the value is null when deciding whether the instace's IsDefined should be set -
			// if the Missing value is used for a non-reference type (such as int) then value will be non-null (since non-reference
			// types CAN'T be null) and isDefined will be false, so the final IsDefined value should be false. If an instance is
			// declared through the public constructor (or through a cast or through the static generic For function) and T is
			// a reference type then IsDefined only be set to true if the specified value is not null (since there is no point
			// in saying IsDefined: true but value: null, if a null value is desired then IsDefined should be false!)
			this.isDefined = isDefined && (value != null);
			this.value = value;
		}

		/// <summary>
		/// Gets an instance that indicates the value was not specified.
		/// </summary>
		public static Optional<T> Missing { get { return _missing; } }
		private static Optional<T> _missing = new Optional<T>(default(T), false);

		/// <summary>
		/// Gets a value indicating whether the value was specified.
		/// </summary>
		public bool IsDefined { get { return this.isDefined; } }

		/// <summary>
		/// Gets the specified value, or the default value for the type if <see cref="IsDefined"/> is <c>false</c>.
		/// </summary>
		public T Value { get { return this.value; } }

		public T GetValueOrDefault(T defaultValue)
		{
			return this.IsDefined ? this.value : defaultValue;
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
				var newValue = mapper(value);
				if (newValue == null)
					return Optional<TResult>.Missing;
				if ((typeof(TResult) == typeof(T)) && newValue.Equals(Value))
				{
					// If the destination type is the same as the current type and the new value is the same as the existing value
					// then just return this instance immediately, rather than creating a new issue. We can't perform a cast because
					// the compiler will complain.
					return Script.Write<Optional<TResult>>("this");
				}
				return mapper(value);
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
			if (!IsDefined && !other.isDefined)
				return true;
			else if (!IsDefined || !other.isDefined)
				return false;
			return Value.Equals(other.value);
		}

		public override int GetHashCode()
		{
			return IsDefined ? value.GetHashCode() : 0; // Choose zero for no-value to be consistent with the framework Nullable type
		}

		public override string ToString()
		{
			return isDefined ? Value.ToString() : "{Missing}";
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