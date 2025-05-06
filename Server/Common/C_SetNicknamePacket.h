#pragma once
#include <string>
#include <vector>
#include <cstring>
#include <boost/serialization/base_object.hpp>
#include <boost/serialization/string.hpp>

#ifdef PACKET_API_EXPORTS
#define PACKET_API __declspec(dllexport)
#else
#define PACKET_API __declspec(dllimport)
#endif

struct PACKET_API C_SetNicknamePacket
{
    std::string nickname;

    std::vector<uint8_t> Serialize() const
    {
        std::vector<uint8_t> data;
        uint16_t len = static_cast<uint16_t>(nickname.size());
        data.insert(data.end(), reinterpret_cast<const uint8_t*>(&len), reinterpret_cast<const uint8_t*>(&len) + sizeof(len));
        data.insert(data.end(), nickname.begin(), nickname.end());
        return data;
    }

    static C_SetNicknamePacket Deserialize(const std::vector<uint8_t>& buffer, size_t& offset)
    {
        C_SetNicknamePacket pkt;
        uint16_t len;
        std::memcpy(&len, &buffer[offset], sizeof(len));
        offset += sizeof(len);
        pkt.nickname = std::string(buffer.begin() + offset, buffer.begin() + offset + len);
        offset += len;
        return pkt;
    }
};
