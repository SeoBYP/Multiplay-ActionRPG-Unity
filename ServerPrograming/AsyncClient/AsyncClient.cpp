
#include <iostream>
#include <boost/assert/source_location.hpp>
#include <boost/asio.hpp>

class TcpClient
{
public:
	TcpClient(boost::asio::io_context& io_context)
		: m_socket(io_context)
	{
		
	}

	// 클라이언트가 서버에 연결할 때 사용되는 함수
	// 비동기 작업을 통해서 연결을 수행합니다.
	// async_connect의 첫번쨰 인자로는 서버 주소와 포트가 있는 endpoint가 전달되고
	// 두번쨰 인자로는 연결이 성공적으로 설정된 후 실행할 함수
	void Connect(std::string host, int port)
	{
		const boost::asio::ip::tcp::endpoint endpoint(boost::asio::ip::make_address(host), port);
		m_socket.async_connect(endpoint, [this](const boost::system::error_code& errorCode) {
			this->OnConnect(errorCode);
		});
	}



private:
	// async_connect 함수의 두번쨰 인자로 전달되는 함수는 연결이 성공적으로 이루어진 후 실행되는 함수
	// 이 함수에서는 서버에서 메시지를 보내는 함수와 서버에서 메시지를 받는 함수를 호출하여
	// 데이터를 비동기적으로 송수신할 예정입니다.
	void OnConnect(const boost::system::error_code& error_code)
	{
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

	/// <summary>
	/// 비동기적으로 데이터를 쓰기 위해 사용되는 함수
	/// async_write 이 함수의 첫번째 인자는 데이터를 보낼 소켓이고
	/// 두번째 인자는 보낼 데이터가 있는 버퍼
	/// std::string의 경우 크기를 추론해서 결정합니다.
	/// 마지막 세번쨰 인자는 데이터 전송이 성공한 후 호출될 콜백 함수
	/// 이 함수를 통해서 비동기적인 데이터 송신 작업을 할 수 있습니다.
	/// </summary>
	/// <param name="message"></param>
	void AsyncWrite(std::string message)
	{
		m_sendMessage = message;
		boost::asio::async_write(m_socket, boost::asio::buffer(m_sendMessage),
			[this](const boost::system::error_code& errorCode,
				const size_t bytesTransferred)
			{
				this->OnWrite(errorCode, bytesTransferred);
			});
	}

	void OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred)
	{
		if (!errorCode)
		{
			
		}
		else
		{
			std::cout << "Error: " << errorCode.message() << std::endl;
		}
	}

	/// <summary>
	/// 비동기적으로 소켓으로 부터 일부 데이터를 읽어오고
	/// 읽기  작업이 완료되면 사용자가 제공한 콜백 함수 호출하는 데이터 읽기 함수
	/// 첫번쨰 인자로는 데이터를 읽어올 버퍼와 크기가 전달되고
	/// 두번쨰 인자는 읽기 작업이 성공한 후 호출될 함수
	/// 이 함수를 통해서 비동기적으로 소켓에서 데이터를 읽어올 수 있습니다.
	/// </summary>
	void AsyncRead()
	{
		m_socket.async_read_some(boost::asio::buffer(m_RecvBuffer, m_RecvBufferSize), 
			[this](const boost::system::error_code& errorCode,
				const size_t bytesTransferred)
			{
				this->OnRead(errorCode, bytesTransferred);
			});
	}

	/// <summary>
	/// Read 함수가 성공한 후 호출되는 함수
	/// 해당 소켓과 지속적으로 데이터를 주고 받아야하기 떄문에 AsyncRead를 재호출합니다.
	/// 이후 서버에 Hello World라는 문자열을 보냅니다.
	/// </summary>
	void OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred)
	{
		if (!errorCode)
		{
			std::cout << std::string(m_RecvBuffer) << std::endl;
			memset(m_RecvBuffer, 0, m_RecvBufferSize);
			AsyncRead();
		}
		else
		{
			std::cout << "Error: " << errorCode.message() << std::endl;
		}
	}

	void InputText()
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
				AsyncWrite(message);
			}
		}
	}

private:
	boost::asio::ip::tcp::socket m_socket;
	std::string m_nickName;
	std::string m_sendMessage;
	static const int m_RecvBufferSize = 1024;
	char m_RecvBuffer[m_RecvBufferSize];
};

int main()
{
	boost::asio::io_context io_context;
	TcpClient client(io_context);
	client.Connect("127.0.0.1", 4242);

	// 비동기 I/O 작업이 처리된 후에 호출되는 함수를 실행시켜주는 함수
	// 이 함수는 블록함수로 새로운 비동기 작업이 등록되거나 완료될 떄까지 기다립니다.
	io_context.run();
	return 0;
}


// 숙제
// 샘플 비동기 에코 서버가 있습니다.
// 이걸로 채팅 서버로 만들어보세요
// 브로드캐스팅이 되어야 합니다.

// 심화
// 채팅에 입장하면 닉네임 입력을 받습니다.
// 이후에 채팅을 하면 닉네임 : 채팅내용으로 나오게 됩니다.
// 귓속말 기능
// /w 닉네임 내용으로 입력하면 해당 닉네임을 사용하는 유저에게만 메시지가 전달되게 됩니다.