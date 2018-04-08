using System;

namespace ProductiveRage.Immutable
{
	/// <summary>
	/// This may be applied to a property on an IAmImmutable implementation to indicate that it may not be updated via the 'With' function. It does not make sense on types that do not
	/// implement IAmImmutable because 'With' does not apply to those. This may be used to allow properties on IAmImmutable implementations which are computed, rather than being simple
	/// auto-properties (which is what is otherwise required) or it may be useful if a type should only be initialised in a particular and some properties should not be changed after
	/// being first created (in which case it would make sense for its constructors to be private and for static factory methods to be defined to ensure that instances are created
	/// to follow whatever patterns are required). Properties annotated with this attribute must still follow all of the other IAmImmutable property rules because it will still
	/// be settable by 'CtorSet' calls.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class ReadOnlyAttribute : Attribute { }
}