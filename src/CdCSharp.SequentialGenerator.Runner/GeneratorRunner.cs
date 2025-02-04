using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics;
using System.Reflection;

namespace CdCSharp.SequentialGenerator;
public static class GeneratorRunner
{
    public static async Task RunGenerators(string rootPath, string outputPath)
    {
        IEnumerable<string> projectFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly);
        string? projectFile = projectFiles.FirstOrDefault() ?? throw new InvalidOperationException("Project file not found");
        bool relativeToRoot = !outputPath.Contains(":");
        string finalOutputPath = relativeToRoot ? Path.Combine(rootPath, outputPath) : outputPath;

        Process? restoreProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });
        await restoreProcess!.WaitForExitAsync();
        if (restoreProcess.ExitCode != 0)
        {
            throw new InvalidOperationException("Failed to restore NuGet packages.");
        }

        Dictionary<string, string> properties = new()
       {
           { "LoadMetadataForReferencedProjects", "true" },
           { "BuildingProject", "true" },
           { "DesignTimeBuild", "true" },
           { "UseRazorSourceGenerator", "true" }
       };

        MSBuildWorkspace workspace = MSBuildWorkspace.Create(properties);
        Project project = await workspace.OpenProjectAsync(projectFile);
        string assemblyPath = project.OutputFilePath ?? throw new InvalidOperationException("Could not determine assembly path");

        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException($"Assembly not found at: {assemblyPath}");
        }

        List<string> assemblyPaths = GetAssemblyPaths(project).ToList();
        List<Type> allTypes = [];

        Compilation compilation = await project.GetCompilationAsync() ??
            throw new InvalidOperationException("Failed to get compilation");

        Dictionary<string, Assembly> assemblies = [];
        MetadataLoadContext loadContext = new(new PathAssemblyResolver(assemblyPaths));

        foreach (string path in assemblyPaths)
        {
            try
            {
                assemblies[path] = loadContext.LoadFromAssemblyPath(path);
                allTypes.AddRange(assemblies[path].GetTypes());
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            catch (ReflectionTypeLoadException ex)
            {
                allTypes.AddRange(ex.Types.Where(t => t != null)!);
            }
            catch
            {
                continue;
            }
        }

        List<Type> imps = allTypes.Where(t => t.IsInstanceOfType(typeof(SequentialGeneratorBase))).ToList();

        if (imps.Count <= 0) { return; }

        foreach (Type @Type in imps)
        {
            try
            {
                // Intento 1: Cargar el tipo desde el assembly del runtime
                Type? runtimeType = Type.GetType(@Type.AssemblyQualifiedName!);

                // Intento 2: Si falla, cargar desde Assembly.Load
                if (runtimeType == null)
                {
                    AssemblyName asmName = new(@Type.Assembly.GetName().Name!);
                    Assembly asm = Assembly.LoadFile(@Type.Assembly.Location);
                    runtimeType = asm.GetType(@Type.FullName!);
                }

                if (runtimeType != null)
                {
                    SequentialGeneratorBase generator = (SequentialGeneratorBase)Activator.CreateInstance(runtimeType)!;

                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
                    driver = driver.RunGenerators(compilation);

                    foreach (GeneratedSourceResult file in driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources))
                    {
                        string path = Path.Combine(finalOutputPath, file.HintName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllText(path, file.SourceText.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load type {Type.FullName}: {ex.Message}");
                continue;
            }
        }
    }

    private static IEnumerable<string> GetAssemblyPaths(Project project)
    {
        HashSet<string> assemblies = [];
        string? mainAssemblyPath = project.OutputFilePath;
        if (!string.IsNullOrEmpty(mainAssemblyPath))
        {
            assemblies.Add(mainAssemblyPath);
        }

        foreach (MetadataReference reference in project.MetadataReferences)
        {
            if (reference is PortableExecutableReference peReference)
            {
                assemblies.Add(peReference.FilePath);
            }
        }

        foreach (ProjectReference projectReference in project.AllProjectReferences)
        {
            Project? referencedProject = project.Solution.GetProject(projectReference.ProjectId);
            if (referencedProject != null)
            {
                string? outputPath = referencedProject.OutputFilePath;
                if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                {
                    assemblies.Add(outputPath);
                }
            }
        }
        return assemblies;
    }
}