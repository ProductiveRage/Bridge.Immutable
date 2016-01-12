﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	[TestClass]
	public class WithCallAnalyzerTests : DiagnosticVerifier
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
					new DiagnosticResultLocation("Test0.cs", 10, 22)
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
					new DiagnosticResultLocation("Test0.cs", 10, 22)
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
					new DiagnosticResultLocation("Test0.cs", 10, 22)
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
					new DiagnosticResultLocation("Test0.cs", 10, 22)
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
					new DiagnosticResultLocation("Test0.cs", 11, 22)
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
					new DiagnosticResultLocation("Test0.cs", 11, 22)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		/// <summary>
		/// It is acceptable for classes to have gettable properties that don't have a setter if those properties are part of an explicit interface implementation.
		/// So long as the properties are not valid for use with With or CtorSet, then this won't be a problem - the property would only be accessible for use in
		/// a With or CtorSet call if the propertyRetriever target was cast to that particular interface and that is not allowed (the target in a property retriever
		/// must be a very simple access, no recasts are allowed - see PropertyTargetMustNotBeManipulatedOrReCast).
		/// </summary>
		[TestMethod]
		public void GettablePropertiesWithoutSettersAreAllowedOnExplicitInterfaceImplementations()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public interface IHaveAnId
					{
						int Id { get; }
					}

					public class Something : IAmImmutable, IHaveAnId
					{
						private readonly int _id;
						public Something(int id, string name)
						{
							_id = id;
							this.CtorSet(_ => _.Name, name);
						}
						int IHaveAnId.Id { get { return _id; } }
						public string Name { get; private set; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// The target of the propertyRetriever must be a simple property access (such as "_ => _.Name"), it is not acceptable for an indirect or recast target
		/// to be used (such as "_ => ((ISomethingElse)_).Name"). This assumption is what allows for classes that implement IAmImmutable to also implement other
		/// interfaces, which have get-only properties, as long as those base interfaces are explicitly implemented (see GettablePropertiesWithoutSettersAre-
		/// AllowedOnExplicitInterfaceImplementations).
		/// </summary>
		[TestMethod]
		public void PropertyTargetMustNotBeManipulatedOrReCast()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static Something WithId(Something x, int id)
						{
							return x.With(_ => ((IHaveAnIdThatIsMutable)_).Id, id);
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
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.IndirectTargetAccessorAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 22)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new WithCallAnalyzer();
		}
	}
}