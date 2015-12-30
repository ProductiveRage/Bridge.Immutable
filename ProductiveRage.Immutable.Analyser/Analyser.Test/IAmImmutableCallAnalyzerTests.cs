using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	public static class IAmImmutableCallAnalyzerTestsGroup
	{
		[TestClass]
		public class Common : IAmImmutableCallAnalyzerTester
		{

			[TestMethod]
			public void BlankContent()
			{
				VerifyCSharpDiagnostic("");
			}
		}

		/// <summary>
		/// These tests are just to verify that the analysers ONLY target classes that implement IAmImmutable, the rules shouldn't be applied to other types
		/// </summary>
		[TestClass]
		public class ForClassesThatDoNotImplementingIAmImmutable : IAmImmutableCallAnalyzerTester
		{
			[TestMethod]
			public void BridgeNameAttributeIsAllowedOnGetters()
			{
				var testContent = @"
					using Bridge;

					namespace TestCase
					{
						public class SomethingWithAnId
						{
							public int Id { [Name(""getSpecialId"")] get; private set; }
						}
					}";
				VerifyCSharpDiagnostic(testContent);
			}

			[TestMethod]
			public void SettersAreOptional()
			{
				var testContent = @"
					namespace TestCase
					{
						public class SomethingWithAnId
						{
							public int Id { get { return 123; } }
						}
					}";
				VerifyCSharpDiagnostic(testContent);
			}

			[TestMethod]
			public void BridgeNameAttributeIsAllowedOnSetters()
			{
				var testContent = @"
					using Bridge;

					namespace TestCase
					{
						public class SomethingWithAnId
						{
							public int Id { get; [Name(""setSpecialId"")] private set; }
						}
					}";
				VerifyCSharpDiagnostic(testContent);
			}
		}

		[TestClass]
		public class ForClassesDirectlyImplementingIAmImmutable : IAmImmutableCallAnalyzerTester
		{
			[TestMethod]
			public void GetterMayNotHaveBridgeNameAttribute()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id { [Name(""getSpecialId"")] get; private set; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 24)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void SettersMustBeAlwaysBeDefined()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id { get { return 123; } }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MustHaveSettersOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 8, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void SetterMayNotHaveBridgeNameAttribute()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id { get; [Name(""setSpecialId"")] private set; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 29)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}
		}

		[TestClass]
		public class ForClassesIndirectlyImplementingIAmImmutable : IAmImmutableCallAnalyzerTester
		{
			[TestMethod]
			public void GetterMayNotHaveBridgeNameAttribute()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id { [Name(""getSpecialId"")] get; private set; }
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 24)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void SettersMustBeAlwaysBeDefined()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id { get { return 123; } }
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MustHaveSettersOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 8, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void SetterMayNotHaveBridgeNameAttribute()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id { get; [Name(""setSpecialId"")] private set; }
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHaveBridgeAttributesOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 29)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}
		}

		public abstract class IAmImmutableCallAnalyzerTester : DiagnosticVerifier
		{
			protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
			{
				return new IAmImmutableAnalyzer();
			}
		}
	}
}