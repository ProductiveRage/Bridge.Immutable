using System;
using System.Collections;
using System.Collections.Generic;
using Bridge;

namespace ProductiveRage.Immutable
{
	[Obsolete("The Set class is now obsolete, it has been replaced by NonNullList - the are currently implicit casts between them but Set will be removed in a future version of the library")]
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

	[Obsolete("The Set class is now obsolete, it has been replaced by NonNullList - the are currently implicit casts between them but Set will be removed in a future version of the library")]
	public sealed class Set<T> : IEnumerable<T>
	{
		private readonly static Set<T> _empty = new Set<T>(null);
		public static Set<T> Empty { get { return _empty; } }

		private readonly Node _headIfAny;
		private Set(Node headIfAny)
		{
			_headIfAny = headIfAny;
		}

		// Making this a uint prevents having to have a summary comment explaining that it will always be zero or greater
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
		/// Due to the internal structure of this class, this is a more expensive operation that Insert (which inserts a new item at the start of the set, rather than at
		/// the end, which this function does).  Null references are not allowed (an exception will be thrown), if you require values that may be null then the type parameter
		/// should be an Optional.
		/// </summary>
		public Set<T> Add(T item)
		{
			if (item == null)
				throw new ArgumentNullException("item");

			var currentValues = new T[Count];
			var node = _headIfAny;
			for (var index = 0; index < Count; index++)
			{
				currentValues[index] = node.Item;
				node = node.NextIfAny;
			}
			var newHead = new Node { Count = 1, Item = item, NextIfAny = null };
			for (var loopIndex = 0; loopIndex < Count; loopIndex++)
			{
				var index = (Count - 1) - loopIndex;
				newHead = new Node
				{
					Count = newHead.Count + 1,
					Item = currentValues[index],
					NextIfAny = newHead
				};
			}
			return new Set<T>(newHead);
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

		/// <summary>
		/// Since the Set class uses uint for its index value, the standard LINQ indexed Select class requires a cast from int to uint - this version prevents that cast from
		/// being necessary
		/// </summary>
		public IEnumerable<TResult> Select<TResult>(Func<T, uint, TResult> selector)
		{
			if (selector == null)
				throw new ArgumentNullException("selector");

			uint index = 0;
			var node = _headIfAny;
			while (node != null)
			{
				yield return selector(node.Item, index);
				node = node.NextIfAny;
				index++;
			}
		}

		/// <summary>
		/// This will return a new Set of the same element type, where each item has been processed with the specified updater delegate. It is not valid for the updater to
		/// return a null reference, this data type will not store null references (if there may be missing values then the type parameter should be an Optional). If the
		/// set is empty or if the updater returns the same reference for every item then no change is required and the current Set reference will be returned unaltered.
		/// </summary>
		public Set<T> UpdateAll(Func<T, T> updater)
		{
			if (updater == null)
				throw new ArgumentNullException("updater");

			return UpdateInternal(updater, optionalFilter: null);
		}

		/// <summary>
		/// This will return a new Set of the same element type, where any item that matches the specified filter will be processed with the specified updater delegate. It
		/// is not valid for the updater to return a null reference, this data type will not store null references (if there may be missing values then the type parameter
		/// should be an Optional). If the set is empty, if the filter does not match any items or if the updater returns the same reference for every matched item then
		/// no change is required and the current Set reference will be returned unaltered.
		/// </summary>
		public Set<T> Update(Func<T, T> updater, Func<T, bool> filter)
		{
			if (updater == null)
				throw new ArgumentNullException("updater");
			if (filter == null)
				throw new ArgumentNullException("filter");

			return UpdateInternal(updater, filter);
		}

		private Set<T> UpdateInternal(Func<T, T> updater, Func<T, bool> optionalFilter)
		{
			if (updater == null)
				throw new ArgumentNullException("updater");

			if (Count == 0)
				return this;

			// Walk down the list, generating the updated values on the way
			var newValues = new T[Count];
			var node = _headIfAny;
			Tuple<Node, int> earliestUnchangedValueAndIndex = null;
			for (var i = 0; i < Count; i++)
			{
				var currentValue = node.Item;
				var needToApplyUpdateToThisValue = (optionalFilter == null) || optionalFilter(currentValue);
				if (!needToApplyUpdateToThisValue)
				{
					if (earliestUnchangedValueAndIndex == null)
						earliestUnchangedValueAndIndex = Tuple.Create(node, i);
					newValues[i] = currentValue;
				}
				else
				{
					var newValue = updater(currentValue);
					if (newValue == null)
						throw new ArgumentException("updated returned a null reference - this is not acceptable, Set<T> will not record nulls");
					var isNewValueTheSameAsCurrentValue = newValue.Equals(currentValue);
					if ((earliestUnchangedValueAndIndex == null) && isNewValueTheSameAsCurrentValue)
						earliestUnchangedValueAndIndex = Tuple.Create(node, i);
					else if (!isNewValueTheSameAsCurrentValue)
						earliestUnchangedValueAndIndex = null;
					newValues[i] = newValue;
				}
				node = node.NextIfAny;
			}

			// If we are able to persist some of the nodes, then start at that point - otherwise, we'll have to rebuild the entire list
			int startIndexOfReusableContent;
			if (earliestUnchangedValueAndIndex == null)
				startIndexOfReusableContent = (int)Count;
			else
			{
				if (earliestUnchangedValueAndIndex.Item2 == 0)
				{
					// If we're able to share the first item then we're able to share the entire list and there's no need to create a new one
					return this;
				}
				startIndexOfReusableContent = earliestUnchangedValueAndIndex.Item2;
				node = earliestUnchangedValueAndIndex.Item1;
			}

			// Now create a new list with the new values, starting before the content that may be reused (if any)
			for (var i = startIndexOfReusableContent - 1; i >= 0; i--)
			{
				node = new Node
				{
					Count = (node == null) ? 1 : (node.Count + 1),
					Item = newValues[i],
					NextIfAny = node
				};
			}
			return new Set<T>(node);
		}

		/// <summary>
		/// This will throw an exception for an invalid index value or for a null value reference
		/// </summary>
		public Set<T> RemoveAt(uint index)
		{
			if (index >= Count)
				throw new ArgumentOutOfRangeException("index");

			// Walk down the list until the node-to-be-removed is reached, tracking the values passed through on the way
			var valuesBeforeRemove = new T[index];
			var node = _headIfAny;
			for (var i = 0; i < index; i++)
			{
				valuesBeforeRemove[i] = node.Item;
				node = node.NextIfAny;
			}

			// Move to the node (if there is one) after the one that was removed
			node = node.NextIfAny;

			// Rebuilding the list from list from here, the node chain after the removal does not need to be altered
			for (var i = valuesBeforeRemove.Length - 1; i >= 0; i--)
			{
				node = new Node
				{
					Count = (node == null) ? 1 : (node.Count + 1),
					Item = valuesBeforeRemove[i],
					NextIfAny = node
				};
			}
			return new Set<T>(node);
		}

		/// <summary>
		/// This will remove any items from the set that match the specified filter. If no items were matched then the initial set will be returned unaltered.
		/// </summary>
		public Set<T> Remove(Func<T, bool> filter)
		{
			if (filter == null)
				throw new ArgumentNullException("filter");

			if (Count == 0)
				return this;

			// Walk down the list, generating the updated values on the way
			var newValues = new Optional<T>[Count];
			var node = _headIfAny;
			Tuple<Node, int> earliestUnchangedValueAndIndex = null;
			for (var i = 0; i < Count; i++)
			{
				if (filter(node.Item))
				{
					earliestUnchangedValueAndIndex = null;
					newValues[i] = Optional<T>.Missing;
				}
				else
				{
					if (earliestUnchangedValueAndIndex == null)
						earliestUnchangedValueAndIndex = Tuple.Create(node, i);
					newValues[i] = node.Item;
				}
				node = node.NextIfAny;
			}

			// If we are able to persist some of the nodes, then start at that point - otherwise, we'll have to rebuild the entire list
			int startIndexOfReusableContent;
			if (earliestUnchangedValueAndIndex == null)
				startIndexOfReusableContent = (int)Count;
			else
			{
				if (earliestUnchangedValueAndIndex.Item2 == 0)
				{
					// If we're able to share the first item then we're able to share the entire list and there's no need to create a new one
					return this;
				}
				startIndexOfReusableContent = earliestUnchangedValueAndIndex.Item2;
				node = earliestUnchangedValueAndIndex.Item1;
			}

			// Now create a new list with the new values, starting before the content that may be reused (if any)
			for (var i = startIndexOfReusableContent - 1; i >= 0; i--)
			{
				if (!newValues[i].IsDefined)
					continue;

				node = new Node
				{
					Count = (node == null) ? 1 : (node.Count + 1),
					Item = newValues[i].Value,
					NextIfAny = node
				};
			}
			return new Set<T>(node);
		}

		public static implicit operator Set<T>(NonNullList<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return new Set<T>(Script.Write<Node>("source._headIfAny"));
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

		// 2017-01-25 DWR: These attributes result in the Node class being represented by a simple object literal. This actually avoids some minor overhead but was really
		// added due to a bug introduced in Bridge 15.7.0 (http://forums.bridge.net/forum/bridge-net-pro/bugs/3356), having Node as an object literal avoids the issue
		[ObjectLiteral(ObjectCreateMode.Plain)]
		[IgnoreGeneric]
		[External]
		private sealed class Node
		{
			public int Count;
			public T Item;
			public Node NextIfAny;
		}
	}
}