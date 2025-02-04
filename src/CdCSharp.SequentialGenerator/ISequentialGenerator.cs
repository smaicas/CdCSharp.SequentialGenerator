using CdCSharp.SequentialGenerator;
using Microsoft.CodeAnalysis;
/// <summary>
/// Interface for a generator that can be executed as part of a sequence
/// </summary>
public interface ISequentialGenerator
{
    string Name { get; }
    IncrementalValuesProvider<INamedTypeSymbol> ConfigureProvider(IncrementalGeneratorInitializationContext context);
    void Execute(SequentialGeneratorExecutionContext context);
}
