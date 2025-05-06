#include "PacketHandlerRegistry.h"
#include "IPacketHandler.h"
#include <iostream>

PacketHandlerRegistry g_PacketHandlerRegistry; // 전역 정의

void PacketHandlerRegistry::Register(PacketType type, std::unique_ptr<IPacketHandler> handler)
{
    if (_handlers.find(type) != _handlers.end())
    {
        std::cerr << "[PacketHandlerRegistry] Warning: 패킷 타입 중복 등록 시도: " << static_cast<int>(type) << "\n";
        return;
    }

    _handlers[type] = std::move(handler);
}

void PacketHandlerRegistry::Dispatch(PlayerSession* session, PacketType type, const std::vector<uint8_t>& data, size_t offset)
{
    auto it = _handlers.find(type);
    if (it != _handlers.end())
    {
        it->second->Handle(session, data, offset);
    }
    else
    {
        std::cerr << "[Dispatcher] 등록되지 않은 패킷 타입: " << static_cast<int>(type) << "\n";
    }
}
