# Use the official Ubuntu image as the base image
FROM ubuntu:latest

# Set the working directory in the container
WORKDIR /app

# Install necessary dependencies
RUN apt-get update && apt-get install -y \
    cmake \
    g++ \
    vim \
    libboost-all-dev \
    openssh-server  # OpenSSH 명확하게 설치

RUN apt-get clean && rm -rf /var/lib/apt/lists/*

# SSH 설정 (설정 파일이 있는지 확인 후 변경)
RUN test -f /etc/ssh/sshd_config || (echo "Error: /etc/ssh/sshd_config not found!" && exit 1)
RUN sed -i 's/#PermitRootLogin prohibit-password/PermitRootLogin yes/' /etc/ssh/sshd_config
RUN sed -i 's/#PasswordAuthentication yes/PasswordAuthentication yes/' /etc/ssh/sshd_config

# 4️⃣ SSH 서비스 실행을 위한 디렉토리 생성
RUN mkdir -p /var/run/sshd

# 5️⃣ root 비밀번호 설정
RUN echo 'root:root' | chpasswd

# C++ 소스 코드 복사
COPY . /app

# CMake 빌드 디렉터리 생성
RUN rm -rf build && mkdir -p build && cd build \
    && cmake .. \
    && make

# 실행 권한 추가 (별도 RUN 명령어 사용)
RUN chmod +x /app/build/Async_Server

# 포트 개방
EXPOSE 4242 22

# 8️⃣ 실행 파일이 있는지 확인 후 실행
CMD ["/app/build/Async_Server"]
