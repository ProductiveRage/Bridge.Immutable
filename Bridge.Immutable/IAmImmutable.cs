namespace Bridge.Immutable
{
	/// <summary>
	/// The CtorSet extension method should only be used with types that are intended for its use so it will only operate against classes the implement this interface. The
	/// interface itself is empty, it is just to identify the for-use-with-CtorSet types (and the only thing to bear in mind with that is that properties should all be set
	/// for the instance within the constructor so that they can not later be altered externally, with CtorSet would allow for properties that had NOT been set in a ctor).
	/// </summary>
	[Priority(1)] // Note: This is required, otherwise the ordering of dependencies goes awry (see http://forums.bridge.net/forum/bridge-net-pro/bugs/1204)
	public interface IAmImmutable { }
}
