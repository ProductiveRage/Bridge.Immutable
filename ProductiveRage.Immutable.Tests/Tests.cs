using Bridge.Html5;
using Bridge.QUnit;
using ProductiveRage.SealedClassVerification;

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

			QUnit.Test("CtorSet may not be called outside of the constructor (only works if CtorSet is set consistently within the constructor)", assert =>
			{
				var x = new SomethingWithStringId("abc");
				assert.Throws(
					() => x.CtorSet(_ => _.Id, "abc"),
					"CtorSet should throw if called outside of the constructor (since it should only be called once per property and the constructor should call it for all properties)"
				);
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

			QUnit.Test("With does not affect original instance", assert =>
			{
				var x0 = new SomethingWithStringId("abc");
				var x1 = x0.With(_ => _.Id, "def");
				assert.Equal(x0.Id, "abc");
				assert.Equal(x1.Id, "def");
			});

			QUnit.Test("Simple string property update of property on a base class using With directly", assert =>
			{
				// This test is just to ensure that there's no monkey business involved when targeting properties on a base class (as there are
				// with interface properties - see above)
				var x = new SecurityPersonDetails(1, "test", 10);
				x = x.With(_ => _.Name, "test2");
				assert.Equal(x.Name, "test2");
			});

			/* TODO: Bridge (since 16.0.0-beta) no longer allows [ObjectLiteral] to implement an interface that isn't also [ObjectLiteral]
			QUnit.Test("Simple string property update using With directly where target is [ObjectLiteral]", assert =>
			{
				var x = new ObjectLiteralPersonDetails(1, "test");
				x = x.With(_ => _.Name, "test2");
				assert.Equal(x.Name, "test2");
			});
			*/

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

			// When first changing the Clone behaviour within ImmutabilityHelpers to work with Bridge 16 (which changes how properties are defined on objects), there was a
			// bug introduced where the updating properties on the clone would update the values on the original value too! These tests confirm that that bug is no more.
			QUnit.Test("Simple string property update against an interface using With directly", assert =>
			{
				// Inspired by issue https://github.com/ProductiveRage/Bridge.Immutable/issues/4
				IAmImmutableAndHaveName viaInterfacePerson = new PersonDetails(1, "test");
				viaInterfacePerson = viaInterfacePerson.With(_ => _.Name, "test2");
				assert.Equal(viaInterfacePerson.Name, "test2");
			});
			QUnit.Test("Double-check must-not-affect-original-instance when targeting property on base class", assert =>
			{
				var x0 = new SecurityPersonDetails(1, "test", 10);
				var x1 = x0.With(_ => _.Name, "test2");
				assert.Equal(x0.Name, "test");
				assert.Equal(x1.Name, "test2");
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

		public sealed class SecurityPersonDetails : PersonDetails
		{
			public SecurityPersonDetails(int key, string name, int securityClearance) : base(key, name)
			{
				this.CtorSet(_ => _.SecurityClearance, securityClearance);
			}
			public int SecurityClearance { get; }
		}

		[DesignedForInheritance]
		public class PersonDetails : IAmImmutableAndHaveName
		{
			public PersonDetails(int key, string name)
			{
				this.CtorSet(_ => _.Key, key);
				this.CtorSet(_ => _.Name, name);
			}
			public int Key { get; }
			public string Name { get; }
		}

		/* TODO: Bridge (since 16.0.0-beta) no longer allows [ObjectLiteral] to implement an interface that isn't also [ObjectLiteral]
		[External]
		[ObjectLiteral(ObjectCreateMode.Constructor)]
		public sealed class ObjectLiteralPersonDetails : IAmImmutable
		{
			public ObjectLiteralPersonDetails(int key, string name)
			{
				this.CtorSet(_ => _.Key, key);
				this.CtorSet(_ => _.Name, name);
			}
			[Name("key")] // TODO: Need to specify this until this is addressed: https://forums.bridge.net/forum/bridge-net-pro/bugs/4203
			public int Key { get; }
			[Name("name")] // TODO: Need to specify this until this is addressed: https://forums.bridge.net/forum/bridge-net-pro/bugs/4203
			public string Name { get; }
		}
		*/

		public interface IAmImmutableAndHaveName : IAmImmutable
		{
			string Name { get; }
		}
	}
}
