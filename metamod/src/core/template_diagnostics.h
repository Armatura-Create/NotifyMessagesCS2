// Статический анализатор шаблонов: неизвестные теги, теги не на своём месте,
// дыры в переводах. Порт Utils/TemplateDiagnostics.cs и ConfigService.Diagnostics.cs.
//
// Тег, который не подставит ни MessageProcessor, ни рендер цветов, доезжает до
// игрока литеральным текстом в скобках. Раньше это находил только игрок.
#pragma once

#include "core/config.h"

#include <string>
#include <vector>

namespace nm {

enum class TemplateSeverity { Error, Warning };

struct TemplateIssue {
    TemplateSeverity severity;
    std::string where;
    std::string tag;
    std::string text;

    // "[ОШИБКА] Ads.json → блок #1, Chat: …"
    std::string ToString() const;
};

// Разбирает один шаблон. allowedContextTags — теги, которые в этом месте
// конфига действительно кто-то подставит.
std::vector<TemplateIssue> AnalyzeTemplate(const std::string& templateText, const Config& config,
                                           const std::string& where,
                                           const std::vector<std::string>& allowedContextTags = {});

// Ключ есть, но не на всех языках, которые встречаются в Messages.json.
std::vector<TemplateIssue> AnalyzeLanguageCoverage(const Config& config);

// Все претензии ко всем шаблонам конфига: при загрузке и по mm_nm_check.
std::vector<TemplateIssue> CollectIssues(const Config& config);

// Наборы контекстных тегов по местам конфига
namespace context {
extern const std::vector<std::string> kPlayerNameOnly;
extern const std::vector<std::string> kRestart;
extern const std::vector<std::string> kChangeTeam;
extern const std::vector<std::string> kJoinTeam;
extern const std::vector<std::string> kJoinLeave;
extern const std::vector<std::string> kServer;
}  // namespace context

}  // namespace nm
