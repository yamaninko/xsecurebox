FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SecureBox.API/SecureBox.API.csproj", "SecureBox.API/"]
COPY ["SecureBox.Core/SecureBox.Core.csproj", "SecureBox.Core/"]
COPY ["SecureBox.Infrastructure/SecureBox.Infrastructure.csproj", "SecureBox.Infrastructure/"]
RUN dotnet restore "SecureBox.API/SecureBox.API.csproj"
COPY . .
WORKDIR "/src/SecureBox.API"
RUN dotnet build "SecureBox.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SecureBox.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "SecureBox.API.dll"]

