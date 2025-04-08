// Client.cpp : 이 파일에는 'main' 함수가 포함됩니다. 거기서 프로그램 실행이 시작되고 종료됩니다.
//

#include <iostream>
#include <boost/asio.hpp>
#include "TcpClient.h"
#include "Packet.h"
#include "ChatPacket.h"

using namespace std;

int main()
{
	boost::asio::io_context io_context;
	
	//TcpClient client(io_context);
	//client.Connect("127.0.0.1", 4242);

	std::shared_ptr<TcpClient> client = std::make_shared<TcpClient>(io_context);
	client->Connect("127.0.0.1", 4242);


	// 비동기 I/O 작업이 처리된 후에 호출되는 함수를 실행시켜주는 함수
	// 이 함수는 블록함수로 새로운 비동기 작업이 등록되거나 완료될 떄까지 기다립니다.
	io_context.run();

	// 다양한 Packet 파생 객체 생성


	//Packet packet(PacketType::SYSTEM, 10);
	//auto data = PacketSerializer::Serialize(packet);
	//auto deserializedPacket = PacketSerializer::Deserialize<Packet>(data);
	//std::cout << deserializedPacket.get()->getSize() << '\n';

	//ChatPacket chatPacket("이름",ChatType::GLOBAL,"안녕하세요");
	//auto chatdata = PacketSerializer::Serialize(chatPacket);
	//auto deserializedChatPacket = PacketSerializer::Deserialize<ChatPacket>(chatdata);
	//std::cout << deserializedChatPacket.get()->getSize() << '\n';
	//std::cout << deserializedChatPacket.get()->getMessage() << '\n';
	return 0;
}
