using System;

namespace ProductiveRage.Immutable
{
	// Borrowed from https://github.com/AArnott/ImmutableObjectGraph - but tweaked slightly: changing the initialisation logic to treat a
	// null value the same as Missing, since I think this is more logical (I can't see why there should be a way to say that a value is
	// missing AND a way to say that this value is not missing but that it is null; surely they indicate the same thing) and added
	// equality logic.
	public struct Optional<T> : IEquatable<Optional<T>>
	{
		private readonly T value;
		private readonly bool isDefined;

		public Optional(T value) : this(value, value != null) { }
		private Optional(T value, bool isDefined)
		{
			this.isDefined = (value != null);
			this.value = value;
		}

		// 2015-11-27 DWR: I used to use a private static "_missing" field that was then returned from this property - but that caused a
		// problem with Bridge 1.10 (see http://forums.bridge.net/forum/bridge-net-pro/bugs/829) and it's not so important with structs
		// (with a reference type you would want a single "Missing" reference but since structs are copied when passed around, it
		// doesn't make much difference).
		/// <summary>
		/// Gets an instance that indicates the value was not specified.
		/// </summary>
		public static Optional<T> Missing { get { return new Optional<T>(default(T), false); } }

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
		/// Implicitly wraps the specified value as an Optional.
		/// </summary>
		public static implicit operator Optional<T>(T value)
		{
			return new Optional<T>(value);
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
	}

	public static class Optional
	{
		public static Optional<T> For<T>(T value)
		{
			return value;
		}
	}
}