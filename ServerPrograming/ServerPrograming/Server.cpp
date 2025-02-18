// EchoServer.cpp : 이 파일에는 'main' 함수가 포함됩니다. 거기서 프로그램 실행이 시작되고 종료됩니다.
//

#include <iostream>
#include <boost/asio.hpp>

using boost::asio::ip::tcp;
using namespace boost;
using namespace std;

int main()
{
    boost::asio::io_context io_context;
    // tcp::v4()는 IPv4 주소 체계를 사용하는 TCP 소켓을 사용하는 활용
    // 현재 해당 코드는 서버로 사용되기 때문에, 해당 함수를 통해 네트워크 인터페이스 카드에서 사용가능한 주소를 자동으로 할당합니다.
    // 이를 통해 서버는 자동으로 사용 가능한 IPv4 주소를 감지하여 소켓을 생성합니다.
    tcp::endpoint endpoint = tcp::endpoint(tcp::v4(), 4242);
    std::cout << "start server" << std::endl;


    while (true)
    {
        // 특정 포트에서 들어오는 연결을 수신할 Accept를 생성
        tcp::acceptor acceptor(io_context, endpoint);
        tcp::socket socket(io_context);

        system::error_code errorCode;

        // 특정 포트에서 들어오는 연결을 수신하고, 해당 연결을 처리할 소켓을 생성하여 전달합니다.
        // acceptor는 클라이언트 연결 요청을 가디라면서, 연결이 들어올때마다 새로운 소켓을 생성하고
        // 해당 소켓을 이용하여 클라이언트와 통신을 처리합니다.
        // 이렇게 수락된 연결은 이후 네트워크 통신에 사용됩니다.
        acceptor.accept(socket, errorCode);
        if (!errorCode)
        {
            std::cout << "accepted" << std::endl;
        }
        else
        {
            std::cout << "aceept Error: " << errorCode.message() << std::endl;
            return 0;
        }

        while (true)
        {
            char recvBuffer[32] = { 0, };

            // 소켓에서 데이터를 읽어와서 지정된 버퍼에 저장하고, 실제로 읽은 바이트수를 반환합니다.
            size_t receivedSize = socket.read_some(boost::asio::buffer(recvBuffer, 32), errorCode);
            if (!errorCode)
            {
                std::cout << "Recv size: " << receivedSize << std::endl;
            }
            else
            {
                std::cout << "Recv Error: " << errorCode.message() << std::endl;
                return 0;
            }

            size_t sentSize = socket.write_some(boost::asio::buffer(recvBuffer, 32), errorCode);
            if (!errorCode)
            {
                std::cout << "Sent size: " << sentSize << std::endl;
            }
            else
            {
                std::cout << "Send Error: " << errorCode.message() << std::endl;
                return 0;
            }
        }
    }
}

