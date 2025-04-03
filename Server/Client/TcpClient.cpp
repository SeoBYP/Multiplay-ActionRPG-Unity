#include "TcpClient.h"
#include <iostream>
#include <ChatPacket.h>


TcpClient::TcpClient(boost::asio::io_context& io_context) : m_socket(io_context)
{

}

void TcpClient::Connect(std::string host, int port)
{
	const boost::asio::ip::tcp::endpoint endpoint(boost::asio::ip::make_address(host), port);
	m_socket.async_connect(endpoint, [this](const boost::system::error_code& errorCode) {
		this->OnConnect(errorCode);
		});
}


void TcpClient::OnConnect(const boost::system::error_code& error_code) {
	std::cout << "채팅 서버에 오신걸 환영합니다." << std::endl;
	std::cout << "닉네임을 입력해주세요 : " << std::endl;
	if (!error_code)
	{
		// 서버에서 보낸 메시지 받는 함수
		AsyncRead();

		// 입력 처리를 위한 Thread 시작하기
		std::thread writeThread(&TcpClient::InputText, this);
		writeThread.detach();
	}
}

void TcpClient::SendPacket(const Packet* packet)
{
	//std::string serializedData = serializePacket(packet);
	//AsyncWrite(serializedData);
}

void TcpClient::AsyncWrite(std::string message)
{
	m_sendMessage = message;
	boost::asio::async_write(m_socket, boost::asio::buffer(m_sendMessage),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnWrite(errorCode, bytesTransferred);
		});
}

void TcpClient::OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	if (!errorCode)
	{

	}
	else
	{
		std::cout << "Error: " << errorCode.message() << std::endl;
	}
}

void TcpClient::AsyncRead()
{
	m_socket.async_read_some(boost::asio::buffer(m_RecvBuffer, m_RecvBufferSize),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnRead(errorCode, bytesTransferred);
		});
}

void TcpClient::OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	if (!errorCode)
	{
		try
		{
			std::string receivedData(m_RecvBuffer, bytesTransferred);
			std::cout << "[TcpSession] 채팅 메시지 수신: " << receivedData << '\n';
			//auto packet = deserializePacket(receivedData);

			//if (!packet) {
			//	std::cerr << "[Error] 패킷 역직렬화 실패: packet == nullptr" << std::endl;
			//	return;
			//}

			//if (packet->getType() == PacketType::CHAT) {
			//	auto chatPacket = dynamic_cast<ChatPacket*>(packet.get());
			//	if (!chatPacket) {
			//		std::cerr << "[Error] ChatPacket 캐스팅 실패" << std::endl;
			//		return;
			//	}
			//	std::cout << chatPacket->getSenderNickname() << ": " << chatPacket->getMessage() << std::endl;
			//}

			std::cout << std::string(m_RecvBuffer) << std::endl;
			memset(m_RecvBuffer, 0, m_RecvBufferSize);
			AsyncRead();
		}
		catch (const std::exception&)
		{
			std::cout << std::string(m_RecvBuffer) << std::endl;
		}
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << std::endl;
	}
}

void TcpClient::InputText()
{
	while (true)
	{
		std::string message;
		std::getline(std::cin, message);
		if (!message.empty())
		{
			if (m_nickName.empty())
			{
				m_nickName = message;
				std::cout << "닉네임이 설정되었습니다. " << std::endl;
				std::cout << m_nickName << "님 어서오세요. " << std::endl;
			}
			else
			{
				// ChatPacket으로 감싸서 전송
				ChatPacket chat(m_nickName, ChatType::GLOBAL, message);
				SendPacket(&chat);  // stack 객체 주소만 넘기기
			}
		}
	}
}
