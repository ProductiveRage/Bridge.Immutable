using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ProductiveRage.Immutable.Analyser.Test
{
	[TestClass]
	public class CtorSetCallAnalyzerTests : CodeFixVerifier
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
				using System;

				namespace TestCase
				{
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, NameDetails name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; private set; }
						public NameDetails Name { get; private set; }
					}
				}";

			VerifyCSharpDiagnostic(testContent);
		}

		[TestMethod]
		public void TargetIsNotThis()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class Test
					{
						public void Go(PersonDetails p)
						{
							p.CtorSet(_ => _.Id, 123);
						}
					}

					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
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
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.SimpleMemberAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void NotCalledFromWithinConstructor()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; private set; }
						public string Name { get; private set; }
						public void Rename(string name)
						{
							this.CtorSet(_ => _.Name, name);
						}
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.ConstructorRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 17, 8)
				}
			};

			VerifyCSharpDiagnostic(testContent, expected);
		}

		[TestMethod]
		public void PropertyIdentifierReturnsVariableInsteadOfProperty()
		{
			var testContent = @"
				using ProductiveRage.Immutable;

				namespace TestCase
				{
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 8)
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
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.ToString(), name);
						}
						public int Id { get; private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 8)
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
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => Id + 1, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 8)
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
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; private set; }
						public string Name { get { return ""; } }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.SimplePropertyAccessorArgumentAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 11, 8)
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
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { [Name(""getSpecialId"")] get; private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 14, 46)
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
					public class PersonDetails : IAmImmutable
					{
						public PersonDetails(int id, string name)
						{
							this.CtorSet(_ => _.Id, id);
							this.CtorSet(_ => _.Name, name);
						}
						public int Id { get; [Name(""setSpecialId"")] private set; }
						public string Name { get; private set; }
					}
				}";

			var expected = new DiagnosticResult
			{
				Id = CtorSetCallAnalyzer.DiagnosticId,
				Message = CtorSetCallAnalyzer.BridgeAttributeAccessRule.MessageFormat.ToString(),
				Severity = DiagnosticSeverity.Warning,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 14, 59)
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
			return new CtorSetCallAnalyzer();
		}
	}
}