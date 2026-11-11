using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace CurioDistrict.Tests.Integration.Content;

/// <summary>Invokes the production CLI as a separate process, including its file and exit-code boundary.</summary>
public sealed class ContentValidatorProcessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Valid_content_exits_zero_and_reports_four_pools()
    {
        var (code, text) = await Run(ProfilePath());
        Assert.Equal(0, code);
        Assert.Contains("PASS", text);
        Assert.Contains("pools=4", text);
    }

    [Fact]
    public async Task Multiple_semantic_defects_exit_one_and_all_appear_deterministically()
    {
        using var fixture = new TempContent();
        var doc = JsonNode.Parse(await File.ReadAllTextAsync(ProfilePath()))!;
        var pool = doc["lootPools"]![0]!;
        pool["rewards"]![0]!["weight"] = 9999;
        pool["sourceSkuId"] = "missing-sku-process";
        var extra = doc["itemDefinitions"]![0]!.DeepClone();
        extra["definitionId"] = doc["products"]![0]!["skuId"]!.GetValue<string>();
        ((JsonArray)doc["itemDefinitions"]!).Add(extra);
        var file = fixture.Write("invalid.json", doc.ToJsonString());
        var first = await Run("--file", file);
        var second = await Run(file);
        Assert.Equal(1, first.Code);
        Assert.Equal(first, second);
        Assert.Contains("[DuplicateId]", first.Text);
        Assert.Contains("[PoolWeight]", first.Text);
        Assert.Contains("[Reference]", first.Text);
        Assert.Contains("missing-sku-process", first.Text);
    }

    [Fact]
    public async Task Unreadable_and_malformed_files_do_not_hide_other_failures()
    {
        using var fixture = new TempContent();
        var malformed = fixture.Write("malformed.json", "{");
        var semantic = fixture.Write("semantic.json", """{"itemDefinitions":[{"definitionId":"x","references":["missing-item"]}]}""");
        var missing = Path.Combine(fixture.Root, "absent.json");
        var result = await Run(ProfilePath(), malformed, missing, semantic);
        Assert.Equal(1, result.Code);
        Assert.Contains("[Input]", result.Text);
        Assert.Contains("[Parse]", result.Text);
        Assert.Contains("[Reference]", result.Text);
        Assert.Contains("missing-item", result.Text);
    }

    [Theory]
    [InlineData()]
    [InlineData("--file")]
    [InlineData("--unknown")]
    public async Task Invalid_usage_exits_two(params string[] args)
    {
        var result = await Run(args);
        Assert.Equal(2, result.Code);
    }

    private async Task<(int Code, string Text)> Run(params string[] args)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(BackendRoot(), "Tools", "ContentValidator", "bin", configuration, "net10.0", "ContentValidator.dll");
        Assert.True(File.Exists(dll), "The matching configuration must be built before process tests: " + dll);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(dll);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        var text = await stdout + await stderr;
        output.WriteLine($"CLI={dll}; child PID={process.Id}; native exit={process.ExitCode}\n{text}");
        return (process.ExitCode, text);
    }

    private static string ProfilePath() => Path.Combine(BackendRoot(), "Config", "CurioDistrict.Config", "profiles", "dev-seed.v0.1.json");

    private static string BackendRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CurioDistrict.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not locate backend root from integration test assembly.");
    }

    private sealed class TempContent : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "curio-content009-" + Guid.NewGuid().ToString("N"));
        internal TempContent() => Directory.CreateDirectory(Root);
        internal string Write(string name, string content)
        {
            var path = Path.Combine(Root, name);
            File.WriteAllText(path, content);
            return path;
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
