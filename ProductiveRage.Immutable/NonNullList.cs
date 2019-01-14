using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bridge;

namespace ProductiveRage.Immutable
{
	public static class NonNullList
	{
		/// <summary>
		/// This will throw an exception for any null references in the values parameters - if nulls may be required then the type parameter should be an Optional
		/// </summary>
		public static NonNullList<T> Of<T>(params T[] values)
		{
			return (values == null) ? NonNullList<T>.Empty : new NonNullList<T>(values);
		}
	}

	public sealed class NonNullList<T> : IEnumerable<T>
	{
		private readonly static NonNullList<T> _empty = new NonNullList<T>((Node)null);
		public static NonNullList<T> Empty { get { return _empty; } }

		private readonly Node _headIfAny;
		private NonNullList(Node headIfAny)
		{
			_headIfAny = headIfAny;
		}
		public NonNullList(IEnumerable<T> values) // This was only added for Bridge.Newtonsoft.Json but it probably doesn't hurt to make it available for general use
		{
			Node node = null;
			foreach (var value in values.Reverse())
			{
				node = new Node
				{
					Count = ((node == null) ? 0 : node.Count) + 1,
					Item = value,
					NextIfAny = node
				};
			}
			_headIfAny = node;
		}
		// Making this a uint prevents having to have a summary comment explaining that it will always be zero or greater
		public uint Count { get { return (_headIfAny == null) ? 0 : (uint)_headIfAny.Count; } }

		/// <summary>
		/// Due to the internal structure of this class, this is the cheapest way to add an item to a set. Null references are not allowed (an exception will be thrown),
		/// if you require values that may be null then the type parameter should be an Optional.
		/// </summary>
		public NonNullList<T> Insert(T item)
		{
			if (item == null)
				throw new ArgumentNullException("item");

			if (_headIfAny == null)
				return new NonNullList<T>(new Node { Count = 1, Item = item, NextIfAny = null });

			return new NonNullList<T>(new Node
			{
				Count = _headIfAny.Count + 1,
				Item = item,
				NextIfAny = _headIfAny
			});
		}

		public NonNullList<T> Insert(NonNullList<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));

			if (other.Count == 0)
				return this;

			var otherItems = new T[other.Count];
			var otherNode = other._headIfAny;
			var otherIndex = 0;
			while (otherNode != null)
			{
				otherItems[otherIndex] = otherNode.Item;
				otherNode = otherNode.NextIfAny;
				otherIndex++;
			}

			var newHeadIfAny = _headIfAny;
			for (var i = 0; i < other.Count; i++) // Can't use a decrement-by-one loop because other.Count is a uint so we need to increment and then..
			{
				newHeadIfAny = new Node
				{
					Count = ((newHeadIfAny == null) ? 0 : newHeadIfAny.Count) + 1,
					Item = otherItems[(other.Count - 1) - i], // .. do some arithmetic here
					NextIfAny = newHeadIfAny
				};
			}
			return new NonNullList<T>(newHeadIfAny);
		}

		/// <summary>
		/// Due to the internal structure of this class, this is a more expensive operation that Insert (which inserts a new item at the start of the set, rather than at
		/// the end, which this function does).  Null references are not allowed (an exception will be thrown), if you require values that may be null then the type parameter
		/// should be an Optional.
		/// </summary>
		public NonNullList<T> Add(T item)
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
			return new NonNullList<T>(newHead);
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
		public NonNullList<T> SetValue(uint index, T value)
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
			return new NonNullList<T>(newNode);
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
		/// This will return a new NonNullList of the same element type, where each item has been processed with the specified updater delegate. It is not valid for the updater
		/// to return a null reference, this data type will not store null references (if there may be missing values then the type parameter should be an Optional). If the
		/// set is empty or if the updater returns the same reference for every item then no change is required and the current Set reference will be returned unaltered.
		/// </summary>
		public NonNullList<T> UpdateAll(Func<T, T> updater)
		{
			if (updater == null)
				throw new ArgumentNullException("updater");

			return UpdateInternal(updater, optionalFilter: null);
		}

		/// <summary>
		/// This will return a new NonNullList of the same element type, where any item that matches the specified filter will be processed with the specified updater delegate.
		/// It is not valid for the updater to return a null reference, this data type will not store null references (if there may be missing values then the type parameter
		/// should be an Optional). If the set is empty, if the filter does not match any items or if the updater returns the same reference for every matched item then
		/// no change is required and the current Set reference will be returned unaltered.
		/// </summary>
		public NonNullList<T> Update(Func<T, T> updater, Func<T, bool> filter)
		{
			if (updater == null)
				throw new ArgumentNullException("updater");
			if (filter == null)
				throw new ArgumentNullException("filter");

			return UpdateInternal(updater, filter);
		}

		private NonNullList<T> UpdateInternal(Func<T, T> updater, Func<T, bool> optionalFilter)
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
						throw new ArgumentException("updated returned a null reference - this is not acceptable, NonNullList<T> will not record nulls");
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
			return new NonNullList<T>(node);
		}

		/// <summary>
		/// This will return a new NonNullList of different element type, where each item has processed with the specified mapper delegate. It is not valid for the mapper
		/// to return a null reference, this data type will not store null references (if there may be missing values then the type parameter should be an Optional). If
		/// none of the items are changed and TDest matches the item type of the current list then the current list will be returned unaltered.
		/// </summary>
		public NonNullList<TDest> Map<TDest>(Func<T, TDest> mapper)
		{
			if (mapper == null)
				throw new ArgumentNullException(nameof(mapper));

			return Map((item, index) => mapper(item));
		}

		/// <summary>
		/// This will return a new NonNullList of different element type, where each item has processed with the specified mapper delegate. It is not valid for the mapper
		/// to return a null reference, this data type will not store null references (if there may be missing values then the type parameter should be an Optional). If
		/// none of the items are changed and TDest matches the item type of the current list then the current list will be returned unaltered.
		/// </summary>
		public NonNullList<TDest> Map<TDest>(Func<T, uint, TDest> mapper)
		{
			if (mapper == null)
				throw new ArgumentNullException(nameof(mapper));

			if (_headIfAny == null)
				return NonNullList<TDest>.Empty;

			uint index = 0;
			var changedAnyValues = false;
			NonNullList<TDest>.Node newHeadIfAny = null;
			NonNullList<TDest>.Node previousNewNodeIfAny = null;
			var node = _headIfAny;
			while (node != null)
			{
				var value = mapper(node.Item, index);
				if (value == null)
					throw new ArgumentException($"Specified {mapper} returned null value - invalid");
				if (!value.Equals(node.Item))
					changedAnyValues = true;
				var newNode = new NonNullList<TDest>.Node
				{
					Count = node.Count,
					Item = value
				};
				if (newHeadIfAny == null)
					newHeadIfAny = newNode;
				if (previousNewNodeIfAny != null)
					previousNewNodeIfAny.NextIfAny = newNode;
				previousNewNodeIfAny = newNode;
				node = node.NextIfAny;
				index++;
			}
			if (!changedAnyValues && (typeof(TDest) == typeof(T)))
				return (NonNullList<TDest>)(object)this;
			return new NonNullList<TDest>(newHeadIfAny);
		}

		/// <summary>
		/// This will throw an exception for an invalid index value or for a null value reference
		/// </summary>
		public NonNullList<T> RemoveAt(uint index)
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
			return new NonNullList<T>(node);
		}

		/// <summary>
		/// This will remove any items from the set that match the specified filter. If no items were matched then the initial set will be returned unaltered.
		/// </summary>
		public NonNullList<T> Remove(Func<T, bool> filter)
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
			return new NonNullList<T>(node);
		}

		public NonNullList<T> OrderBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey> optionalComparer = null)
		{
			if (keySelector == null)
				throw new ArgumentNullException(nameof(keySelector));

			// If there are zero or one items then there's no ordering that can possibly be needed!
			if (Count < 2)
				return this;

			// We'll create an enumerable of Node instances that have the Item properties set for the current data (but not Count of NextIfAny because they will be wrong after the
			// data is ordered anyway) and we'll order this using OrderBy to give a new enumerable set of these same Node instances. Then we need to wrap wrap these instances up
			// into a linked list and set their Count and NextIfAny properties as we go. Then we create a new list with that new linked list and we're done.
			Node newHead = null;
			Node previous = null;
			var count = _headIfAny.Count; // We know that _headIfAny is not null because we ensured that Count was 2 or greater earlier
			foreach (var newNode in EnumerateClonedNodeWrappedItems().OrderBy(node => keySelector(node.Item), optionalComparer))
			{
				if (newHead == null)
					newHead = newNode;
				else
					previous.NextIfAny = newNode;
				newNode.Count = count;
				previous = newNode;
				count--;
			}
			return new NonNullList<T>(newHead);
		}

		public NonNullList<T> OrderByDescending<TKey>(Func<T, TKey> keySelector, IComparer<TKey> optionalComparer = null)
		{
			if (keySelector == null)
				throw new ArgumentNullException(nameof(keySelector));

			return OrderBy(keySelector, new ReversedComparer<TKey>(optionalComparer ?? Comparer<TKey>.Default));
		}

		/// <summary>
		/// This returns an enumerable of Node instances where the Item property is set but not the Count or NextIfAny (this is only useful within the OrderBy implementation, which
		/// will set those properties on the cloned Nodes after rearranging them - ordinarily )
		/// </summary>
		private IEnumerable<Node> EnumerateClonedNodeWrappedItems()
		{
			var node = _headIfAny;
			while (node != null)
			{
				yield return new Node { Item = node.Item };
				node = node.NextIfAny;
			}
		}

#pragma warning disable CS0618 // Ignore the fact that Set is obsolete, this implicit cast will only be supported until the Set class is finally removed from the library
		public static implicit operator NonNullList<T>(Set<T> source)
#pragma warning restore CS0618 // Type or member is obsolete
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return new NonNullList<T>(Script.Write<Node>("source._headIfAny"));
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

		private sealed class ReversedComparer<TKey> : IComparer<TKey>
		{
			private readonly IComparer<TKey> _comparer;
			public ReversedComparer(IComparer<TKey> comparer)
			{
				if (comparer == null)
					throw new ArgumentNullException(nameof(comparer));
				_comparer = comparer;
			}
			public int Compare(TKey x, TKey y)
			{
				return _comparer.Compare(y, x);
			}
		}
	}
}
