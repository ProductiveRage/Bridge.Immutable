using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace ProductiveRage.Immutable.Analyser
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCodeFixProvider)), Shared]
	public class NullCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds { get { return ImmutableArray<string>.Empty; } }

		public sealed override FixAllProvider GetFixAllProvider() { return WellKnownFixAllProviders.BatchFixer; }

		public sealed override Task RegisterCodeFixesAsync(CodeFixContext context) { return new Task(() => { }); }
	}
}