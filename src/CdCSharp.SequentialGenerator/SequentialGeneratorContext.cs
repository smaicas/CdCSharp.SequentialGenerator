using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace CdCSharp.SequentialGenerator;
/// <summary>
/// Context shared between sequential generators
/// </summary>
public class SequentialGeneratorContext
{
    private readonly List<SyntaxTree> _generatedTrees = [];
    private readonly List<ISequentialGenerator> _pendingGenerators = [];

    public SequentialGeneratorContext(Compilation initialCompilation) => CurrentCompilation = initialCompilation;

    public Compilation CurrentCompilation { get; private set; }

    public void AddGeneratedCode(string fileName, string sourceCode)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            SourceText.From(sourceCode, Encoding.UTF8),
            path: fileName);

        _generatedTrees.Add(syntaxTree);
        CurrentCompilation = CurrentCompilation.AddSyntaxTrees(syntaxTree);
    }

    public void RegisterGenerator(ISequentialGenerator generator) =>
        _pendingGenerators.Add(generator);

    public IReadOnlyList<ISequentialGenerator> GetPendingGenerators() =>
        _pendingGenerators;

    public IReadOnlyList<SyntaxTree> GetGeneratedTrees() =>
        _generatedTrees;

    internal void ClearGeneratedTrees() =>
        _generatedTrees.Clear();
}