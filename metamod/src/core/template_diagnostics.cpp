#include "core/template_diagnostics.h"

#include "core/message_processor.h"
#include "core/text_formatter.h"

#include <set>
#include <utility>

namespace nm {

namespace context {
const std::vector<std::string> kPlayerNameOnly = {"{PLAYERNAME}"};
const std::vector<std::string> kRestart = {"{TIME_RESTART}", "{SECONDS}"};
const std::vector<std::string> kChangeTeam = {"{PLAYERNAME}", "{TEAM}", "{OLD_TEAM}"};
const std::vector<std::string> kJoinTeam = {"{PLAYERNAME}", "{TEAM}"};
const std::vector<std::string> kJoinLeave = {"{PLAYERNAME}", "{COUNTRY}", "{CITY}"};
const std::vector<std::string> kServer = {"{SERVER_IP}", "{SERVER_PORT}", "{SERVER_MAP}",
                                          "{SERVER_PLAYERS}", "{SERVER_MAXPLAYERS}"};
}  // namespace context

namespace {

// Больше трёх уровней вложенности ({prefix} -> текст -> ещё ключ) не бывает,
// а предел заодно страхует от циклов {a} -> {b} -> {a}
constexpr int kMaxDepth = 3;

using CiSet = std::set<std::string, CaseInsensitiveLess>;

// Теги, которые подставляет вызывающий код, а не MessageProcessor.
// Значение — человекочитаемое «где именно».
const CiMap<std::string>& ContextualTags() {
    static const CiMap<std::string> tags = {
        {"{PLAYERNAME}", "WelcomeMessage, ChangeTeamMessage, JoinTeamMessage, Join/LeaveMessages"},
        {"{TEAM}", "ChangeTeamMessage, JoinTeamMessage"},
        {"{OLD_TEAM}", "ChangeTeamMessage"},
        {"{TIME_RESTART}", "RestartNotify"},
        {"{SECONDS}", "RestartNotify"},
        {"{COUNTRY}", "JoinMessages, LeaveMessages"},
        {"{CITY}", "JoinMessages, LeaveMessages"},
        {"{SERVER_IP}", "Servers.json"},
        {"{SERVER_PORT}", "Servers.json"},
        {"{SERVER_MAP}", "Servers.json"},
        {"{SERVER_PLAYERS}", "Servers.json"},
        {"{SERVER_MAXPLAYERS}", "Servers.json"},
    };
    return tags;
}

void Walk(const std::string& text, const Config& config, const std::string& where,
          const CiSet& allowed, std::vector<TemplateIssue>* issues, CiSet* expanded, int depth) {
    if (depth > kMaxDepth) return;

    for (const TagMatch& match : MessageProcessor::FindTags(text)) {
        if (text::IsKnownColorTag(match.tag)) continue;
        if (MessageProcessor::IsSystemTag(match.tag)) continue;
        if (allowed.count(match.tag) != 0) continue;

        const auto contextual = ContextualTags().find(match.tag);
        if (contextual != ContextualTags().end()) {
            issues->push_back({TemplateSeverity::Warning, where, match.tag,
                               "тег " + match.tag + " здесь никто не подставит — он работает только в: " +
                                   contextual->second});
            continue;
        }

        const auto translations = config.languageMessages.find(match.name);
        if (translations != config.languageMessages.end()) {
            // Ключ известен. Его тексты сами содержат теги — проверяем и их
            if (!expanded->insert(match.name).second) continue;
            for (const auto& lang : translations->second) {
                if (lang.second.empty()) continue;
                Walk(lang.second, config, where + " → " + match.tag + "[" + lang.first + "]", allowed,
                     issues, expanded, depth + 1);
            }
            continue;
        }

        issues->push_back({TemplateSeverity::Error, where, match.tag,
                           "неизвестный тег " + match.tag +
                               " — игрок увидит его как текст. Добавьте ключ в Messages.json "
                               "или исправьте опечатку"});
    }
}

void Check(const std::string& templateText, const Config& config, const std::string& where,
           const std::vector<std::string>& context, std::vector<TemplateIssue>* issues) {
    for (TemplateIssue& issue : AnalyzeTemplate(templateText, config, where, context)) {
        issues->push_back(std::move(issue));
    }
}

void CheckMessageList(const CiMap<std::vector<std::string>>& messages, const std::string& where,
                      const Config& config, std::vector<TemplateIssue>* issues) {
    for (const auto& lang : messages) {
        for (size_t i = 0; i < lang.second.size(); ++i) {
            Check(lang.second[i], config, where + "[" + lang.first + "] #" + std::to_string(i + 1),
                  context::kJoinLeave, issues);
        }
    }
}

}  // namespace

std::string TemplateIssue::ToString() const {
    return std::string(severity == TemplateSeverity::Error ? "[ОШИБКА] " : "[внимание] ") + where +
           ": " + text;
}

std::vector<TemplateIssue> AnalyzeTemplate(const std::string& templateText, const Config& config,
                                           const std::string& where,
                                           const std::vector<std::string>& allowedContextTags) {
    std::vector<TemplateIssue> issues;
    if (templateText.empty()) return issues;

    const CiSet allowed(allowedContextTags.begin(), allowedContextTags.end());
    CiSet expanded;
    Walk(templateText, config, where, allowed, &issues, &expanded, 0);
    return issues;
}

std::vector<TemplateIssue> AnalyzeLanguageCoverage(const Config& config) {
    std::vector<TemplateIssue> issues;

    // Все языки конфига: ключи переводов плюс DefaultLang
    CiSet languages;
    if (!config.defaultLang.empty()) languages.insert(config.defaultLang);
    for (const auto& key : config.languageMessages) {
        for (const auto& lang : key.second) languages.insert(lang.first);
    }
    if (languages.empty()) return issues;

    for (const auto& key : config.languageMessages) {
        for (const std::string& lang : languages) {
            if (key.second.count(lang) != 0) continue;
            issues.push_back({TemplateSeverity::Warning, "Messages.json → " + key.first, "{" + key.first + "}",
                              "нет перевода на «" + lang +
                                  "» — игрок с этим языком увидит текст на другом языке"});
        }
    }
    return issues;
}

std::vector<TemplateIssue> CollectIssues(const Config& config) {
    std::vector<TemplateIssue> issues;
    const std::vector<std::string> none;

    // --- Settings.json ---
    if (config.welcomeMessage) {
        Check(config.welcomeMessage->message, config, "Settings.json → WelcomeMessage",
              context::kPlayerNameOnly, &issues);
    }
    Check(config.changeTeamMessage, config, "Settings.json → ChangeTeamMessage", context::kChangeTeam, &issues);
    Check(config.joinTeamMessage, config, "Settings.json → JoinTeamMessage", context::kJoinTeam, &issues);
    Check(config.titleAnnounceServers, config, "Settings.json → TitleAnnounceServers", none, &issues);

    if (config.restartNotify) {
        Check(config.restartNotify->defaultMessage, config, "Settings.json → RestartNotify.DefaultMessage",
              context::kRestart, &issues);
        for (const auto& threshold : config.restartNotify->thresholds) {
            Check(threshold.second, config,
                  "Settings.json → RestartNotify.Thresholds[" + threshold.first + "]", context::kRestart,
                  &issues);
        }
    }

    // --- Messages.json ---
    CheckMessageList(config.joinMessages, "Messages.json → JoinMessages", config, &issues);
    CheckMessageList(config.leaveMessages, "Messages.json → LeaveMessages", config, &issues);
    for (TemplateIssue& issue : AnalyzeLanguageCoverage(config)) issues.push_back(std::move(issue));

    // --- Ads.json ---
    for (size_t i = 0; i < config.ads.size(); ++i) {
        for (const OrderedPairs& block : config.ads[i].messages) {
            for (const auto& channel : block) {
                Check(channel.second, config,
                      "Ads.json → блок #" + std::to_string(i + 1) + ", " + channel.first, none, &issues);
            }
        }
    }

    // --- Servers.json ---
    if (config.servers) {
        for (size_t i = 0; i < config.servers->list.size(); ++i) {
            const ServerData& server = config.servers->list[i];
            const std::string index = std::to_string(i + 1);
            Check(server.messageTemplate, config, "Servers.json → сервер #" + index + ", MessageTemplate",
                  context::kServer, &issues);
            Check(server.messageTemplateConsole, config,
                  "Servers.json → сервер #" + index + ", MessageTemplateConsole", context::kServer, &issues);
        }
    }

    return issues;
}

}  // namespace nm
