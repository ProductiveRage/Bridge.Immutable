using System;

namespace ProductiveRage.Immutable
{
	/// <summary>
	/// This attribute may be used a way to pass a Property Identifier reference from one method to another (and then on to the IAmImmutable With extension method). When an argument
	/// has this attribute on it, the caller must always provide a lambda that meets the same criteria as calls to With (or CtorSet or GetProperty) - it must be a simple lambda that
	/// references a simple property on an IAmImmutable target. Method arguments that have this attribute on may not be reassigned within the method since could bypass the performed
	/// validation (and it might not be possible to ensure that the new value meets the required criteria).
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public sealed class PropertyIdentifierAttribute : Attribute { }
}