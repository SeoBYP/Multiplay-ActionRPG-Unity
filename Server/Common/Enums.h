#pragma once

#include <cstdint>

enum class PacketType : uint32_t {
    CHAT = 1,
    LOGIN = 2,
    SYSTEM = 3,
    UNKNOWN = 255
};

enum class ChatType {
    GLOBAL = 0,
    WHISPER = 1,
    SYSTEM = 2
};
