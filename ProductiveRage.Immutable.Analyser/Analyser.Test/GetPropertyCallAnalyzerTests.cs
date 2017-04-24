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
		public void LegacyIdealUsage()
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
						public int Id { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void IdealUsageForSingleArgumentMethodOverload()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<SomethingWithAnId, int> Test()
						{
							return ImmutabilityHelpers.GetProperty<SomethingWithAnId, int>(_ => _.Id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; }
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

		/// <summary>
		/// This tests the fix for Issue 6, which showed that the TPropertyValue type parameter could be a type that was less specific that the property - which would mean
		/// that the With call would set the target property to a type that it shouldn't be possible for it to be (for example, it would allow a string property to be set
		/// to an instance of an object that wasn't a string).
		/// </summary>
		[TestMethod]
		public void TPropertyValueMayNotBeAncestorOfPropertyType()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						static Main()
						{
							var x = new SomethingWithAnId(""abc""));
							var propertyRetriever = x.GetProperty<SomethingWithAnId, object>(_ => _.Id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(string id) { }
						public string Id { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = GetPropertyCallAnalyzer.DiagnosticId,
				Message = string.Format(
					GetPropertyCallAnalyzer.PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule.MessageFormat.ToString(),
					"string",
					"Object"
				),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 32)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		/// <summary>
		/// This is a companion to TPropertyValueMayNotBeAncestorOfPropertyType that illustrates that TPropertyValue does not have to be the precise same type as the
		/// target property - it may be a MORE specific type (but it may not be a LESS specific type)
		/// </summary>
		[TestMethod]
		public void TPropertyValueMayBeSpecialisationOfPropertyType()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						static Main()
						{
							var x = new SomethingWithAnId(""abc""));
							var propertyRetriever = x.GetProperty<SomethingWithAnId, string>(_ => _.Id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(object id) { }
						public object Id { get; private set; }
					}
				}";
			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// It would be a bit pointless to use one PropertyIdentifier to create another (because they would have the same generic types and so perform the exact same property retrieval)
		/// but this test confirms that the analysers understand that a PropertyIdentifier may be passed in as the lambda to the GetProperty method (just as it may be passed into the
		/// CtorSet and With methods)
		/// </summary>
		[TestMethod]
		public void CanUseOnePropertyIdentifierToGenerateAnother()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x, PropertyIdentifier propertyIdentifier)
						{
							return x.GetProperty(propertyIdentifier);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void CanUsePropertyIdentifierAttributeIdentifiedArgumentToGeneratePropertyGetter()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static PropertyIdentifier<SomethingWithAnId, int> Test(SomethingWithAnId x, [PropertyIdentifier] Func<SomethingWithAnId, int> propertyIdentifier)
						{
							return x.GetProperty(propertyIdentifier);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id) { }
						public int Id { get; }
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