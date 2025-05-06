#pragma once

#include "vector"

class PlayerSession;

class IPacketHandler
{
public:
    virtual ~IPacketHandler() = default;
    virtual void Handle(PlayerSession* session, const std::vector<uint8_t>& data, size_t offset) = 0;
};

