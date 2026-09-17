using System.Reflection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using Xunit;

namespace NotifyMessages.Tests;

/// Регрессия на реальный инцидент из cssharp/: MinimumApiVersion поставили «посвежее»,
/// чем версия, против которой собирались, и сервер на более старой сборке отказался
/// грузить плагин.
///
/// Правило: собираемся против МИНИМАЛЬНОЙ поддерживаемой версии, и MinimumAPIVersion
/// равен ей. Тогда компиляция сама доказывает, что API из новых сборок не используется.
public class ApiVersionTests
{
    [SkippableFact]
    public void MinimumApiVersion_MatchesTheSwiftlyBuildWeCompileAgainst()
    {
        Skip.IfNot(SwiftlyRuntime.Available, SwiftlyRuntime.SkipReason);
        Bound.MatchesReferencedAssembly();
    }

    [SkippableFact]
    public void MinimumApiVersion_MatchesTheConstantUsedInTheProjectFile()
    {
        Skip.IfNot(SwiftlyRuntime.Available, SwiftlyRuntime.SkipReason);
        Bound.MatchesConstant();
    }

    [SkippableFact]
    public void PluginId_IsStable()
    {
        Skip.IfNot(SwiftlyRuntime.Available, SwiftlyRuntime.SkipReason);
        Bound.PluginIdIsStable();
    }

    /// Всё, что трогает типы SwiftlyS2 — см. комментарий в SwiftlyRuntime.
    private static class Bound
    {
        public static void MatchesReferencedAssembly()
        {
            var declared = typeof(NotifyMessages).GetCustomAttribute<PluginMetadata>();
            Assert.NotNull(declared);

            // Версия сборки SwiftlyS2 — та, против которой реально идёт компиляция.
            // Сравниваем по "major.minor.build": в NuGet-версии четвёртого числа нет.
            var referenced = typeof(BasePlugin).Assembly.GetName().Version!;
            var expected = $"{referenced.Major}.{referenced.Minor}.{referenced.Build}";

            Assert.Equal(expected, declared!.MinimumAPIVersion);
        }

        public static void MatchesConstant()
        {
            var declared = typeof(NotifyMessages).GetCustomAttribute<PluginMetadata>();
            Assert.Equal(NotifyMessages.SwiftlyApiVersion, declared!.MinimumAPIVersion);
        }

        public static void PluginIdIsStable()
        {
            // Id определяет путь конфигов (configs/plugins/<Id>).
            // Переименование молча уводит сервер на пустой конфиг.
            var declared = typeof(NotifyMessages).GetCustomAttribute<PluginMetadata>();
            Assert.Equal("NotifyMessages", declared!.Id);
        }
    }
}
