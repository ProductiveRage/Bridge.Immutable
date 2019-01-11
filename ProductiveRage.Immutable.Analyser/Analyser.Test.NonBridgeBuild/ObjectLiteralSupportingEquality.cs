namespace ProductiveRage.Immutable
{
	/// <summary>
	/// This implementation varies from the Bridge one because the GetMethod method signatures supported by Bridge are different to .NET and the easiest
	/// thing to do is to trim out that logic from this 'non-BridgeBuild'
	/// </summary>
	public static class ObjectLiteralSupportingEquality
	{
		public static bool AreEqual(object x, object y)
		{
			if ((x == null) && (y == null))
				return true;
			else if ((x == null) || (y == null))
				return false;
			return x.Equals(y);
		}
	}
}