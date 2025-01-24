
using Microsoft.CodeAnalysis;

namespace CdCSharp.SequentialGenerator.Abstractions;
public interface ISequentialGenerator
{
    IncrementalValuesProvider<INamedTypeSymbol> ConfigureProvider(IncrementalGeneratorInitializationContext context);
    void Execute(GeneratorExecutionContext context);
}

