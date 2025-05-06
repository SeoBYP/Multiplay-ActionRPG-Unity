#pragma once

#include <unordered_map>
#include <memory>
#include "IPacketHandler.h"
#include "PacketType.h" // PacketType enum 정의

class PlayerSession;

class PacketHandlerRegistry
{
public:
    void Register(PacketType type, std::unique_ptr<IPacketHandler> handler);
    void Dispatch(PlayerSession* session, PacketType type, const std::vector<uint8_t>& data, size_t offset);

private:
    std::unordered_map<PacketType, std::unique_ptr<IPacketHandler>> _handlers;
};

// 전역 인스턴스 선언
extern PacketHandlerRegistry g_PacketHandlerRegistry;
