using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class DedicatedServerBuild
{
    public static void BuildLinux()
    {
        var outputPath = Path.GetFullPath(
            GetRequiredArgument("-serverBuildPath"));

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new BuildFailedException(
                "No enabled scenes were found in Editor Build Settings");

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new BuildFailedException(
                $"Invalid server output path: {outputPath}");

        Directory.CreateDirectory(outputDirectory);

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.StrictMode
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Dedicated server build failed: {report.summary.result}");
        }

        if (!File.Exists(outputPath))
        {
            throw new BuildFailedException(
                $"Dedicated server executable was not created: {outputPath}");
        }
    }

    private static string GetRequiredArgument(string argumentName)
    {
        var arguments = Environment.GetCommandLineArgs();

        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(
                    arguments[index],
                    argumentName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        throw new BuildFailedException(
            $"Missing required command-line argument: {argumentName}");
    }
}