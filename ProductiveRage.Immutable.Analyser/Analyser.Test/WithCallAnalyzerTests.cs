using Microsoft.CodeAnalysis;
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

		[TestMethod]
		public void UpdateViaPropertyRetrieverReference()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, int id)
						{
							var propertyIdentifier = x.GetProperty(_ => _.Id);
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

		[TestMethod]
		public void UpdateViaPropertyIdentifierAttributeMethodArgument()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static SomethingWithAnId WithId(SomethingWithAnId x, [PropertyIdentifier] Func<SomethingWithAnId, int> propertyIdentifier, int id)
						{
							return x.With(propertyIdentifier, id);
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
		/// If an anonymous method is passed as an argument where the receiving method is expecting a User-defined delegate and the delegate specifies the PropetyIdentifier attribute on a parameter
		/// then everything should work the same as if a specific method was called that had the attribute on the parameter
		/// </summary>
		[TestMethod]
		public void WithCallMayHaveTargetSpecifiedByDelegateArgumentIfArgumentIsAnnotatedWithPropertyIdentifierAttribute()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static void Test()
						{
							var x = new SomethingWithAnId(123);
							UpdateProperty(property => x.With(property, 456));
						}

						private static void UpdateProperty(MyDelegate property)
						{
							property(_ => _.Id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id)
						{
							this.CtorSet(_ => _.Id, id);
						}
						public int Id { get; }
					}

					public delegate void MyDelegate([PropertyIdentifier] Func<SomethingWithAnId, int> propertyIdentifier);
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// A variation on WithCallMayHaveTargetSpecifiedByDelegateArgumentIfArgumentIsAnnotatedWithPropertyIdentifierAttribute where the delegate has multiple parameters (though only one of them
		/// has the PropertyIdentifier attribute on it)
		/// </summary>
		[TestMethod]
		public void WithCallMayHaveTargetSpecifiedByDelegateArgumentIfArgumentIsAnnotatedWithPropertyIdentifierAttribute_DelegateHasMultipleArgument()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static void Test()
						{
							var x = new SomethingWithAnId(123);
							UpdateProperty((property, something) => x.With(property, 456));
						}

						private static void UpdateProperty(MyDelegate property)
						{
							property(_ => _.Id, ""abc"");
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id)
						{
							this.CtorSet(_ => _.Id, id);
						}
						public int Id { get; }
					}

					public delegate void MyDelegate([PropertyIdentifier] Func<SomethingWithAnId, int> propertyIdentifier, string something);
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// A counterpart to WithCallMayHaveTargetSpecifiedByDelegateArgumentIfArgumentIsAnnotatedWithPropertyIdentifierAttribute that ensures that the changes to support the PropertyIdentifier attribute
		/// on User-defined delegate has not accidentally relaxed the restrictions on delegate parameters without the attribute
		/// </summary>
		[TestMethod]
		public void WithCallMayNotHaveTargetSpecifiedByDelegateArgumentIfArgumentIsNotAnnotatedWithPropertyIdentifierAttribute()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public static class Program
					{
						public static void Test()
						{
							var x = new SomethingWithAnId(123);
							UpdateProperty(property => x.With(property, 456));
						}

						private static void UpdateProperty(MyDelegate property)
						{
							property(_ => _.Id);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(int id)
						{
							this.CtorSet(_ => _.Id, id);
						}
						public int Id { get; }
					}

					public delegate void MyDelegate(Func<SomethingWithAnId, int> propertyIdentifier);
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.MethodParameterWithoutPropertyIdentifierAttributeRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 12, 42)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
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
							x = x.With(_ => _.Id, new object());
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
				Id = WithCallAnalyzer.DiagnosticId,
				Message = string.Format(
					WithCallAnalyzer.PropertyMayNotBeSetToInstanceOfLessSpecificTypeRule.MessageFormat.ToString(),
					"string",
					"Object"
				),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 12)
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
							x = x.With(_ => _.Id, ""def"");
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
		/// It was originally intended that With calls would have inline lambdas tha specify the property but this was a bit limiting in places so the GetProperty method
		/// was added which would generate a property-retriever instance but even this got a bit cumbersome in places and so now there is support for a delegate to be passed
		/// to a function that may be used as a property retriever but it must be annotated with the [PropertyIdentifier] attribute so that we can ensure that it is always
		/// provided with an acceptable value. If a delegate method argument is used but the attribute is forgotten then the generic error about a property-accessing lambda
		/// message is shown but it would be better to say a bit more and suggest that it's just a case of a forgotten [PropertyIdentifier] attribute and not that it's not
		/// possible to pass a property identifier delegate as a method parameter.
		/// </summary>
		[TestMethod]
		public void HelpfulMessageShouldBeDisplayedIfPropertyIdentifierAttributeIsForgotten()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class Example
					{
						public SomethingWithAnId UpdateId(SomethingWithAnId target, Func<SomethingWithAnId, string> propertyIdentifier, string newId)
						{
							return target.With(propertyIdentifier, newId);
						}
					}

					public class SomethingWithAnId : IAmImmutable
					{
						public SomethingWithAnId(string id)
						{
							this.CtorSet(_ => _.Id, id);
						}
						public string Id { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = WithCallAnalyzer.DiagnosticId,
				Message = WithCallAnalyzer.MethodParameterWithoutPropertyIdentifierAttributeRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 27)
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