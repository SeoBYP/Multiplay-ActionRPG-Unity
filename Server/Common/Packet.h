#pragma once

#include <cstdint>

// DLL export/import ¼³Á¤
#ifdef PACKET_API_EXPORTS
#define PACKET_API __declspec(dllexport)
#else
#define PACKET_API __declspec(dllimport)
#endif

#include "Enums.h"
#include <boost/serialization/serialization.hpp>
#include <cstdint>

#pragma pack(push, 1)
struct PACKET_API PacketHeader {
    PacketType type;
    uint32_t size;
};

#pragma pack(pop)