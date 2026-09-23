// Указатели на интерфейсы движка.
//
// Всё здесь доступно ТОЛЬКО из главного потока. Обращение к любому из этих
// указателей из фонового потока — чтение чужой памяти и смерть процесса без стека.
#pragma once

class IVEngineServer2;
class ISource2Server;
class IGameEventSystem;
class IGameEventManager2;
class INetworkMessages;
// Именно ISource2GameClients: IServerGameClients в eiface.h — typedef, и
// форвард-объявление его классом ломает сборку раньше первой полезной ошибки.
class ISource2GameClients;

namespace nm {

// Фабричные интерфейсы — получаются в Load() через GET_V_IFACE_*
extern IVEngineServer2* g_engine;
extern ISource2Server* g_server;
extern ISource2GameClients* g_gameClients;
extern IGameEventSystem* g_gameEventSystem;
extern INetworkMessages* g_networkMessages;

// Менеджера игровых событий фабрика не отдаёт. Его vtable ищется по RTTI
// (mm/rtti.cpp), а сам объект — первый аргумент нашего хука FireEvent: движок
// стреляет событиями постоянно, и к первому HTML-сообщению указатель уже есть.
// nullptr — ещё не пойман: HTML-центр тогда выводится обычным центром.
extern IGameEventManager2* g_gameEventManager;

// ICvar своего указателя не имеет специально: интерфейс забирается в g_pCVar
// из tier1, потому что ConVar_Register (а значит и META_CONVAR_REGISTER) смотрит
// именно туда. Свой второй указатель молча оставил бы команды незарегистрированными.

}  // namespace nm
