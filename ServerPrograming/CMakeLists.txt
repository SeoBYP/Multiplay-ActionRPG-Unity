cmake_minimum_required(VERSION 3.15)
project(Async_Server)

# C++ 표준 설정 (필요에 따라 조정 가능)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)

# 소스 파일 설정
set(SRC_FILES 
  AsyncServer/AsyncServer.cpp
  AsyncServer/TcpServer.cpp
  AsyncServer/TcpSession.cpp
)

# 실행 파일 생성
add_executable(Async_Server ${SRC_FILES})

# include 디렉토리 추가
target_include_directories(Async_Server PRIVATE include)