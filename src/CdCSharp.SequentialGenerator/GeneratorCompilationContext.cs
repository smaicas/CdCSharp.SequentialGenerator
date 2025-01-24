using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace CdCSharp.SequentialGenerator;
public class GeneratorCompilationContext
{
    public Compilation Compilation { get; }
    public Dictionary<string, ImmutableArray<INamedTypeSymbol>> Classes { get; }

    public GeneratorCompilationContext(Compilation compilation)
    {
        Compilation = compilation;
        Classes = [];
    }

    public GeneratorCompilationContext AddClasses(string generatorName, ImmutableArray<INamedTypeSymbol> classes)
    {
        Classes[generatorName] = classes;
        return this;
    }
}
