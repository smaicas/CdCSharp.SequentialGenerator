using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace CdCSharp.SequentialGenerator;
/// <summary>
/// Orchestrator that manages the sequential execution of generators
/// </summary>

[Generator]
public abstract class SequentialGeneratorBase : IIncrementalGenerator
{
    private readonly List<ISequentialGenerator> _generators = [];

    protected void RegisterGenerator(ISequentialGenerator generator) =>
        _generators.Add(generator);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Configure providers
        List<(string Name, IncrementalValuesProvider<INamedTypeSymbol> Provider)> providersWithNames = _generators
            .Select(g => (g.Name, Provider: g.ConfigureProvider(context)))
            .ToList();

        // Combine providers
        IncrementalValueProvider<SequentialGeneratorCompilationContext> combinedProviders = CombineProviders(context.CompilationProvider, providersWithNames);

        // Register sequential execution
        context.RegisterSourceOutput(combinedProviders, ExecuteGeneratorsSequentially);
    }

    private IncrementalValueProvider<SequentialGeneratorCompilationContext> CombineProviders(
       IncrementalValueProvider<Compilation> compilationProvider,
       List<(string Name, IncrementalValuesProvider<INamedTypeSymbol> Provider)> providers)
    {
        IncrementalValueProvider<SequentialGeneratorCompilationContext> initialValue = compilationProvider.Select((c, _) =>
            new SequentialGeneratorCompilationContext(c));

        return providers.Aggregate(initialValue,
            (current, provider) => current.Combine(provider.Provider.Collect())
                .Select((tuple, _) => tuple.Left.AddClasses(provider.Name, tuple.Right)));
    }

    private void ExecuteGeneratorsSequentially(SourceProductionContext sourceContext, SequentialGeneratorCompilationContext context)
    {
        Compilation currentCompilation = context.Compilation;

        foreach (ISequentialGenerator generator in _generators)
        {
            if (!context.Classes.TryGetValue(generator.Name, out ImmutableArray<INamedTypeSymbol> classes))
                continue;

            SequentialGeneratorExecutionContext executionContext = new(
                currentCompilation,
                classes,
                sourceContext);

            // Execute generator
            generator.Execute(executionContext);

            // Update compilation for next generator
            currentCompilation = executionContext.Compilation;
        }
    }
}

public class SequentialGeneratorCompilationContext
{
    public Compilation Compilation { get; }
    public Dictionary<string, ImmutableArray<INamedTypeSymbol>> Classes { get; }

    public SequentialGeneratorCompilationContext(Compilation compilation)
    {
        Compilation = compilation;
        Classes = [];
    }

    public SequentialGeneratorCompilationContext AddClasses(string generatorName, ImmutableArray<INamedTypeSymbol> classes)
    {
        Classes[generatorName] = classes;
        return this;
    }
}

public class SequentialGeneratorExecutionContext
{
    public Compilation Compilation { get; private set; }
    public ImmutableArray<INamedTypeSymbol> Classes { get; }
    public SourceProductionContext SourceContext { get; }
    private readonly List<(string FileName, SourceText Source)> _generatedSources = [];

    public SequentialGeneratorExecutionContext(
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
