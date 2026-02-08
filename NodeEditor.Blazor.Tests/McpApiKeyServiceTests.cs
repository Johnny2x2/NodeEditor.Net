using NodeEditor.Blazor.Services.Mcp;

namespace NodeEditor.Blazor.Tests;

public sealed class McpApiKeyServiceTests
{
    [Fact]
    public void GetCurrentKey_ReturnsNull_WhenNoKeyGenerated()
    {
        var service = CreateIsolatedService();

        Assert.Null(service.GetCurrentKey());
    }

    [Fact]
    public void GenerateNewKey_ReturnsNonEmptyKey()
    {
        var service = CreateIsolatedService();

        var key = service.GenerateNewKey();

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.True(key.Length > 20);
    }

    [Fact]
    public void GenerateNewKey_PersistsKey()
    {
        var service = CreateIsolatedService();

        var key = service.GenerateNewKey();
        var retrieved = service.GetCurrentKey();

        Assert.Equal(key, retrieved);
    }

    [Fact]
    public void GenerateNewKey_ReplacesExistingKey()
    {
        var service = CreateIsolatedService();

        var key1 = service.GenerateNewKey();
        var key2 = service.GenerateNewKey();

        Assert.NotEqual(key1, key2);
        Assert.Equal(key2, service.GetCurrentKey());
    }

    [Fact]
    public void ValidateKey_ReturnsTrueForCorrectKey()
    {
        var service = CreateIsolatedService();

        var key = service.GenerateNewKey();

        Assert.True(service.ValidateKey(key));
    }

    [Fact]
    public void ValidateKey_ReturnsFalseForWrongKey()
    {
        var service = CreateIsolatedService();

        service.GenerateNewKey();

        Assert.False(service.ValidateKey("wrong-key"));
    }

    [Fact]
    public void ValidateKey_ReturnsFalseForNull()
    {
        var service = CreateIsolatedService();

        service.GenerateNewKey();

        Assert.False(service.ValidateKey(null));
    }

    [Fact]
    public void ValidateKey_ReturnsFalseForEmptyString()
    {
        var service = CreateIsolatedService();

        service.GenerateNewKey();

        Assert.False(service.ValidateKey(""));
    }

    [Fact]
    public void ValidateKey_ReturnsFalseWhenNoKeyGenerated()
    {
        var service = CreateIsolatedService();

        Assert.False(service.ValidateKey("some-key"));
    }

    [Fact]
    public void RevokeKey_ClearsKey()
    {
        var service = CreateIsolatedService();

        service.GenerateNewKey();
        service.RevokeKey();

        Assert.Null(service.GetCurrentKey());
    }

    [Fact]
    public void RevokeKey_DoesNotThrowWhenNoKey()
    {
        var service = CreateIsolatedService();

        service.RevokeKey(); // should not throw
    }

    [Fact]
    public void ValidateKey_FailsAfterRevoke()
    {
        var service = CreateIsolatedService();

        var key = service.GenerateNewKey();
        service.RevokeKey();

        Assert.False(service.ValidateKey(key));
    }

    [Fact]
    public void GenerateNewKey_DoesNotContainUnsafeChars()
    {
        var service = CreateIsolatedService();

        // Generate multiple keys and check none contain +, /, or =
        for (int i = 0; i < 10; i++)
        {
            var key = service.GenerateNewKey();
            Assert.DoesNotContain("+", key);
            Assert.DoesNotContain("/", key);
            Assert.DoesNotContain("=", key);
        }
    }

    [Fact]
    public void ConcurrentValidation_IsThreadSafe()
    {
        var service = CreateIsolatedService();
        var key = service.GenerateNewKey();

        Parallel.For(0, 100, _ =>
        {
            Assert.True(service.ValidateKey(key));
            Assert.False(service.ValidateKey("wrong"));
        });
    }

    // ── Enabled / Disabled ──────────────────────────────────────────

    [Fact]
    public void IsEnabled_DefaultsFalse()
    {
        var service = CreateIsolatedService();

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_PersistsState()
    {
        var service = CreateIsolatedService();

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_False_AfterTrue()
    {
        var service = CreateIsolatedService();

        service.SetEnabled(true);
        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_SurvivesNewInstance()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NodeEditorMax-Tests", Guid.NewGuid().ToString("N"));
        var service = new McpApiKeyService(tempDir);
        service.SetEnabled(true);

        // Simulate a new instance reading from disk (same directory)
        var service2 = new McpApiKeyService(tempDir);
        Assert.True(service2.IsEnabled());
    }

    [Fact]
    public void ConcurrentEnabledToggle_IsThreadSafe()
    {
        var service = CreateIsolatedService();

        Parallel.For(0, 100, i =>
        {
            service.SetEnabled(i % 2 == 0);
            _ = service.IsEnabled(); // should not throw
        });
    }

    /// <summary>
    /// Creates a McpApiKeyService backed by a unique temp directory so tests
    /// don't interfere with each other or with the real key store.
    /// </summary>
    private static McpApiKeyService CreateIsolatedService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NodeEditorMax-Tests", Guid.NewGuid().ToString("N"));
        return new McpApiKeyService(tempDir);
    }
}
