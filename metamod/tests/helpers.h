// Общие заглушки для тестов ядра.
#pragma once

#include "core/display_service.h"
#include "core/logger.h"
#include "core/message_processor.h"

#include <cstdio>
#include <cstdlib>
#include <unistd.h>  // mkdtemp на macOS
#include <string>
#include <vector>

namespace nm_test {

class RecordingLogger final : public nm::ILogger {
public:
    std::vector<std::string> infos;
    std::vector<std::string> errors;
    std::vector<std::string> debugs;

    void Info(const std::string& message) override { infos.push_back(message); }
    void Error(const std::string& message) override { errors.push_back(message); }
    void Debug(const std::string& message) override { debugs.push_back(message); }
};

// Факты о сервере без движка: значения известны заранее
class FakeServerInfo final : public nm::IServerInfoSource {
public:
    std::string map = "de_dust2";
    std::string hostname = "Test Server";
    int maxPlayers = 32;
    int players = 7;

    std::string MapName() override { return map; }
    std::string Hostname() override { return hostname; }
    std::string Ip() override { return "127.0.0.1"; }
    std::string Port() override { return "27015"; }
    int MaxPlayers() override { return maxPlayers; }
    int Players() override { return players; }
};

struct Delivery {
    std::string channel;
    int slot;
    std::string text;
};

class RecordingSink final : public nm::IMessageSink {
public:
    std::vector<Delivery> sent;
    bool html = true;

    void Chat(int slot, const std::string& text) override { sent.push_back({"Chat", slot, text}); }
    void Center(int slot, const std::string& text) override { sent.push_back({"Center", slot, text}); }
    void Alert(int slot, const std::string& text) override { sent.push_back({"Alert", slot, text}); }
    void Console(int slot, const std::string& text) override { sent.push_back({"Console", slot, text}); }
    void CenterHtml(int slot, const std::string& text) override { sent.push_back({"CenterHtml", slot, text}); }
    bool HtmlAvailable() const override { return html; }
};

inline bool HasControlCodes(const std::string& text) {
    for (const char c : text) {
        const unsigned char b = static_cast<unsigned char>(c);
        if (b >= 0x01 && b <= 0x10) return true;
    }
    return false;
}

inline bool Contains(const std::string& text, const std::string& part) {
    return text.find(part) != std::string::npos;
}

// Каталог во временной папке, удаляется в деструкторе
class TempDir {
public:
    TempDir() {
        char pattern[] = "/tmp/nm-tests-XXXXXX";
        const char* made = mkdtemp(pattern);
        path = made != nullptr ? made : "";
    }
    ~TempDir() {
        // glibc помечает system() warn_unused_result, а (void) gcc это не снимает
        if (!path.empty() && std::system(("rm -rf '" + path + "'").c_str()) != 0) path.clear();
    }
    std::string path;
};

}  // namespace nm_test
