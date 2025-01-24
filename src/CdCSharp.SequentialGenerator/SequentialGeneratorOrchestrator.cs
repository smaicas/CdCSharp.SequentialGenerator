using CdCSharp.SequentialGenerator.Abstractions;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using GeneratorExecutionContext = CdCSharp.SequentialGenerator.Abstractions.GeneratorExecutionContext;

namespace CdCSharp.SequentialGenerator;

[Generator]
public class SequentialGeneratorOrchestrator : IIncrementalGenerator
{
    private readonly List<(string Name, ISequentialGenerator Generator, int Order)> _generators = [];

    public void RegisterGenerator(string name, ISequentialGenerator generator, int order = 0) => _generators.Add((name, generator, order));

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Configure providers
        List<(string Name, IncrementalValuesProvider<INamedTypeSymbol> Provider)> providersWithNames = _generators
            .OrderBy(g => g.Order)
            .Select(g => (g.Name, Provider: g.Generator.ConfigureProvider(context)))
            .ToList();

        // Combine providers
        IncrementalValueProvider<GeneratorCompilationContext> combinedProviders = CombineProviders(context.CompilationProvider, providersWithNames);

        // Register sequential execution
        context.RegisterSourceOutput(combinedProviders, ExecuteGeneratorsSequentially);
    }

    private IncrementalValueProvider<GeneratorCompilationContext> CombineProviders(
        IncrementalValueProvider<Compilation> compilationProvider,
        List<(string Name, IncrementalValuesProvider<INamedTypeSymbol> Provider)> providers)
    {
        IncrementalValueProvider<GeneratorCompilationContext> initialValue = compilationProvider.Select((c, _) =>
            new GeneratorCompilationContext(c));

        return providers.Aggregate(initialValue,
            (current, provider) => current.Combine(provider.Provider.Collect())
                .Select((tuple, _) => tuple.Left.AddClasses(provider.Name, tuple.Right)));
    }

    private void ExecuteGeneratorsSequentially(SourceProductionContext sourceContext, GeneratorCompilationContext context)
    {
        Compilation currentCompilation = context.Compilation;

        foreach ((string name, ISequentialGenerator generator, int _) in _generators.OrderBy(g => g.Order))
        {
            if (!context.Classes.TryGetValue(name, out ImmutableArray<INamedTypeSymbol> classes))
                continue;

            GeneratorExecutionContext executionContext = new(
                currentCompilation,
                classes,
                sourceContext);

            generator.Execute(executionContext);
            currentCompilation = executionContext.Compilation;
        }
    }
}
