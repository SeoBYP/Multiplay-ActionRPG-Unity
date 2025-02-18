
#include <iostream>

#include "TcpServer.h"


int main()
{
	boost::asio::io_context io_context;
	TcpServer server(io_context,4242);
	server.StartAccept();

	std::cout << "Server Start" << std::endl;

	// 비동기 I/O 작업이 처리된 후에 호출되는 함수를 실행시켜주는 함수
	// 이 함수는 블록함수로 새로운 비동기 작업이 등록되거나 완료될 떄까지 기다립니다.
	io_context.run();
	return 0;
}
