FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SocketServer/SocketServer/SocketServer.csproj             SocketServer/SocketServer/
COPY Shared/Shared.Infrastructure/Shared.Infrastructure.csproj Shared/Shared.Infrastructure/
COPY Shared/Shared.Packet/Shared.Packet.csproj                 Shared/Shared.Packet/

RUN dotnet restore SocketServer/SocketServer/SocketServer.csproj

COPY SocketServer/ SocketServer/
COPY Shared/       Shared/

RUN dotnet publish SocketServer/SocketServer/SocketServer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 7777
ENTRYPOINT ["dotnet", "SocketServer.dll"]
