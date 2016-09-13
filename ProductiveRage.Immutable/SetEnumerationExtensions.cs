using System;
using System.Collections.Generic;

namespace ProductiveRage.Immutable
{
	public static class SetEnumerationExtensions
	{
		/// <summary>
		/// Since the Set class uses uint for its index value, the standard LINQ indexed Select class requires a cast from int to uint - this version, specifically
		/// for the Set class, prevents that cast from being necessary
		/// </summary>
		public static IEnumerable<TResult> Select<TSource, TResult>(this Set<TSource> source, Func<TSource, uint, TResult> selector)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (selector == null)
				throw new ArgumentNullException("selector");

			uint index = 0;
			foreach (var value in source)
			{
				yield return selector(value, index);
				index++;
			}
		}
	}
}