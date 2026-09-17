using System;
using System.Reflection;
using SwiftlyS2.Shared;
using Xunit;

namespace NotifyMessages.Tests;

/// Версия не хардкодится: её источник — <Version> в .csproj (и -p:Version= в CI).
/// Иначе номер приходится помнить поднять в двух местах, и релиз уезжает со старым.
public class ModuleVersionTests
{
    [Fact]
    public void FormatModuleVersion_StripsSourceLinkSuffix()
        => Assert.Equal("v2.3.0", PluginText.FormatModuleVersion("2.3.0+1319a4ada93f7a6d63c66b090aee77f8e8e206ef", null));

    [Fact]
    public void FormatModuleVersion_KeepsPreReleaseSuffix()
        => Assert.Equal("v2.3.0-rc1", PluginText.FormatModuleVersion("2.3.0-rc1", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+onlysha")]
    public void FormatModuleVersion_FallsBackToAssemblyVersion(string? informational)
        => Assert.Equal("v3.4.5", PluginText.FormatModuleVersion(informational, new Version(3, 4, 5, 6)));

    [Fact]
    public void FormatModuleVersion_HasSafeFallbackWhenNothingIsKnown()
        => Assert.Equal("v0.0.0", PluginText.FormatModuleVersion(null, null));

    [Fact]
    public void ResolveModuleVersion_IsNotHardcoded()
    {
        var resolved = PluginText.ResolveModuleVersion(typeof(PluginText).Assembly);
        var assemblyVersion = typeof(PluginText).Assembly.GetName().Version!;

        Assert.StartsWith($"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", resolved,
            StringComparison.Ordinal);
    }

    /// PluginMetadata.Version — константа времени компиляции, поэтому её генерирует
    /// MSBuild из того же <Version>. Тест ловит возврат к литералу: там всегда
    /// окажется версия, забытая при прошлом релизе.
    [SkippableFact]
    public void PluginMetadataVersion_ComesFromTheAssemblyVersion()
    {
        Skip.IfNot(SwiftlyRuntime.Available, SwiftlyRuntime.SkipReason);
        Bound.MetadataMatchesAssembly();
    }

    private static class Bound
    {
        public static void MetadataMatchesAssembly()
        {
            var declared = typeof(NotifyMessages).GetCustomAttribute<PluginMetadata>()!;
            var resolved = PluginText.ResolveModuleVersion(typeof(PluginText).Assembly);

            Assert.Equal(resolved, "v" + declared.Version);
        }
    }
}
