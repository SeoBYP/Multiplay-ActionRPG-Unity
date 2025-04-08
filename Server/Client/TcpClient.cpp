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

void TcpClient::SendPacket(const std::vector<uint8_t>& data)
{
	auto bufferPtr = std::make_shared<std::vector<uint8_t>>(data);
	AsyncWrite(bufferPtr);
}


void TcpClient::OnConnect(const boost::system::error_code& error_code) {
	if (!error_code)
	{
		std::cout << "채팅 서버에 오신걸 환영합니다." << std::endl;
		std::cout << "닉네임을 입력해주세요 : " << std::endl;
		std::getline(std::cin, m_nickName);

		m_isReady = true;
		std::cout << "환영합니다. " << m_nickName << "님" << std::endl;

		// 서버에서 보낸 메시지 받는 함수
		AsyncRead();

		//// 입력 처리를 위한 Thread 시작하기
		//std::thread writeThread(&TcpClient::StartInput, this);
		//writeThread.detach();
		std::thread inputThread([this]() {
			StartInput();
			});
		inputThread.detach();
	}
}

void TcpClient::AsyncWrite(std::string& message)
{
	boost::asio::async_write(m_socket, boost::asio::buffer(message),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnWrite(errorCode, bytesTransferred);
		});
}

void TcpClient::AsyncWrite(std::shared_ptr<std::vector<uint8_t>> bufferPtr) {
	boost::asio::async_write(m_socket, boost::asio::buffer(*bufferPtr),
		[this, bufferPtr]
		(const boost::system::error_code& ec, std::size_t length) 
		{
			OnWrite(ec, length);
		});
}

void TcpClient::OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	if (errorCode) {
		std::cerr << "전송 실패: " << errorCode.message() << std::endl;
	}
	else {
		std::cout << "[Debug] 전송 완료: " << bytesTransferred << " bytes" << std::endl;
	}
}

void TcpClient::AsyncRead()
{
	boost::asio::async_read(m_socket, boost::asio::buffer(m_headerBuffer, sizeof(PacketHeader)),
		[this](boost::system::error_code ec, std::size_t length) {
			if (!ec && length == sizeof(PacketHeader)) {
				std::memcpy(&m_currentHeader, m_headerBuffer, sizeof(PacketHeader));
				std::cout << "[Debug] 수신한 헤더: type=" << static_cast<int>(m_currentHeader.type)
					<< ", size=" << m_currentHeader.size << std::endl;
				OnRead(ec, length);
			}
			else {
				std::cerr << "헤더 수신 실패: " << ec.message() << " (length=" << length << ")" << std::endl;
			}
		});
}

void TcpClient::OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	if (!errorCode) {
		m_bodyBuffer.resize(m_currentHeader.size);
		boost::asio::async_read(m_socket, boost::asio::buffer(m_bodyBuffer),
			[this](boost::system::error_code ec, std::size_t length) {
				if (!ec && length == m_currentHeader.size) {
					std::cout << "[Debug] 수신한 바디 크기: " << length << std::endl;
					try {
						HandlePacket(m_currentHeader.type, m_bodyBuffer);
					}
					catch (const std::exception& e) {
						std::cerr << "[Error] 패킷 처리 중 예외 발생: " << e.what() << std::endl;
					}
					AsyncRead();
				}
				else {
					std::cerr << "본문 수신 실패: " << ec.message() << " (length=" << length << ", expected=" << m_currentHeader.size << ")" << std::endl;
				}
			});
	}
	else {
		std::cerr << "헤더 수신 실패 (OnRead): " << errorCode.message() << std::endl;
	}
}

void TcpClient::StartInput()
{
	while (true)
	{
		if (!m_isReady) {
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
			continue;
		}

		std::string msg;
		if (!std::getline(std::cin, msg)) {
			std::cerr << "입력 중 오류 발생 또는 종료됨." << std::endl;
			break;
		}

		if (msg.empty()) continue;

		try {
			ChatPacket pkt{ m_nickName, "ALL", msg, 0 };
			auto body = pkt.Serialize();
			PacketHeader header{ PacketType::CHAT, static_cast<uint32_t>(body.size()) };

			std::vector<uint8_t> packet;
			packet.insert(packet.end(), reinterpret_cast<uint8_t*>(&header), reinterpret_cast<uint8_t*>(&header) + sizeof(header));
			packet.insert(packet.end(), body.begin(), body.end());

			SendPacket(packet);
		}
		catch (const std::exception& e) {
			std::cerr << "메시지 직렬화 또는 전송 중 오류: " << e.what() << std::endl;
		}
	}
}

void TcpClient::HandlePacket(PacketType type, const std::vector<uint8_t>& body)
{
	std::cout << "[Debug] HandlePacket: type=" << static_cast<int>(type) << ", body size=" << body.size() << std::endl;
	size_t offset = 0;
	if (type == PacketType::CHAT) {
		ChatPacket pkt = ChatPacket::Deserialize(body, offset);
		std::cout << "\n[ " << pkt.sender << " ]: " << pkt.message << std::endl;
	}
}
