using System.Runtime.CompilerServices;

// Тесты проверяют разбор недоверенных A2S-пакетов и сборку строк статуса —
// это internal-логика, публичного API для неё заводить незачем.
[assembly: InternalsVisibleTo("NotifyMessages.Tests")]
