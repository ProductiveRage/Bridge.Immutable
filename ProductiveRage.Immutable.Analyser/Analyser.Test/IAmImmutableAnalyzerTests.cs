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
		public class ForClassesThatDoNotImplementIAmImmutable : IAmImmutableCallAnalyzerTester
		{
			[TestMethod]
			public void DoNotComplainAboutMutableFieldsIfTheTypeDoesNotImplementIAmImmutable()
			{
				var testContent = @"
					using Bridge;
					using System.Collections.Generics;
					using System.Linq;

					namespace TestCase
					{
						public class Test
						{
							public static int GetCount(IEnumerable<int> values)
							{
								return values.Count();
							}
						}

						public class SomethingWithAnId
						{
							public int Id;
						}
					}";
				VerifyCSharpDiagnostic(testContent);
			}

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

			[TestMethod]
			public void ExpressionBodiedMembersAreAcceptable()
			{
				var testContent = @"
					namespace TestCase
					{
						public sealed class UnhandledDocumentFocus
						{
							public static UnhandledDocumentFocus Instance => _instance;
							private static UnhandledDocumentFocus _instance = new UnhandledDocumentFocus();
							private UnhandledDocumentFocus() { }
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
			public void SettersMustBeAlwaysBeDefinedIfNotReadOnlyAutoProperty()
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

			[TestMethod]
			public void GettablePropertiesWithoutSettersAreAllowedOnExplicitInterfaceImplementations()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public interface IHaveValue
						{
							string Value { get; }
						}

						public class C1 : IAmImmutable, IHaveValue
						{
							string IHaveValue.Value { get { return ""abc""; } }
						}
					}";

				VerifyCSharpDiagnostic(testContent);
			}

			[TestMethod]
			public void ReadOnlyAutoPropertiesAreSupported()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id { get; }
						}
					}";
				VerifyCSharpDiagnostic(testContent);
			}

			/// <summary>
			/// If there's a refactor from mutable types (using fields rather than properties) then the mutable fields should be identified as invalid for
			/// an IAmImmutable implementation (there could feasibly be an argument that private mutable fields have a purpose but never public fields)
			/// </summary>
			[TestMethod]
			public void PublicMutableFieldsAreNotAllowed()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id;
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHavePublicNonReadOnlyFieldsRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			/// <summary>
			/// It doesn't make sense for a class to have a public setter if it's supposed to be immutable - clearly it's NOT immutable if data can be
			/// changed! This is an easy mistake to make if writing the properties by hand (currently, Bridge doesn't support C# 6 and so a readonly /
			/// get-only property can't be used, so the property must be written as { get; private set; } and not as { get; set; } - if the auto-
			/// populator code fix is used then this should be a non-issue!) 2016-09-21 DWR: C# 6 *is* supported now, since Bridge 15.0, so this
			/// mistake will hopefully be less common since the preferable readonly auto-property format may be used.
			/// </summary>
			[TestMethod]
			public void PublicSettersAreNotAllowed()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public int Id { get; set; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHavePublicSettersRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 8, 29)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void ConstructorShouldNotPerformValidationSinceItWillNotBeCalledByWithUpdates()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public SomethingWithAnId(int id)
							{
								// I only like positives
								if (id <= 0)
									throw new ArgumentOutOfRangeException(""id must be a positive value"");

								this.CtorSet(_ => _.Id, id);
							}

							public int Id { get; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = IAmImmutableAnalyzer.ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod.MessageFormat.ToString(),
					Severity = DiagnosticSeverity.Warning,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void IfParameterValidationIsRequiredThenItMayBePutIntoSeparateValidateMethodAndCalledAtTheEndOfTheConstructor()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public SomethingWithAnId(int id)
							{
								this.CtorSet(_ => _.Id, id);
								Validate();
							}
							private void Validate()
							{
								// I only like positives
								if (Id <= 0)
									throw new ArgumentOutOfRangeException(""Id must be a positive value"");
							}

							public int Id { get; }
						}
					}";

				VerifyCSharpDiagnostic(testContent);
			}

			[TestMethod]
			public void IfValidateMethodIsCalledFromConstructorThenItMustBeTheFinalLine()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public SomethingWithAnId(int id)
							{
								Validate();
								this.CtorSet(_ => _.Id, id);
							}

							private void Validate()
							{
								// I only like positives
								if (Id <= 0)
									throw new ArgumentOutOfRangeException(""Id must be a positive value"");
							}

							public int Id { get; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = IAmImmutableAnalyzer.ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod.MessageFormat.ToString(),
					Severity = DiagnosticSeverity.Warning,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void ValidateShouldBeCalledFromConstructorIfMethodIsPresent()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public SomethingWithAnId(int id)
							{
								this.CtorSet(_ => _.Id, id);
							}

							private void Validate()
							{
								// I only like positives
								if (Id <= 0)
									throw new ArgumentOutOfRangeException(""Id must be a positive value"");
							}

							public int Id { get; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.ConstructorDoesNotCallValidateMethod.MessageFormat.ToString(), "SomethingWithAnId"),
					Severity = DiagnosticSeverity.Warning,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			/// <summary>
			/// While it looks like it's clearly a mistake to not call Validate in this case, classes that don't implement IAmImmutable are not our concern (and it may well be acceptable
			/// for them to have a Validate method that isn't called from the constructor), so ensure that we don't interfere in that case
			/// </summary>
			[TestMethod]
			public void ValidateCallCheckShouldOnlyApplyToIAmImmutableImplementations()
			{
				var testContent = @"
					using Bridge;

					namespace TestCase
					{
						public class SomethingWithAnId
						{
							public SomethingWithAnId(int id)
							{
								Id = id;
							}

							private void Validate()
							{
								// I only like positives
								if (Id <= 0)
									throw new ArgumentOutOfRangeException(""Id must be a positive value"");
							}

							public int Id { get; }
						}
					}";

				VerifyCSharpDiagnostic(testContent);
			}

			[TestMethod]
			public void ValidateMethodMayNotHaveAnyBridgeAttributesSinceItIsPresumedToHaveKnownNameAtRuntime()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : IAmImmutable
						{
							public SomethingWithAnId(int id)
							{
								this.CtorSet(_ => _.Id, id);
								Validate();
							}

							[Name(""validator"")]
							private void Validate()
							{
								// I only like positives
								if (Id <= 0)
									throw new ArgumentOutOfRangeException(""Id must be a positive value"");
							}

							public int Id { get; }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = IAmImmutableAnalyzer.ConstructorWithLogicOtherThanCtorSetCallsShouldUseValidateMethod.MessageFormat.ToString(),
					Severity = DiagnosticSeverity.Warning,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
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
			public void SettersMustBeAlwaysBeDefinedIfNotReadOnlyAutoProperty()
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


			[TestMethod]
			public void ReadOnlyAutoPropertiesAreSupported()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id { get; }
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";
				VerifyCSharpDiagnostic(testContent);
			}

			/// <summary>
			/// If there's a refactor from mutable types (using fields rather than properties) then the mutable fields should be identified as invalid for
			/// an IAmImmutable implementation (there could feasibly be an argument that private mutable fields have a purpose but never public fields)
			/// </summary>
			[TestMethod]
			public void PublicMutableFieldsAreNotAllowed()
			{
				var testContent = @"
					using Bridge;
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id;
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHavePublicNonReadOnlyFieldsRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 9, 8)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			/// <summary>
			/// It doesn't make sense for a class to have a public setter if it's supposed to be immutable - clearly it's NOT immutable if data can be
			/// changed! This is an easy mistake to make if writing the properties by hand (currently, Bridge doesn't support C# 6 and so a readonly /
			/// get-only property can't be used, so the property must be written as { get; private set; } and not as { get; set; } - if the auto-
			/// populator code fix is used then this should be a non-issue!) 2016-09-21 DWR: C# 6 *is* supported now, since Bridge 15.0, so this
			/// mistake will hopefully be less common since the preferable readonly auto-property format may be used.
			/// </summary>
			[TestMethod]
			public void PublicSettersAreNotAllowed()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public class SomethingWithAnId : ImmutableBase
						{
							public int Id { get; set; }
						}
						public abstract class ImmutableBase : IAmImmutable { }
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MayNotHavePublicSettersRule.MessageFormat.ToString(), "Id"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 8, 29)
					}
				};

				VerifyCSharpDiagnostic(testContent, expected);
			}

			[TestMethod]
			public void ExpressionBodiedMembersAreNotAllowedSinceThereWillBeNoSetter()
			{
				var testContent = @"
					using ProductiveRage.Immutable;

					namespace TestCase
					{
						public sealed class UnhandledDocumentFocus : IAmImmutable
						{
							public static UnhandledDocumentFocus Instance => _instance;
							private static UnhandledDocumentFocus _instance = new UnhandledDocumentFocus();
							private UnhandledDocumentFocus() { }
						}
					}";

				var expected = new DiagnosticResult
				{
					Id = IAmImmutableAnalyzer.DiagnosticId,
					Message = string.Format(IAmImmutableAnalyzer.MustHaveSettersOnPropertiesWithGettersAccessRule.MessageFormat.ToString(), "Instance"),
					Severity = DiagnosticSeverity.Error,
					Locations = new[]
					{
						new DiagnosticResultLocation("Test0.cs", 8, 8)
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