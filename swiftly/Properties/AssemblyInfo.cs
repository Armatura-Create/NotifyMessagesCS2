using System.Runtime.CompilerServices;

// Тесты проверяют разбор недоверенных A2S-пакетов, диагностику шаблонов и рендер —
// это internal-логика, публичного API для неё заводить незачем.
[assembly: InternalsVisibleTo("NotifyMessages.Swiftly.Tests")]
