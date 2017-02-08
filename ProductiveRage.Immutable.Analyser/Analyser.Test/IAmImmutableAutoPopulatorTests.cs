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
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.Rule.MessageFormat.ToString(), "EmployeeDetails"),
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
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.Rule.MessageFormat.ToString(), "EmployeeDetails"),
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
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.Rule.MessageFormat.ToString(), "EmployeeDetails"),
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
						public ManagerDetails(int id, string name, Set<EmployeeDetails> reports) : base(id, name)
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
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.Rule.MessageFormat.ToString(), "ManagerDetails"),
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
						public ManagerDetails(int id, string name, Set<EmployeeDetails> reports) : base(id, name)
						{
							this.CtorSet(_ => _.Reports, reports);
						}

						public Set<EmployeeDetails> Reports { get; }
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
				Message = string.Format(IAmImmutableAutoPopulatorAnalyzer.Rule.MessageFormat.ToString(), "EmployeeDetails"),
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