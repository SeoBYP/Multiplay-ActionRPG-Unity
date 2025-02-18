// EchoClient.cpp : 이 파일에는 'main' 함수가 포함됩니다. 거기서 프로그램 실행이 시작되고 종료됩니다.
//

#include <iostream>
#include <boost/asio.hpp>
#include <thread>
#include <vector>
// Asio를 사용하기 위해서는 다음과 같이 헤더 파일을 포함해야 됩니다.
// Boost 라이브러리의 경우, 경우에 따라 Cpp 파일을 가져와서 CMake해야 되는 경우도 있으나
// Asio같은 경우는 헤더 파일만 가져오면 쓸 수 있습니다.

int main()
{
	// Asio 라이브러리에서 사용되는 중요한 객체, 주로 I/O 작업을 관리하는데 활용됩니다.
	// io_context를 통해서 네트워크 통신, 파일 I/O 등과 같은 작업을 효율적으로 처리할 수 있습니다.
	boost::asio::io_context io_contex;


	// TCP 소켓을 생성하는 부분으로 I/O 작업 처리를 하기 위해 io_context를 사용합니다.
	boost::asio::ip::tcp::socket socket(io_contex);


	// 작업중에 발생한 오류 정보를 안전하게 처리하기 위한 수단.
	// 이 객체는 함수 호출 결과를 통해 오류 여부와 세부적인 오류 코드를 확인하는데 사용합니다.
	// 안전하게 에러 처리를하고 적절하게 대응하기 위함
	boost::system::error_code errorCode;

	// 특정 서비스에 접속하기 위한 정보를 정의
	// 네트워크 통신에서 사용되며, 호스트 IP 주소와 포트 번호를 결합한 형태로 서버를 나타냅니다.
	// boost::asio::ip::tcp::endpoint endpoint(boost::asio::ip::make_address("127.0.0.1", errorCode), 2529);

	boost::asio::ip::tcp::endpoint endpoint(boost::asio::ip::make_address("127.0.0.1", errorCode), 4242);
	if (errorCode)
	{
		std::cout << "endpoint Error: " << errorCode.message() << std::endl;
		return 0;
	}

	// endpoint를 이용하여 대상에게 접속을 요청하는 부분
	socket.connect(endpoint, errorCode);

	// 에러코드를 활용해서 작업이 정상적으로 진행됐는지 확인하는 부분
	if (!errorCode)
	{
		std::cout << "Connected" << std::endl;
	}
	else
	{
		std::cout << "Connected Error: " << errorCode.message() << std::endl;
		return 0;
	}

	// 주어진 데이터를 소켓으로 전송하고, 전송된 바이트수를 반환합니다.
	// boost::asio::buffer는 데이터의 주소와 크기를 Asio 라이브러리에서 사용할 수 있는 형태로 변환하는 역할
	std::string request = "Hello World";
	socket.write_some(boost::asio::buffer(request.data(), request.size()), errorCode);
	if (errorCode)
	{
		std::cout << "write_some Error: " << errorCode.message() << std::endl;
		return 0;
	}

	std::this_thread::sleep_for(std::chrono::seconds(5));

	// 소켓의 입력 버퍼에서 현재 읽을 수 있는 데이터양을 반환하는 함수
	// 이 함수를 호출하면 소켓의 입력 버퍼에 현재 얼마나 많은 데이터가 있는지 확인할 수 있습니다.
	size_t  size = socket.available();
	std::cout << "read available: " << size << std::endl;
	std::vector<char> recvBuffer(size);

	// 소켓에서 데이터를 읽어와서 지정된 버퍼에 저장하고, 실제로 읽은 바이트수를 반환합니다.
	socket.read_some(boost::asio::buffer(recvBuffer.data(), recvBuffer.size()), errorCode);

	for (auto c : recvBuffer)
	{
		std::cout << c;
	}

	return 0;
}


// Docker desktop 회원가입 및 설치