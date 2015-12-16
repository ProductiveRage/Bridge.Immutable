using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	[TestClass]
	public class WithCallAnalyzerTests : CodeFixVerifier
	{
		[TestMethod]
		public void BlankContent()
		{
			VerifyCSharpDiagnostic("");
		}

		[TestMethod]
		public void IdealUsage()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.Id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; private set; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// The CtorSet method should only be called as an extension method (and always target "this") but it's not compulsory for the With method to do the same
		/// </summary>
		[TestMethod]
		public void AcceptableToCallDirectlyIfDesired()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return ImmutabilityHelpers.With(x, _ => _.Id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; private set; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void PropertyIdentifierReturnsVariableInsteadOfProperty()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyIdentifierReturnsMethodInsteadOfProperty()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.GetHashCode(), id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyIdentifierReturnsManipulatedInsteadOfSimpleProperty()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.Id + 1, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyWithoutSetter()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.Id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get { return 123; } }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyWithGetterWithBridgeAttribute()
		{
			var testContent = @"
				using Bridge;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.Id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { [Name(""getSpecialId"")] get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyWithSetterWithBridgeAttribute()
		{
			var testContent = @"
				using Bridge;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							return x.With(_ => _.Id, id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; [Name(""setSpecialId"")] private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 15)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}


		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new NullCodeFixProvider();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new WithCallAnalyzer();
		}
	}
}