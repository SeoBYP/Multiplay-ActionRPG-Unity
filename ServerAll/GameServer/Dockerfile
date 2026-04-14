FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj만 먼저 복사 → restore 레이어 캐시 (소스 안 바뀌면 restore 재실행 안 함)
COPY GameServer/GameServer.API/GameServer.API.csproj                       GameServer/GameServer.API/
COPY GameServer/GameServer.Application/GameServer.Application.csproj       GameServer/GameServer.Application/
COPY GameServer/GameServer.Domain/GameServer.Domain.csproj                 GameServer/GameServer.Domain/
COPY GameServer/GameServer.Infrastructure/GameServer.Infrastructure.csproj GameServer/GameServer.Infrastructure/
COPY Shared/Shared.Contracts/Shared.Contracts.csproj                       Shared/Shared.Contracts/
COPY Shared/Shared.Infrastructure/Shared.Infrastructure.csproj             Shared/Shared.Infrastructure/
COPY Shared/Shared.Packet/Shared.Packet.csproj                             Shared/Shared.Packet/

RUN dotnet restore GameServer/GameServer.API/GameServer.API.csproj

# 소스 전체 복사 후 publish
COPY GameServer/ GameServer/
COPY Shared/     Shared/

# SKIP_CODEGEN=true → AfterBuild에서 ClientCodegen 실행 안 함
# Docker 안에는 Unity 프로젝트 경로가 없으므로 실행하면 에러남
RUN dotnet publish GameServer/GameServer.API/GameServer.API.csproj \
    -c Release -o /app/publish --no-restore \
    -p:SKIP_CODEGEN=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5131 5132
ENTRYPOINT ["dotnet", "GameServer.API.dll"]