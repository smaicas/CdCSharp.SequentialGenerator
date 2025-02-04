using System.Runtime.CompilerServices;

namespace CdCSharp.SequentialGenerator.SnapshotTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Force Assembly initialization (!! important !!)
        Type b = typeof(ISequentialGenerator);
        Type c = typeof(SequentialGeneratorBase);
        VerifySourceGenerators.Initialize();
    }
}