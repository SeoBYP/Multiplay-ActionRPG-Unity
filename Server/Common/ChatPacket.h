#pragma once

#include "Packet.h"
#include <string>
#include <boost/serialization/base_object.hpp>
#include <boost/serialization/string.hpp>
#include "Enums.h"

#ifdef PACKET_API_EXPORTS
#define PACKET_API __declspec(dllexport)
#else
#define PACKET_API __declspec(dllimport)
#endif

class PACKET_API ChatPacket : public Packet {
private:
    std::string sender;
    std::string receiver;
    ChatType chatType;
    std::string message;

public:
    ChatPacket();
    ChatPacket(std::string s, ChatType t, std::string msg, std::string r = "");

    const std::string& getSender() const { return sender; }
    const std::string& getReceiver() const { return receiver; }
    const std::string& getMessage() const { return message; }
    ChatType getChatType() const { return chatType; }

private:
    friend class boost::serialization::access;
    template<class Archive>
    void serialize(Archive& ar, const unsigned int version) {
        ar& boost::serialization::base_object<Packet>(*this);
        ar& sender;
        ar& receiver;
        ar& chatType;
        ar& message;
    }
};

// 선언은 실행파일(Client)에서만 하도록 분기
#ifndef PACKET_API_EXPORTS
#include <boost/serialization/export.hpp>
BOOST_CLASS_EXPORT_KEY(ChatPacket)
#endif
