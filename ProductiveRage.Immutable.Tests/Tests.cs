using Bridge.Html5;
using Bridge.QUnit;

namespace ProductiveRage.Immutable.Tests
{
	public static class Tests
	{
		[Ready]
		public static void Go()
		{
			OptionalTests();
			CtorSetTests();
			WithTests();
		}

		private static void OptionalTests()
		{
			QUnit.Module("OptionalTests");

			QUnit.Test("Optional.Map from one string to another", assert =>
			{
				var x = Optional.For("abc");
				x = x.Map(_ => _ + _);
				assert.Equal(x.IsDefined, true);
				assert.Equal(x.Value, "abcabc");
			});

			// I don't expect this to be a common thing to do but it makes more sense to allow a null result from a mapper to result in a Missing
			// response than for it to throw its toys out the pram
			QUnit.Test("Optional.Map from one string to null (should return Missing)", assert =>
			{
				var x = Optional.For("abc");
				x = x.Map<string>(_ => null);
				assert.Equal(x.IsDefined, false);
			});

			QUnit.Test("Optional.Map from one string to itself (should return same instance back)", assert =>
			{
				var x = Optional.For("abc");
				var updatedX = x.Map(_ => _);
				assert.StrictEqual(updatedX, x);
			});
		}

		private static void CtorSetTests()
		{
			QUnit.Module("CtorSetTests");

			QUnit.Test("Simple string property CtorSet initialisation", assert =>
			{
				var x = new SomethingWithStringId("abc");
				assert.Equal(x.Id, "abc");
			});
		}

		private static void WithTests()
		{
			QUnit.Module("WithTests");

			QUnit.Test("Simple string property update using With directly", assert =>
			{
				var x = new SomethingWithStringId("abc");
				x = x.With(_ => _.Id, "def");
				assert.Equal(x.Id, "def");
			});

			QUnit.Test("Simple string property update using With indirectly", assert =>
			{
				var x = new SomethingWithStringId("abc");
				var idUpdater = x.With(_ => _.Id);
				x = idUpdater("def");
				assert.Equal(x.Id, "def");
			});

			QUnit.Test("Simple string property update using GetProperty and With", assert =>
			{
				var x = new SomethingWithStringId("abc");
				var propertyToUpdate = x.GetProperty(_ => _.Id);
				x = x.With(propertyToUpdate, "def");
				assert.Equal(x.Id, "def");
			});

			QUnit.Test("Single-element NonNullList<string> property update using With directly", assert =>
			{
				var x = new SomethingWithNonNullListStringValues(NonNullList.Of("abc", "def"));
				x = x.With(_ => _.Values, 1, "xyz");
				assert.Equal(x.Values[1], "xyz");
			});

			QUnit.Test("Single-element NonNullList<string> property update using GetProperty and With", assert =>
			{
				var x = new SomethingWithNonNullListStringValues(NonNullList.Of("abc", "def"));
				var propertyToUpdate = x.GetProperty(_ => _.Values);
				x = x.With<SomethingWithNonNullListStringValues, string>(propertyToUpdate, 1, "xyz");
				assert.Equal(x.Values[1], "xyz");
			});

			QUnit.Test("Single-element Set<string> (legacy compatibility alias for NonNullList) property update using GetProperty and With", assert =>
			{
#pragma warning disable CS0618 // Ignore the fact that Set is obsolete
				var x = new SomethingWithNonNullListStringValues(Set.Of("abc", "def"));
#pragma warning restore CS0618
				var propertyToUpdate = x.GetProperty(_ => _.Values);
				x = x.With<SomethingWithNonNullListStringValues, string>(propertyToUpdate, 1, "xyz");
				assert.Equal(x.Values[1], "xyz");
			});
		}

		public sealed class SomethingWithStringId : IAmImmutable
		{
			public SomethingWithStringId(string id)
			{
				this.CtorSet(_ => _.Id, id);
			}
			public string Id { get; }
		}

		public sealed class SomethingWithNonNullListStringValues : IAmImmutable
		{
			public SomethingWithNonNullListStringValues(NonNullList<string> values)
			{
				this.CtorSet(_ => _.Values, values);
			}
			public NonNullList<string> Values { get; }
		}
	}
}
