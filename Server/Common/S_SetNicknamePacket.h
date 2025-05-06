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

struct PACKET_API S_SetNicknamePacket
{
    bool success;
    std::string message;

    std::vector<uint8_t> Serialize() const
    {
        std::vector<uint8_t> data;
        data.push_back(success ? 1 : 0);
        uint16_t len = static_cast<uint16_t>(message.size());
        data.insert(data.end(), reinterpret_cast<const uint8_t*>(&len), reinterpret_cast<const uint8_t*>(&len) + sizeof(len));
        data.insert(data.end(), message.begin(), message.end());
        return data;
    }

    static S_SetNicknamePacket Deserialize(const std::vector<uint8_t>& buffer, size_t& offset)
    {
        S_SetNicknamePacket pkt;
        pkt.success = buffer[offset++] != 0;
        uint16_t len;
        std::memcpy(&len, &buffer[offset], sizeof(len));
        offset += sizeof(len);
        pkt.message = std::string(buffer.begin() + offset, buffer.begin() + offset + len);
        offset += len;
        return pkt;
    }
};
