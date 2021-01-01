using System;
using System.IO;
using static Bullseye.Targets;
using static SimpleExec.Command;

internal class Program
{
    public static void Main(string[] args)
    {
        var sdk = new DotnetSdkManager();

        Target("default", DependsOn("test"));

        Target("build",
            Directory.EnumerateFiles(".", "*.sln", SearchOption.AllDirectories),
            solution => Run(sdk.GetDotnetCliPath(), $"build \"{solution}\" --configuration Release"));

        Target("test", DependsOn("build"),
            Directory.EnumerateFiles("tests", "*Tests.csproj", SearchOption.AllDirectories),
            proj => Run(sdk.GetDotnetCliPath(), $"test \"{proj}\" --configuration Release --no-build"));
        
        RunTargetsAndExit(args);
    }
}
