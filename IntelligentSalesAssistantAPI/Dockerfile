# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Kopiera projektfil och återställ beroenden
COPY ["IntelligentSalesAssistantAPI/IntelligentSalesAssistantAPI.csproj", "IntelligentSalesAssistantAPI/"]
RUN dotnet restore "IntelligentSalesAssistantAPI/IntelligentSalesAssistantAPI.csproj"

# Kopiera resten av källkoden och bygg
COPY . .
WORKDIR "/src/IntelligentSalesAssistantAPI"
RUN dotnet build "IntelligentSalesAssistantAPI.csproj" -c Release -o /app/build

# Publicera applikationen
FROM build AS publish
RUN dotnet publish "IntelligentSalesAssistantAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Slutgiltig körningsmiljö
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Tvinga .NET att lyssna på rätt port för rootless containers
ENV ASPNETCORE_URLS=http://+:8080

# Exponera porten för API:et
EXPOSE 8080

ENTRYPOINT ["dotnet", "IntelligentSalesAssistantAPI.dll"]