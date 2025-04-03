#pragma once

#include <cstdint>

// DLL export/import ¼³Á¤
#ifdef PACKET_API_EXPORTS
#define PACKET_API __declspec(dllexport)
#else
#define PACKET_API __declspec(dllimport)
#endif

#pragma once

#include "Enums.h"
#include <boost/serialization/serialization.hpp>
#include <cstdint>


class Packet {
protected:
    PacketType m_type;
    uint32_t m_size;

public:
    Packet(PacketType type = PacketType::UNKNOWN, uint32_t size = 0)
        : m_type(type), m_size(size) {
    }
    virtual ~Packet() = default;

    PacketType getType() const { return m_type; }
    uint32_t getSize() const { return m_size; }

protected:
    void setType(PacketType type) { m_type = type; }
    void setSize(uint32_t size) { m_size = size; }

private:
    friend class boost::serialization::access;
    template<class Archive>
    void serialize(Archive& ar, const unsigned int version) {
        ar& m_type;
        ar& m_size;
    }
};