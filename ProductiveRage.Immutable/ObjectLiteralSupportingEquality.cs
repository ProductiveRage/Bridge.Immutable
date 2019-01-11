using System;
using System.Reflection;
using Bridge;

namespace ProductiveRage.Immutable
{
	internal static class ObjectLiteralSupportingEquality
	{
		/// <summary>
		/// If two instances of an [ObjectLiteral] type that has an overridden Equals method are compared by Bridge then the custom Equals method is not always used. This may
		/// not be fixed by Bridge since there is some debate around what sort of 'full C# compatibility' that [ObjectLiteral] types should have (as discussed in the forum:
		/// https://forums.bridge.net/forum/community/help/6001). For now, I'm going to perform the equality checks required by this library using this method because it
		/// will handle [ObjectLiteral] types (so long as metadata for the assembly is emitted by Bridge, which I think is the common case).
		/// </summary>
		public static bool AreEqual(object x, object y)
		{
			if ((x == null) && (y == null))
				return true;
			else if ((x == null) || (y == null))
				return false;

			var type = Script.Write<Type>("Bridge.getType({0});", x);
			if (Script.Write<bool>("type.$literal === true"))
			{
				var equalsMethodInfo = type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(object) });
				if (equalsMethodInfo != null)
				{
					var javaScriptEqualsMethodName = Script.Write<string>("{0}.sn", equalsMethodInfo);
					if (Script.Write<bool>("javaScriptEqualsMethodName"))
					{
						/*@
						var equalsMethod = type.prototype[javaScriptEqualsMethodName];
						if (equalsMethod)
						{
							return equalsMethod.apply(x, [y]);
						}
						*/
					}
				}
			}

			return x.Equals(y);
		}
	}
}