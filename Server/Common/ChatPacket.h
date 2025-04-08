#pragma once

#include "Packet.h"
#include <string>
#include <boost/serialization/base_object.hpp>
#include <boost/serialization/string.hpp>
#include "Enums.h"
#include <vector>

#ifdef PACKET_API_EXPORTS
#define PACKET_API __declspec(dllexport)
#else
#define PACKET_API __declspec(dllimport)
#endif

struct PACKET_API ChatPacket {
    std::string sender;
    std::string receiver;
    std::string message;
    uint8_t chatType;

    std::vector<uint8_t> Serialize() const {
        std::vector<uint8_t> data;
        auto writeString = [&](const std::string& s) {
            uint16_t len = static_cast<uint16_t>(s.size());
            data.insert(data.end(), reinterpret_cast<uint8_t*>(&len), reinterpret_cast<uint8_t*>(&len) + sizeof(len));
            data.insert(data.end(), s.begin(), s.end());
            };

        writeString(sender);
        writeString(receiver);
        writeString(message);
        data.push_back(chatType);

        return data;
    }

    static ChatPacket Deserialize(const std::vector<uint8_t>& buffer, size_t& offset) {
        auto readString = [&](std::string& out) {
            uint16_t len;
            std::memcpy(&len, &buffer[offset], sizeof(len));
            offset += sizeof(len);
            out = std::string(buffer.begin() + offset, buffer.begin() + offset + len);
            offset += len;
            };

        ChatPacket pkt;
        readString(pkt.sender);
        readString(pkt.receiver);
        readString(pkt.message);
        pkt.chatType = buffer[offset++];
        return pkt;
    }
};