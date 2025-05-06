#include "SetNicknamePacketHandler.h"
#include "C_SetNicknamePacket.h"
#include "S_SetNicknamePacket.h"
#include "PlayerSession.h"
#include "GameRoom.h"
#include <iostream>

void SetNicknamePacketHandler::Handle(PlayerSession* session, const std::vector<uint8_t>& data, size_t offset)
{
	C_SetNicknamePacket request = C_SetNicknamePacket::Deserialize(data, offset);

	auto room = session->GetGameRoom();
	if (!room) {
		std::cout << "session에 설정된 GameRoom이 없습니다." << '\n';
		return;
	}

	auto duplicate = room->FindPlayerByNick(request.nickname);
	S_SetNicknamePacket response;

	if (duplicate)
	{
		response.success = false;
		response.message = "이미 사용 중인 닉네임입니다.";
	}
	else
	{
		session->SetNickname(request.nickname);
		response.success = true;
		response.message = request.nickname;
	}


	auto body = response.Serialize();
	PacketHeader header{ PacketType::SET_NICKNAME_S2C, static_cast<uint32_t>(body.size()) };

	std::vector<uint8_t> packet(sizeof(PacketHeader));
	std::memcpy(packet.data(), &header, sizeof(header));
	packet.insert(packet.end(), body.begin(), body.end());

	session->Send(packet);

	if (response.success)
		std::cout << "[닉네임 등록] " << request.nickname << " 성공\n";
	else
		std::cout << "[닉네임 실패] " << request.nickname << " 중복\n";
}
