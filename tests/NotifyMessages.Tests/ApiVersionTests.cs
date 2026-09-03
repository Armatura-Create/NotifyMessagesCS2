using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Xunit;

namespace NotifyMessages.Tests;

/// Регрессия на реальный инцидент: в 2.1.0 в MinimumApiVersion поставили версию «посвежее»
/// (373), хотя собирались против неё же и ничего нового не использовали. Сервер на 1.0.371
/// отказался грузить плагин.
///
/// Правило: собираем против МИНИМАЛЬНОЙ поддерживаемой версии, и MinimumApiVersion равен ей.
/// Тогда компиляция сама доказывает, что API из более новых сборок не используется.
public class ApiVersionTests
{
    [Fact]
    public void MinimumApiVersion_MatchesTheCounterStrikeSharpBuildWeCompileAgainst()
    {
        var declared = typeof(NotifyMessages).GetCustomAttribute<MinimumApiVersion>();
        Assert.NotNull(declared);

        var referencedBuild = typeof(BasePlugin).Assembly.GetName().Version!.Build;

        Assert.Equal(referencedBuild, declared!.Version);
    }

    [Fact]
    public void MinimumApiVersion_IsAtLeastTheDotnet10Boundary()
    {
        // 1.0.369 — первая версия CounterStrikeSharp на .NET 10.
        // Ниже неё net10.0-плагин физически не загрузится.
        var declared = typeof(NotifyMessages).GetCustomAttribute<MinimumApiVersion>();
        Assert.True(declared!.Version >= 369,
            $"MinimumApiVersion {declared.Version} ниже границы .NET 10 (369)");
    }
}
