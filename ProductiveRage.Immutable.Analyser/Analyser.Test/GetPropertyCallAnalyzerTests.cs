using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	[TestClass]
	public class GetPropertyCallAnalyzerTests : DiagnosticVerifier
	{
		[TestMethod]
		public void IdealUsage()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return x.GetProperty(_ => _.Id);
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return ImmutabilityHelpers.GetProperty(x, _ => _.Id);
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							var id = 123;
							return x.GetProperty(_ => id);
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 29)
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x, int id)
						{
							return x.GetProperty(_ => _.GetHashCode());
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 29)
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return x.GetProperty(_ => _.Id + 1);
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 29)
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return x.GetProperty(_ => _.Id);
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 29)
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return x.GetProperty(_ => _.Id);
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 29)
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
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x)
						{
							return x.GetProperty(_ => _.Id);
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
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 29)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyTargetMustNotBeManipulatedOrReCast()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<Something, int> Test(Something x)
						{
							return x.GetProperty(_ => ((IHaveAnIdThatIsMutable)_).Id);
						}
					}

					public interface IHaveAnIdThatIsMutable
					{
						int Id { get; set; }
					}

					public class Something : IHaveAnIdThatIsMutable, IAmImmutable
					{
						public Something(int id) { }
						int IHaveAnIdThatIsMutable.Id { get; set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = GetPropertyCallAnalyzer.IndirectTargetAccessorAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 29)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void UpdateViaPropertyRetrieverReference()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x, PropertyIdentifier<SomethingWithAnId, int> propertyIdentifier)
						{
							return x.With(propertyIdentifier, id);
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

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new GetPropertyCallAnalyzer();
		}
	}
}