# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Kopiera projektfil och återställ beroenden
COPY ["IntelligentSalesAssistant.ContentEngine/IntelligentSalesAssistant.ContentEngine.csproj", "IntelligentSalesAssistant.ContentEngine/"]
RUN dotnet restore "IntelligentSalesAssistant.ContentEngine/IntelligentSalesAssistant.ContentEngine.csproj"

# Kopiera resten av källkoden och bygg
COPY . .
WORKDIR "/src/IntelligentSalesAssistant.ContentEngine"
RUN dotnet build "IntelligentSalesAssistant.ContentEngine.csproj" -c Release -o /app/build

# Publicera applikationen
FROM build AS publish
RUN dotnet publish "IntelligentSalesAssistant.ContentEngine.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Slutgiltig körningsmiljö
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Tvinga .NET att lyssna på rätt port för rootless containers
ENV ASPNETCORE_URLS=http://+:8080

# Exponera porten för Content Engine
EXPOSE 8080

ENTRYPOINT ["dotnet", "IntelligentSalesAssistant.ContentEngine.dll"]