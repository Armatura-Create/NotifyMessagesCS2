using System;
using System.Reflection;

namespace NotifyMessages;

/// Чистые помощники, не знающие ни про SwiftlyS2, ни про движок.
///
/// Вынесены из класса плагина не ради красоты: сборка SwiftlyS2.CS2 собрана под x64
/// (игровой сервер бывает только таким), и любое обращение к типу, унаследованному
/// от BasePlugin, тянет её загрузку. На arm64-машине разработчика это FileLoadException,
/// и разбор номера версии становится непроверяемым там, где для проверки не нужно
/// вообще ничего.
public static class PluginText
{
    /// Версия сборки в виде "vX.Y.Z" (с суффиксом pre-release, если он есть).
    public static string ResolveModuleVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return FormatModuleVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);
    }

    /// Чистая часть резолва — вынесена ради тестов.
    /// InformationalVersion может нести хвост "+Sha.abc123" от SourceLink — его отрезаем.
    public static string FormatModuleVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            var trimmed = plus >= 0 ? informationalVersion[..plus] : informationalVersion;
            if (!string.IsNullOrWhiteSpace(trimmed)) return "v" + trimmed.Trim();
        }

        return assemblyVersion == null ? "v0.0.0" : "v" + assemblyVersion.ToString(3);
    }
}
