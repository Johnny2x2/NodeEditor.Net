using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Plugins.Marketplace;

namespace NodeEditor.Blazor.Tests;

public sealed class LocalPluginRepositoryServiceTests
{
    [Fact]
    public async Task AddPackageAsync_SavesZipAndReturnsMetadata()
    {
        var repoDir = CreateTempDir();

        try
        {
            var service = CreateService(repoDir);
            await using var package = CreatePluginZip(
                pluginId: "com.acme.echo",
                pluginName: "Echo Plugin",
                version: "1.0.0",
                entryPath: "Echo/plugin.json");

            var result = await service.AddPackageAsync(package, "echo.zip");

            Assert.True(result.Success);
            Assert.Equal("com.acme.echo", result.PluginId);
            Assert.Equal("Echo Plugin", result.PluginName);
            Assert.Equal("1.0.0", result.Version);
            Assert.False(string.IsNullOrWhiteSpace(result.StoredPath));
            Assert.True(File.Exists(result.StoredPath!));
        }
        finally
        {
            TryDeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task AddPackageAsync_ReplacesExistingPluginIdEntry()
    {
        var repoDir = CreateTempDir();

        try
        {
            var service = CreateService(repoDir);

            await using (var first = CreatePluginZip(
                pluginId: "com.acme.shared",
                pluginName: "Shared Plugin",
                version: "1.0.0",
                entryPath: "plugin.json"))
            {
                var firstResult = await service.AddPackageAsync(first, "shared-1.zip");
                Assert.True(firstResult.Success);
            }

            await using (var second = CreatePluginZip(
                pluginId: "com.acme.shared",
                pluginName: "Shared Plugin",
                version: "2.0.0",
                entryPath: "v2/plugin.json"))
            {
                var secondResult = await service.AddPackageAsync(second, "shared-2.zip");
                Assert.True(secondResult.Success);
                Assert.Equal("2.0.0", secondResult.Version);
            }

            var zipFiles = Directory.GetFiles(repoDir, "*.zip", SearchOption.TopDirectoryOnly);
            Assert.Single(zipFiles);
        }
        finally
        {
            TryDeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task DeletePluginAsync_RemovesStoredPackage()
    {
        var repoDir = CreateTempDir();

        try
        {
            var service = CreateService(repoDir);

            await using (var package = CreatePluginZip(
                pluginId: "com.acme.delete",
                pluginName: "Delete Me",
                version: "1.0.0",
                entryPath: "plugin.json"))
            {
                var addResult = await service.AddPackageAsync(package, "delete.zip");
                Assert.True(addResult.Success);
                Assert.NotNull(addResult.StoredPath);
                Assert.True(File.Exists(addResult.StoredPath!));
            }

            var deleteResult = await service.DeletePluginAsync("com.acme.delete");

            Assert.True(deleteResult.Success);
            Assert.False(string.IsNullOrWhiteSpace(deleteResult.DeletedPath));
            Assert.Empty(Directory.GetFiles(repoDir, "*.zip", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            TryDeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task AddPackageAsync_RejectsNonZipFileName()
    {
        var repoDir = CreateTempDir();

        try
        {
            var service = CreateService(repoDir);
            await using var package = CreatePluginZip(
                pluginId: "com.acme.invalid",
                pluginName: "Invalid",
                version: "1.0.0",
                entryPath: "plugin.json");

            var result = await service.AddPackageAsync(package, "invalid.json");

            Assert.False(result.Success);
            Assert.Contains(".zip", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(repoDir);
        }
    }

    private static LocalPluginRepositoryService CreateService(string repositoryPath)
    {
        var options = Options.Create(new MarketplaceOptions
        {
            LocalRepositoryPath = repositoryPath,
            MaxUploadSizeBytes = MarketplaceOptions.DefaultMaxUploadSizeBytes
        });

        return new LocalPluginRepositoryService(options, NullLogger<LocalPluginRepositoryService>.Instance);
    }

    private static MemoryStream CreatePluginZip(
        string pluginId,
        string pluginName,
        string version,
        string entryPath)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"{{\"Id\":\"{pluginId}\",\"Name\":\"{pluginName}\",\"Version\":\"{version}\",\"MinApiVersion\":\"1.0.0\",\"EntryAssembly\":\"Plugin.dll\",\"Category\":\"Test\"}}");
        }

        stream.Position = 0;
        return stream;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NodeEditor-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
