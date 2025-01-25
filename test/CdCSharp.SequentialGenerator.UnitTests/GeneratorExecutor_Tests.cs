using CdCSharp.SequentialGenerator.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Text;
using GeneratorExecutionContext = CdCSharp.SequentialGenerator.Abstractions.GeneratorExecutionContext;

namespace CdCSharp.SequentialGenerator.UnitTests;

public class GeneratorExecutor_Tests
{
    [Fact]
    public async Task RunGenerators_ShouldGenerateFiles()
    {
        // Arrange
        string projectDir = GetProjectDirectory();
        string outputDir = Path.Combine(projectDir, "Generated");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        // Act
        await GeneratorExecutor.RunGenerators(projectDir, outputDir);

        // Assert
        Assert.True(Directory.Exists(outputDir));
        string[] generatedFiles = Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories);
        Assert.NotEmpty(generatedFiles);
    }

    private static string GetProjectDirectory()
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string binDir = Path.GetDirectoryName(assemblyLocation)!;
        string projectDir = Directory.GetParent(binDir)!.Parent!.Parent!.FullName;
        return projectDir;
    }
}

[SequentialGenerator(0)]
public class FirstTestGenerator : ISequentialGenerator
{
    public IncrementalValuesProvider<INamedTypeSymbol> ConfigureProvider(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax,
                (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(symbol => symbol != null &&
                  symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "FirstTest"))!;
    }

    public void Execute(GeneratorExecutionContext context)
    {
        context.AddSource(
            "FirstGenerated.cs",
            SourceText.From(@"namespace Generated;
            
                public class FirstTestClass 
                { 
                    public string Value => ""First"";
                }
            ", Encoding.UTF8));
    }
}

[SequentialGenerator(1)]
public class SecondTestGenerator : ISequentialGenerator
{
    public IncrementalValuesProvider<INamedTypeSymbol> ConfigureProvider(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax,
                (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(symbol => symbol != null &&
                  symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "SecondTest"))!;
    }

    public void Execute(GeneratorExecutionContext context)
    {
        context.AddSource(
            "SecondGenerated.cs",
            SourceText.From(@"namespace Generated;
            
                public class SecondTestClass 
                { 
                    public string Value => ""Second"";
                }
            ", Encoding.UTF8));
    }
}