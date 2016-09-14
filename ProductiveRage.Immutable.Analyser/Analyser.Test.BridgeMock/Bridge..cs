using System;
using System.Text.RegularExpressions;

namespace Bridge
{
	public class ImmutableAttribute : Attribute { }
	public class IgnoreGenericAttribute : Attribute { }
	public class NameAttribute : Attribute
	{
		public NameAttribute(string name) { }
	}
	public class PriorityAttribute : Attribute
	{
		public PriorityAttribute(int priority) { }
	}

	public static class Script
	{
		public static T Write<T>(string script) { throw new NotImplementedException(); }
		public static void Write(string script, params object[] args) { throw new NotImplementedException(); }
	}

	public static class Text
	{
		public static class RegularExpressions
		{
			public class Regex
			{
				public Regex(string pattern) { throw new NotImplementedException(); }
				public string[] Exec(string value) { throw new NotImplementedException(); }
			}
		}
	}

	public static class BridgeExtensions
	{
		public static string JsSubstring(this object source, int start, int end) { throw new NotImplementedException(); }
		public static string Exec(this Regex source, string pattern) { throw new NotImplementedException(); }
		public static string Replace(this string source, Text.RegularExpressions.Regex matcher, string value) { throw new NotImplementedException(); }
	}
}