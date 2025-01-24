using CdCSharp.SequentialGenerator.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System.Reflection;

namespace CdCSharp.SequentialGenerator;
public static class GeneratorExecutor
{
    public static async Task RunGenerators(string rootPath, string outputPath)
    {
        IEnumerable<string> projectFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly);
        string? projectFile = projectFiles.FirstOrDefault() ?? throw new InvalidOperationException("Project file not found");
        bool relativeToRoot = !outputPath.Contains(":");
        string finalOutputPath = relativeToRoot ? Path.Combine(rootPath, outputPath) : outputPath;

        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        Project project = await workspace.OpenProjectAsync(projectFile);
        string assemblyPath = Path.Combine(
            Path.GetDirectoryName(projectFile)!,
            project.OutputFilePath ?? throw new InvalidOperationException("Could not determine assembly path")
        );

        Compilation compilation = await project.GetCompilationAsync() ??
            throw new InvalidOperationException("Failed to get compilation");

        Assembly assembly = Assembly.LoadFrom(assemblyPath);

        // Buscar generadores secuenciales
        List<(Type Type, SequentialGeneratorAttribute? Attr)> sequentialGenerators = assembly.GetTypes()
            .Where(t => typeof(ISequentialGenerator).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<SequentialGeneratorAttribute>()))
            .Where(x => x.Attr != null)
            .OrderBy(x => x.Attr!.Order)
            .ToList();

        // Crear y ejecutar orquestador
        SequentialGeneratorOrchestrator orchestrator = new();
        foreach ((Type type, SequentialGeneratorAttribute _) in sequentialGenerators)
        {
            ISequentialGenerator generator = (ISequentialGenerator)Activator.CreateInstance(type)!;
            orchestrator.RegisterGenerator(type.Name, generator);
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(orchestrator);
        driver = driver.RunGenerators(compilation);

        foreach (GeneratedSourceResult file in driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources))
        {
            string path = Path.Combine(finalOutputPath, file.HintName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.SourceText.ToString());
        }
    }
}
