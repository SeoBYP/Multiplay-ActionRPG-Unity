#pragma once
#include <string>
#include <boost/asio.hpp>
#include <thread>
#include <iostream>
#include "Packet.h"

class TcpClient
{
public:
	TcpClient(boost::asio::io_context& io_context);

	/// <summary>
	/// 클라이언트가 서버에 연결할 때 사용되는 함수
	/// 비동기 작업을 통해서 연결을 수행합니다.
	/// async_connect의 첫번쨰 인자로는 서버 주소와 포트가 있는 endpoint가 전달되고
	/// 두번쨰 인자로는 연결이 성공적으로 설정된 후 실행할 함수
	/// </summary>
	/// <param name="host"></param>
	/// <param name="port"></param>
	void Connect(std::string host, int port);
	void SendPacket(const std::vector<uint8_t>& data);

private:
	// async_connect 함수의 두번쨰 인자로 전달되는 함수는 연결이 성공적으로 이루어진 후 실행되는 함수
	// 이 함수에서는 서버에서 메시지를 보내는 함수와 서버에서 메시지를 받는 함수를 호출하여
	// 데이터를 비동기적으로 송수신할 예정입니다.
	void OnConnect(const boost::system::error_code& error_code);

	/// <summary>
	/// 비동기적으로 데이터를 쓰기 위해 사용되는 함수
	/// async_write 이 함수의 첫번째 인자는 데이터를 보낼 소켓이고
	/// 두번째 인자는 보낼 데이터가 있는 버퍼
	/// std::string의 경우 크기를 추론해서 결정합니다.
	/// 마지막 세번쨰 인자는 데이터 전송이 성공한 후 호출될 콜백 함수
	/// 이 함수를 통해서 비동기적인 데이터 송신 작업을 할 수 있습니다.
	/// </summary>
	/// <param name="message"></param>
	void AsyncWrite(std::string& message);
	void AsyncWrite(std::shared_ptr<std::vector<uint8_t>> bufferPtr);

	void OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred);

	/// <summary>
	/// 비동기적으로 소켓으로 부터 일부 데이터를 읽어오고
	/// 읽기  작업이 완료되면 사용자가 제공한 콜백 함수 호출하는 데이터 읽기 함수
	/// 첫번쨰 인자로는 데이터를 읽어올 버퍼와 크기가 전달되고
	/// 두번쨰 인자는 읽기 작업이 성공한 후 호출될 함수
	/// 이 함수를 통해서 비동기적으로 소켓에서 데이터를 읽어올 수 있습니다.
	/// </summary>
	void AsyncRead();

	/// <summary>
	/// Read 함수가 성공한 후 호출되는 함수
	/// 해당 소켓과 지속적으로 데이터를 주고 받아야하기 떄문에 AsyncRead를 재호출합니다.
	/// 이후 서버에 Hello World라는 문자열을 보냅니다.
	/// </summary>
	void OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred);

	void StartInput();

	void HandlePacket(PacketType type, const std::vector<uint8_t>& body);

private:
	//boost::asio::ip::tcp::socket m_socket;
	//std::string m_nickName;
	//static const int m_RecvBufferSize = 1024;
	//char m_RecvBuffer[m_RecvBufferSize];
	boost::asio::ip::tcp::socket m_socket;
	PacketHeader m_currentHeader;
	std::vector<uint8_t> m_bodyBuffer;
	char m_headerBuffer[sizeof(PacketHeader)];
	std::string m_nickName;
	bool m_isReady;
};
