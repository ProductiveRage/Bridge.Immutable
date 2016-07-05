using System;
using Bridge;

namespace ProductiveRage.Immutable
{
	/// <summary>
	/// This class allows a property identifier reference to be created and then passed into the With extension method. The validation that would be applied to the With
	/// call will be bypassed if a PropertyIdentifier is provided (instead of a Func) because the same validation is applied to the GetProperty extension method, which
	/// is the only way that an instance of this class may be created. 
	/// </summary>
	[IgnoreGeneric]
	public sealed class PropertyIdentifier<T, TPropertyValue>
	{
		internal PropertyIdentifier(Func<T, TPropertyValue> method)
		{
			if (method == null)
				throw new ArgumentNullException("method");

			Method = method;
		}

		public TPropertyValue Get(T value)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			return Method(value);
		}

		internal Func<T, TPropertyValue> Method { get; private set; }

		public static implicit operator Func<T, TPropertyValue>(PropertyIdentifier<T, TPropertyValue> source)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			return source.Method;
		}
	}
}