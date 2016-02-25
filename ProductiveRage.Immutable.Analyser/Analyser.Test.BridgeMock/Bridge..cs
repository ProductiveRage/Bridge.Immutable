using System;
using System.Text.RegularExpressions;

namespace Bridge
{
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
	}

	public static class BridgeExtensions
	{
		public static string GetClassName(this object source) { throw new NotImplementedException(); }
		public static string JsSubstring(this object source, int start, int end) { throw new NotImplementedException(); }
		public static string Exec(this Regex source, string pattern) { throw new NotImplementedException(); }
		public static string Replace(this string source, Regex matcher, string value) { throw new NotImplementedException(); }
	}
}