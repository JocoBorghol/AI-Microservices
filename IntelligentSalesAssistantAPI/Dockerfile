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
# Skapa mappar för databas och genererat material under byggskedet
RUN mkdir -p /app/publish/Data && mkdir -p /app/Site/generated && mkdir -p /app/Site/drafts

# Slutgiltig körningsmiljö
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS final
WORKDIR /app
# Kopiera filer och tilldela ägarskap till app-användaren (UID 1654)
COPY --from=publish --chown=app:app /app/publish .
COPY --from=build --chown=app:app /src/Site /Site

# Tvinga .NET att lyssna på rätt port för rootless containers
ENV ASPNETCORE_URLS=http://+:8080

# Exponera porten för API:et
EXPOSE 8080

# Kör som icke-root
USER app

ENTRYPOINT ["dotnet", "IntelligentSalesAssistantAPI.dll"]