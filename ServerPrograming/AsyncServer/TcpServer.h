#pragma once

#include <iostream>
#include <boost/asio.hpp>


class TcpSession;
using boost::asio::ip::tcp;
using namespace boost;
using namespace std;

class TcpServer
{
public:
	TcpServer(asio::io_context& io_context, int port);
	void StartAccept();
	void broad_cast(char* message, size_t message_size);
	void broad_cast(string& message);
	void send_whisper(const string& nickname, char* message, size_t message_size);
	void send_whisper(const string& nickname, string& message);
	void OnAccept(TcpSession* session, const system::error_code& error_code);

private:
	tcp::acceptor m_acceptor;
	asio::io_context& m_io_context;
	vector<TcpSession*> m_sessions;
};