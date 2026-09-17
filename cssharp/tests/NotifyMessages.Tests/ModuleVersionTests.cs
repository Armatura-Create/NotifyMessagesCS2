using System;
using System.Reflection;
using Xunit;

namespace NotifyMessages.Tests;

/// Регрессия: релиз v2.1.1 уехал с ModuleVersion "v2.1.0" внутри, потому что версию
/// надо было помнить поднять руками в двух местах. Теперь она одна — из метаданных сборки.
public class ModuleVersionTests
{
    [Fact]
    public void FormatModuleVersion_StripsSourceLinkSuffix()
    {
        Assert.Equal("v2.1.1-fix",
            NotifyMessages.FormatModuleVersion("2.1.1-fix+1319a4ada93f7a6d63c66b090aee77f8e8e206ef", null));
    }

    [Fact]
    public void FormatModuleVersion_KeepsPreReleaseSuffix()
    {
        Assert.Equal("v2.1.1-fix", NotifyMessages.FormatModuleVersion("2.1.1-fix", null));
    }

    [Fact]
    public void FormatModuleVersion_UsesPlainVersionAsIs()
    {
        Assert.Equal("v2.1.1", NotifyMessages.FormatModuleVersion("2.1.1", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+onlysha")]
    public void FormatModuleVersion_FallsBackToAssemblyVersion(string? informational)
    {
        Assert.Equal("v3.4.5", NotifyMessages.FormatModuleVersion(informational, new Version(3, 4, 5, 6)));
    }

    [Fact]
    public void FormatModuleVersion_HasSafeFallbackWhenNothingIsKnown()
    {
        Assert.Equal("v0.0.0", NotifyMessages.FormatModuleVersion(null, null));
    }

    [Fact]
    public void ResolveModuleVersion_MatchesTheBuiltAssemblyMetadata()
    {
        var assembly = typeof(NotifyMessages).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var expected = "v" + informational.Split('+')[0];

        Assert.Equal(expected, NotifyMessages.ResolveModuleVersion(assembly));
    }

    [Fact]
    public void ResolveModuleVersion_IsNotHardcoded()
    {
        // Версия обязана следовать за сборкой, а не за строковым литералом в коде
        var resolved = NotifyMessages.ResolveModuleVersion(typeof(NotifyMessages).Assembly);
        var assemblyVersion = typeof(NotifyMessages).Assembly.GetName().Version!;

        Assert.StartsWith($"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", resolved);
    }
}
