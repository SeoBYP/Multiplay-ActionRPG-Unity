#pragma once

#include <sstream>
#include <vector>
#include <memory>
#include <boost/archive/binary_oarchive.hpp>
#include <boost/archive/binary_iarchive.hpp>
#include "Packet.h"

class PacketSerializer {
public:
    template <typename T>
    static std::vector<uint8_t> Serialize(const T& obj) {
        std::ostringstream oss(std::ios::binary);
        boost::archive::binary_oarchive oa(oss);
        oa << obj;

        std::string raw = oss.str();
        uint32_t length = static_cast<uint32_t>(raw.size());

        std::vector<uint8_t> buffer(sizeof(length) + raw.size());
        std::memcpy(buffer.data(), &length, sizeof(length));
        std::memcpy(buffer.data() + sizeof(length), raw.data(), raw.size());
        return buffer;
    }

    template <typename T>
    static std::unique_ptr<T> Deserialize(const std::vector<uint8_t>& buffer) {
        std::istringstream iss(std::string(
            reinterpret_cast<const char*>(buffer.data() + sizeof(uint32_t)),
            buffer.size() - sizeof(uint32_t)));
        boost::archive::binary_iarchive ia(iss);

        std::unique_ptr<T> result = std::make_unique<T>();
        ia >> (*result);
        return result;
    }

    // 다형성 Packet* 전용 Serialize
    static std::vector<uint8_t> Serialize(const Packet* packet) {
        std::ostringstream oss(std::ios::binary);
        boost::archive::binary_oarchive oa(oss);
        oa << packet;

        std::string raw = oss.str();
        uint32_t length = static_cast<uint32_t>(raw.size());

        std::vector<uint8_t> buffer(sizeof(length) + raw.size());
        std::memcpy(buffer.data(), &length, sizeof(length));
        std::memcpy(buffer.data() + sizeof(length), raw.data(), raw.size());
        return buffer;
    }

    // 다형성 Packet* 전용 Deserialize
    static std::unique_ptr<Packet> Deserialize(const std::vector<uint8_t>& buffer) {
        std::istringstream iss(std::string(
            reinterpret_cast<const char*>(buffer.data() + sizeof(uint32_t)),
            buffer.size() - sizeof(uint32_t)));
        boost::archive::binary_iarchive ia(iss);

        Packet* ptr = nullptr;
        ia >> ptr;
        return std::unique_ptr<Packet>(ptr);
    }
};
