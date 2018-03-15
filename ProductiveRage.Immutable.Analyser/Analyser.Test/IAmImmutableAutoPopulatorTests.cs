using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	[TestClass]
	public class IAmImmutableAutoPopulatorTests : CodeFixVerifier
	{
		[TestMethod]
		public void BlankContent()
		{
			VerifyCSharpDiagnostic("");
		}

		[TestMethod]
		public void AutoPopulateEmptyClassThatHasNoBaseClass()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
						}
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "EmployeeDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		/// <summary>
		/// The CtorSet extension methods only work if there is the ProductiveRage.Immutable namespace is pulled in through a using directive, if
		/// it's not been then a using directive will be added (this could be the case if the IAmImmutable was referenced by its full name or if
		/// a class is derived from a base class in a different file that implements IAmImmutable - in which case, the current file may no have
		/// the using directive)
		/// </summary>
		[TestMethod]
		public void UsingDirectiveWillBeAddedIfNotAlreadyPresent()
		{
			var testContent = @"
				namespace TestCase
				{
					public class EmployeeDetails : ProductiveRage.Immutable.IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
						}
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "EmployeeDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 6, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : ProductiveRage.Immutable.IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			// Without setting allowNewCompilerDiagnostics to true, the CodeFixVerifier will throw an exception that "ProductiveRage.Immutable" is not a valid
			// namespace (and then complain that "CtorSet" is not available and that perhaps a using directive is missing). I'm not entirely sure why setting
			// it to true bypasses the check that the code fix doesn't introduce any new issues (I would have thought that false would make more sense - as
			// in "no, don't run diagnostics on the code fix output") but it does.
			VerifyCSharpFix(
				GetStringForCodeFixComparison(testContent),
				GetStringForCodeFixComparison(fixContent),
				allowNewCompilerDiagnostics: true
			);
		}

		/// <summary>
		/// The most commonly expected use case is for an empty constructor to be present in a class that has no properties defined (since the properties can
		/// be auto-populated) but, if any properties ARE already present, then duplicate properties should not be injected by the code fix
		/// </summary>
		[TestMethod]
		public void DoNotInjectPropertiesThatAreAlreadyPresent()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
						}

						public int Id { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "EmployeeDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; private set; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		[TestMethod]
		public void DoNotConsiderConstructorArgumentsPassedToBaseConstructor()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class ManagerDetails : EmployeeDetails
					{
						public ManagerDetails(int id, string name, NonNullList<EmployeeDetails> reports) : base(id, name)
						{
						}
					}

					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "ManagerDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class ManagerDetails : EmployeeDetails
					{
						public ManagerDetails(int id, string name, NonNullList<EmployeeDetails> reports) : base(id, name)
						{
							this.CtorSet(_ => _.Reports, reports);
						}

						public NonNullList<EmployeeDetails> Reports { get; }
					}

					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; private set; }
						public string Name { get; private set; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		/// <summary>
		/// If there's a parameterless Validate method then call that at the end of the auto-populated constructor. A static method wouldn't make sense (because it wouldn't be able to
		/// perform validation for the current instance) but the JavaScript in the With method can't tell if the method (if present) is static or not, so the auto-populator doesn't
		/// differentiate between static or instance Validate methods. If it's on a base class (if there is one) then it's the base class' responsibility to call it from its constructor.
		/// If it has arguments then we can't call it because the Validate method that we're supporting will validate the current instance as it is configured, for which no arguments
		/// should be required. This doesn't support partial classes (where the constructor is in one file and the Validate method in another) - in order to deal with that case we would
		/// have to do deeper analysis.
		/// </summary>
		[TestMethod]
		public void IfApplicableValidateMethodIsDefinedThenItShouldBeCalledAtConstructorEndWhenAutoPopulating()
		{
			var testContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
						}
						private void Validate()
						{
							if (string.IsNullOrWhiteSpace(Name))
								throw new ArgumentNullException($""Null/blank {nameof(Name)} specified"");
						}
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "EmployeeDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 9, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using System;
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
							Validate();
						}
						private void Validate()
						{
							if (string.IsNullOrWhiteSpace(Name))
								throw new ArgumentNullException($""Null/blank {nameof(Name)} specified"");
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		/// <summary>
		/// The analyser and code fix can be run/applied to invalid code, such as a constructor argument list having a missing entry - in this case, don't throw an exception (because
		/// code fixes should never throw), instead just ignore the missing entry when auto-populating the rest of the class
		/// </summary>
		[TestMethod]
		public void IgnoreMissingConstructorArgumentsInsteadOfThrowing()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, , string name)
						{
						}
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.EmptyConstructorRule.MessageFormat.ToString(), "EmployeeDetails"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, , string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		[TestMethod]
		public void DoNotApplyToNonIAmImmutableClasses()
		{
			var testContent = @"
				namespace TestCase
				{
					public class EmployeeDetails
					{
						public EmployeeDetails(int id, string name)
						{
						}
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void DoNotConsiderIncompleteContent()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void NewConstructorArgumentNeedsToBePropagated()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
						}

						public int Id { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.OutOfSyncConstructorRule.MessageFormat.ToString(), "EmployeeDetails", "name"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		[TestMethod]
		public void NewConstructorArgumentExistsAsPropertyButCtorSetCallIsMissing()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.OutOfSyncConstructorRule.MessageFormat.ToString(), "EmployeeDetails", "name"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		[TestMethod]
		public void NewConstructorArgumentNeedsToBePropagatedAndValidateCallMustBeAdded()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
						}

						private void Validate()
						{
						}

						public int Id { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.OutOfSyncConstructorRule.MessageFormat.ToString(), "EmployeeDetails", "name"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
							Validate();
						}

						private void Validate()
						{
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		[TestMethod]
		public void NewConstructorArgumentNeedsToBePropagatedAndExistingValidateCallMustBeMoved()
		{
			// Just for fun, there is some whitespace and a comment before the Validate call ("trivia" in Roslyn terms) that should still be with the Validate call after
			// the autopopulator has done its work
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);

							// Validate!
							Validate();
						}

						private void Validate()
						{
						}

						public int Id { get; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = IAmImmutableAutoPopulatorAnalyzer.DiagnosticId,
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.OutOfSyncConstructorRule.MessageFormat.ToString(), "EmployeeDetails", "name"),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 8, 7)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);

			var fixContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : IAmImmutable
					{
						public EmployeeDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);

							// Validate!
							Validate();
						}

						private void Validate()
						{
						}

						public int Id { get; }
						public string Name { get; }
					}
				}";

			VerifyCSharpFix(GetStringForCodeFixComparison(testContent), GetStringForCodeFixComparison(fixContent));
		}

		/// <summary>
		/// The analyser realised that constructor arguments were being used if they were passed directly into the base constructor but if they were passed into a function whose
		/// return value was passed into the base constructor then the analyser didn't realise this and complained that the constructor argument was not being used
		/// </summary>
		[TestMethod]
		public void DoNotFalselyIdentifyConstructorArgumentThatIsPassedToBaseConstructorOtherThanDirectlyAsAnArgument()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class EmployeeDetails : SomethingWithKey, IAmImmutable
					{
						public EmployeeDetails(int id, string key) : base(Something(key))
						{
							this.CtorSet(_ => _.Id, id);
						}

						public int Id { get; }

						private static string Something(string value) { return value; }
					}

					public class SomethingWithKey
					{
						public SomethingWithKey(string key)
						{
							Key = key;
						}

						public string Name { get; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		/// <summary>
		/// When the code fix adds lines, it uses spaces instead of tabs (which I use in the files here) and so it's easiest to just replace tabs with runs of four spaces before
		/// making comparisons between before and after values. The strings in this file are also indented so that they appear "within" the containing method, rather than being
		/// aligned to the zero column in the editor, but this offset will not respected by lines added by the code fix - so it's just easiest to remove the offset from each
		/// line before comparing.
		/// </summary>
		private static string GetStringForCodeFixComparison(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			const string _contentWhitespaceOffset = "				";
			var whitespaceAdjustedLines = value
				.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
				.Select(line => line.StartsWith(_contentWhitespaceOffset) ? line.Substring(_contentWhitespaceOffset.Length) : line)
				.Select(line => line.Replace("\t", "    "));
			return string.Join(Environment.NewLine, whitespaceAdjustedLines);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new IAmImmutableAutoPopulatorAnalyzer();
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new IAmImmutableAutoPopulatorCodeFixProvider();
		}
	}
}