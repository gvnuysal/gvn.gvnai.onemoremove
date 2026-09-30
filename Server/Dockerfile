# Build from the repository root (the server compiles the game's Core/Session/Persistence sources):
#   docker build -f Server/Dockerfile -t onemoremove-server .
#   docker run -p 8080:8080 -v omm-data:/data -e Server__SigningKey=... -e Server__AdminApiKey=... onemoremove-server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NuGet.Config ./
COPY Server/Directory.Build.props Server/
COPY Server/OneMoreMove.Server/ Server/OneMoreMove.Server/
COPY Assets/_Project/Scripts/Core/ Assets/_Project/Scripts/Core/
COPY Assets/_Project/Scripts/Session/ Assets/_Project/Scripts/Session/
COPY Assets/_Project/Scripts/Persistence/ Assets/_Project/Scripts/Persistence/
RUN dotnet publish Server/OneMoreMove.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY Assets/_Project/Content/LevelSource/*.json levels/
ENV ASPNETCORE_URLS=http://+:8080 \
    Server__Database__ConnectionString="Data Source=/data/onemoremove.db"
RUN mkdir -p /data && chown app /data
VOLUME /data
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "OneMoreMove.Server.dll"]
