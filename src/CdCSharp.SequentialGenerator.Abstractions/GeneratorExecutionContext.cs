using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace CdCSharp.SequentialGenerator.Abstractions;
public class GeneratorExecutionContext
{
    public Compilation Compilation { get; private set; }
    public ImmutableArray<INamedTypeSymbol> Classes { get; }
    public SourceProductionContext SourceContext { get; }
    private readonly List<(string FileName, SourceText Source)> _generatedSources = [];

    public GeneratorExecutionContext(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> classes,
        SourceProductionContext sourceContext)
    {
        Compilation = compilation;
        Classes = classes;
        SourceContext = sourceContext;
    }

    public void AddSource(string fileName, SourceText sourceText)
    {
        _generatedSources.Add((fileName, sourceText));

        // Add to compilation immediately
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName);
        Compilation = Compilation.AddSyntaxTrees(syntaxTree);

        // Add to source context immediately
        SourceContext.AddSource(fileName, sourceText);
    }

    public void ReportDiagnostic(Diagnostic diagnostic) => SourceContext.ReportDiagnostic(diagnostic);

    public CancellationToken CancellationToken => SourceContext.CancellationToken;

    public IReadOnlyList<(string FileName, SourceText Source)> GeneratedSources => _generatedSources;
}
