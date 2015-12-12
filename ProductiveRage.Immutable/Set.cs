using System;
using System.Collections;
using System.Collections.Generic;

namespace ProductiveRage.Immutable
{
	public static class Set
	{
		/// <summary>
		/// This will throw an exception for any null references in the values parameters - if nulls may be required then the type parameter should be an Optional
		/// </summary>
		public static Set<T> Of<T>(params T[] values)
		{
			var list = Set<T>.Empty;
			if (values != null)
			{
				for (var i = values.Length - 1; i >= 0; i--)
				{
					var item = values[i];
					if (item == null)
						throw new ArgumentException("Null reference encountered at index " + i);
					list = list.Insert(item);
				}
			}
			return list;
		}
	}

	public sealed class Set<T> : IEnumerable<T>
	{
		private readonly static Set<T> _empty = new Set<T>(null);
		public static Set<T> Empty { get { return _empty; } }

		private readonly Node _headIfAny;
		private Set(Node headIfAny)
		{
			_headIfAny = headIfAny;
		}

		// Making this a unit prevents having to have a summary comment explaining that it will always be zero or greater
		public uint Count { get { return (_headIfAny == null) ? 0 : (uint)_headIfAny.Count; } }

		/// <summary>
		/// Due to the internal structure of this class, this is the cheapest way to add an item to a set. Null references are not allowed (an exception will be thrown),
		/// if you require values that may be null then the type parameter should be an Optional.
		/// </summary>
		public Set<T> Insert(T item)
		{
			if (item == null)
				throw new ArgumentNullException("item");

			if (_headIfAny == null)
				return new Set<T>(new Node { Count = 1, Item = item, NextIfAny = null });

			return new Set<T>(new Node
			{
				Count = _headIfAny.Count + 1,
				Item = item,
				NextIfAny = _headIfAny
			});
		}

		/// <summary>
		/// This will throw an exception for an invalid index value. It will never return a null reference as this data type will not store null references - if nulls
		/// may be required then the type parameter should be an Optional.
		/// </summary>
		public T this[uint index] // Using uint prevents a negative value from ever being passed in (one less condition to check for at runtime)
		{
			get
			{
				if (index >= Count)
					throw new ArgumentOutOfRangeException("index");

				var node = _headIfAny;
				for (var i = 0; i < index; i++)
					node = node.NextIfAny;
				return node.Item;
			}
		}

		/// <summary>
		/// This will throw an exception for an invalid index value or for a null value reference. This data type will not store null references - if nulls may be required
		/// then the type parameter should be an Optional.
		/// </summary>
		public Set<T> SetValue(uint index, T value)
		{
			if (index >= Count)
				throw new ArgumentOutOfRangeException("index");
			if (value == null)
				throw new ArgumentNullException("value");

			var valuesBeforeUpdate = new T[index];
			var node = _headIfAny;
			for (var i = 0; i < index; i++)
			{
				valuesBeforeUpdate[i] = node.Item;
				node = node.NextIfAny;
			}
			if (node.Item.Equals(value))
				return this; // If the new value is the same as the current then return this instance unaltered
			var nodeAfterUpdate = node.NextIfAny;
			var newNode = new Node
			{
				Count = (nodeAfterUpdate == null) ? 1 : (nodeAfterUpdate.Count + 1),
				Item = value,
				NextIfAny = nodeAfterUpdate
			};
			for (var i = valuesBeforeUpdate.Length - 1; i >= 0; i--)
			{
				newNode = new Node
				{
					Count = newNode.Count + 1,
					Item = valuesBeforeUpdate[i],
					NextIfAny = newNode
				};
			}
			return new Set<T>(newNode);
		}

		public IEnumerator<T> GetEnumerator()
		{
			var node = _headIfAny;
			while (node != null)
			{
				yield return node.Item;
				node = node.NextIfAny;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

		private sealed class Node
		{
			public int Count;
			public T Item;
			public Node NextIfAny;
		}
	}
}