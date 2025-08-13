# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files and restore dependencies
COPY TsDiscordBot.Core/TsDiscordBot.Core.csproj TsDiscordBot.Core/
COPY TsDiscordBot.Entry/TsDiscordBot.Entry.csproj TsDiscordBot.Entry/
RUN dotnet restore TsDiscordBot.Entry/TsDiscordBot.Entry.csproj

# Copy the remaining source code and publish the application
COPY . .
RUN dotnet publish TsDiscordBot.Entry/TsDiscordBot.Entry.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TsDiscordBot.Entry.dll"]
