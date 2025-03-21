using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using FluentAssertions;

namespace Community.PowerToys.Run.Plugin.Templates.Tests;

[Category("Integration")]
public class VerifyTests
{
    [SetUp]
    public void Setup()
    {
        var template = @"..\..\..\..\src\templates\";
        Delete("hive");
        RunProcess(@$"dotnet new install {template} --debug:custom-hive hive");
    }

    [Test]
    public Task ptrun_sln()
    {
        var output = "MySolution";
        Delete(output);
        var result = RunProcess(@$"dotnet new ptrun-sln -o {output} --debug:custom-hive hive");

        result.ExitCode.Should().Be(0);
        Directory.Exists(output).Should().BeTrue("Output directory was not created.");
        return VerifyDirectory(output,
            fileScrubber: (path, builder) =>
            {
                if (Path.GetFileName(path) == "MySolution.sln")
                {
                    Replace(builder, @"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}", "{00000000-0000-0000-0000-000000000000}");
                }
                if (Path.GetFileName(path) == "Main.cs")
                {
                    Replace(builder, "PluginID => \".*\";", "PluginID => \"00000000000000000000000000000000\";");
                }
                if (Path.GetFileName(path) == "plugin.json")
                {
                    Replace(builder, "\"ID\": \".*\",", "\"ID\": \"00000000000000000000000000000000\",");
                }
            });
    }

    [Test]
    public Task ptrun_proj()
    {
        var output = "MyProject";
        Delete(output);
        var result = RunProcess(@$"dotnet new ptrun-proj -o {output} --debug:custom-hive hive");

        result.ExitCode.Should().Be(0);
        Directory.Exists(output).Should().BeTrue("Output directory was not created.");
        return VerifyDirectory(output,
            fileScrubber: (path, builder) =>
            {
                if (Path.GetFileName(path) == "Main.cs")
                {
                    Replace(builder, "PluginID => \".*\";", "PluginID => \"00000000000000000000000000000000\";");
                }
                if (Path.GetFileName(path) == "plugin.json")
                {
                    Replace(builder, "\"ID\": \".*\",", "\"ID\": \"00000000000000000000000000000000\",");
                }
            });
    }

    [Test]
    public Task ptrun_scripts()
    {
        var output = "MyScripts";
        Delete(output);
        var result = RunProcess(@$"dotnet new ptrun-scripts -o {output} --debug:custom-hive hive");

        result.ExitCode.Should().Be(0);
        Directory.Exists(output).Should().BeTrue("Output directory was not created.");
        return VerifyDirectory(output);
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static ProcessResult RunProcess(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private record ProcessResult(int ExitCode, string Output, string Error);

    private void Replace(StringBuilder builder, string pattern, string replacement)
    {
        var result = Regex.Replace(builder.ToString(), pattern, replacement);
        builder.Clear();
        builder.Append(result);
    }
}
