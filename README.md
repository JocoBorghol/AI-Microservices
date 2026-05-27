# Intelligent Sales Assistant Platform

[Svenska](README.md) | [English](README.en.md) | [Enkel version](README.simple.md) | [Portfolio](README.portfolio.md)

> En distribuerad mikrotjänstplattform för AI-baserad generering av webbplatser och säljmaterial

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Architecture](https://img.shields.io/badge/architecture-microservices-green.svg)](docs)

---

## 🚀 Översikt

Intelligent Sales Assistant Platform är ett produktionsklart mikrotjänstsystem som automatiserar säljflöden genom AI-driven innehållsgenerering. Plattformen hämtar företagsdata i realtid från svenska företagsregister och använder Google Gemini AI för att skapa professionella webbplatser och marknadsföringsmaterial.

**Kärnfunktioner:**
- Automatiserad företagsresearch via BolagsAPI (Svenska företagsregistret)
- AI-driven generering av webbplatser med anpassningsbara teman
- Skapande av marknadsföringsmaterial (sociala medier, e-post, nyhetsbrev)
- RESTful API med fullständig OpenAPI-dokumentation
- JWT-autentisering och rollbaserad behörighetskontroll

---

## 🏗️ Arkitektur

Plattformen implementerar en **mikrotjänstarkitektur** med två oberoende tjänster som kommunicerar via HTTP:

```
┌─────────────────────────────────────────────────────────────┐
│           IntelligentSalesAssistantAPI (Port 5267)          │
│                 Kärn-API & Affärslogik                      │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  Företags-   │  │  Webbplats-  │  │  Innehålls-  │       │
│  │  research    │  │  generering  │  │   utkast     │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
│         │                  │                  │             │
│         └──────────────────┴──────────────────┘             │
│                            │                                │
│                   ┌────────▼────────┐                       │
│                   │  LlmProxyClient │                       │
│                   │  (Typed Client) │                       │
│                   └────────┬────────┘                       │
└────────────────────────────┼────────────────────────────────┘
                             │ HTTPS + API-nyckel
                             │ (Proxy-mönster)
┌────────────────────────────▼────────────────────────────────┐
│    IntelligentSalesAssistant.ContentEngine (Port 5006)      │
│                      AI Content Engine                      │
│                                                             │
│                   ┌────────────────┐                        │
│                   │ GeminiClient   │                        │
│                   │  (Typed HTTP)  │                        │
│                   └────────┬───────┘                        │
│                            │                                │
│                   ┌────────▼───────┐                        │
│                   │  Gemini API    │                        │
│                   │  (Google AI)   │                        │
│                   └────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

### IntelligentSalesAssistantAPI
- **Ansvarsområde:** Affärslogik, datasamordning, användarautentisering
- **Teknologi:** ASP.NET Core Web API, Entity Framework Core, SQLite
- **Endpoints:** Företagsresearch, webbplatsgenerering, innehållsutkast
- **Säkerhet:** JWT-autentisering med rollerna Admin och Seller

### IntelligentSalesAssistant.ContentEngine
- **Ansvarsområde:** Proxy för AI-innehållsgenerering
- **Teknologi:** ASP.NET Core Web API, Google Gemini API-integration
- **Endpoints:** Textgenerering, strukturerat webbplatsinnehåll
- **Säkerhet:** API-nyckelvalidering för kommunikation mellan tjänster

---

## ✨ Kärnfunktioner

### Automatiserad företagsresearch
- Hämtar realtidsdata från BolagsAPI (Svenska företagsregistret)
- Cachas i minnet för den aktuella sessionen
- Tillhandahåller strukturerad företagsinformation (namn, organisationsnummer, adress, bransch)

### AI-driven webbplatsgenerering
- Genererar kompletta HTML/CSS/JavaScript-webbplatser
- Anpassningsbar ton (professionell, vänlig, djärv) och målgrupp
- Responsiv design med mobilfokuserade mallar
- Webbplatser sparas i `Site/generated/{company-name}/index.html`

### Skapande av marknadsföringsmaterial
- Skapar inlägg för sociala medier (Facebook, Instagram, LinkedIn)
- Genererar e-postmeddelanden, blogginlägg och nyhetsbrev
- Innehållet refererar till genererade webbplatser för konsekvent tonalitet
- Utkast sparas i `Site/drafts/{company-name}/{type}_{timestamp}.txt`

### Optimerad JSON-arkitektur (Lean Payload)
Kommunikationen mellan tjänsterna är optimerad för hög prestanda:
- **IntelligentSalesAssistant.ContentEngine** returnerar strukturerad JSON (3-5 KB)
- **IntelligentSalesAssistantAPI** bygger HTML lokalt utifrån mallar
- **Resultat:** 10-20x mindre datamängd över nätverket jämfört med att skicka hela HTML-filer

**Fördelar:**
- Minskad nätverksbelastning och snabbare svarstider
- Lägre bandbreddskostnader i molnet
- Tydlig ansvarsfördelning (AI-intelligens kontra presentation)
- Oberoende skalning baserat på faktisk belastning på respektive tjänst

---

## 🛠️ Teknikstack

| Kategori | Teknologi |
|----------|-----------|
| **Ramverk** | .NET 9.0 |
| **Programspråk** | C# 12 |
| **API** | ASP.NET Core Web API |
| **ORM** | Entity Framework Core |
| **Databas** | SQLite |
| **HTTP-klient** | IHttpClientFactory med Typed Clients |
| **Resiliens** | Polly (Retry, Circuit Breaker, Timeout) |
| **Autentisering** | JWT Bearer Tokens |
| **API-dokumentation** | Scalar (OpenAPI 3.0) |
| **AI-leverantör** | Google Gemini API (gemini-3.1-flash-lite-preview) |
| **Externa API:er** | BolagsAPI (Svenska företagsregistret) |

---

## 📦 Komma igång

### Förutsättningar

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) eller senare
- [Git](https://git-scm.com/)
- API-nycklar:
  - [Google Gemini API-nyckel](https://ai.google.dev/)
  - [BolagsAPI-nyckel](https://bolagsapi.se/)

### Installation

1. **Klona arkivet**
   ```bash
   git clone https://github.com/yourusername/intelligent-sales-assistant.git
   cd intelligent-sales-assistant
   ```

2. **Återställ NuGet-paket**
   ```bash
   dotnet restore
   ```
   Detta säkerställer att alla beroenden laddas ner lokalt före konfigurationen.

3. **Konfigurera User Secrets för IntelligentSalesAssistantAPI**
   ```bash
   cd IntelligentSalesAssistantAPI
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "din-superhemliga-jwt-nyckel-minst-32-tecken"
   dotnet user-secrets set "AdminPassword" "ditt-admin-lösenord"
   dotnet user-secrets set "SellerPassword" "ditt-säljar-lösenord"
   dotnet user-secrets set "BolagsApi:ApiKey" "din-bolagsapi-nyckel"
   dotnet user-secrets set "LlmProxySettings:ApiKey" "din-interna-api-nyckel-för-service-b"
   ```

4. **Konfigurera User Secrets för IntelligentSalesAssistant.ContentEngine**
   ```bash
   cd ../IntelligentSalesAssistant.ContentEngine
   dotnet user-secrets init
   dotnet user-secrets set "GeminiSettings:ApiKey" "din-gemini-api-nyckel"
   dotnet user-secrets set "ServiceAuth:ApiKey" "din-interna-api-nyckel-för-service-b"
   ```
   
   > **Obs:** Värdet för `ServiceAuth:ApiKey` i ContentEngine måste matcha `LlmProxySettings:ApiKey` i IntelligentSalesAssistantAPI.
   
   > **AI-integration:** `GeminiSettings:ApiKey` krävs för att AI Content Engine ska kunna kommunicera med Google Gemini API. Utan denna nyckel kan plattformen inte generera texter.

5. **Tillämpa databasmigreringar**
   ```bash
   cd ../IntelligentSalesAssistantAPI
   dotnet ef database update
   ```

### Köra tjänsterna

**Terminal 1 - IntelligentSalesAssistantAPI:**
```bash
cd IntelligentSalesAssistantAPI
dotnet run
```
Tjänsten startar på `http://localhost:5267`

**Terminal 2 - IntelligentSalesAssistant.ContentEngine:**
```bash
cd IntelligentSalesAssistant.ContentEngine
dotnet run
```
Tjänsten startar på `http://localhost:5006`

### Köra via Docker Compose (Alternativ)
```bash
# Bygg och starta båda mikrotjänsterna i bakgrunden från rotmappen
docker-compose up -d --build
```

### Verifiera installationen

- **IntelligentSalesAssistantAPI:** `http://localhost:5267/scalar/v1`
- **IntelligentSalesAssistant.ContentEngine:** `http://localhost:5006/scalar/v1`

---

## 🎯 Snabbguide (Quick Start)

### 1. Autentisera dig

```bash
curl -X POST http://localhost:5267/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "ditt-admin-lösenord"
  }'
```

Spara den returnerade JWT-token.

### 2. Gör research på ett företag

```bash
curl -X POST http://localhost:5267/api/research \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer DIN_JWT_TOKEN" \
  -d '{
    "orgNumber": "5565093902"
  }'
```

Detta hämtar företagsdata från BolagsAPI och sparar det i minnescachen.

### 3. Generera en webbplats

```bash
curl -X POST http://localhost:5267/api/websites \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer DIN_JWT_TOKEN" \
  -d '{
    "customization": {
      "tone": "professional and welcoming",
      "targetAudience": "families with children",
      "topServices": ["Candy", "Chocolate", "Gift Cards"],
      "keywords": ["quality", "tradition", "joy"]
    }
  }'
```

### 4. Visa den genererade webbplatsen

Öppna webbadressen från svaret:
```
http://localhost:5267/generated/kandyz-ab/index.html
```

### 5. Skapa marknadsföringsmaterial

```bash
curl -X POST http://localhost:5267/api/content/drafts \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer DIN_JWT_TOKEN" \
  -d '{
    "contentType": "facebook_post",
    "instructions": "Create a fun post about our summer opening hours",
    "tone": "casual",
    "websiteId": 1
  }'
```

---

## 📚 API-dokumentation

### IntelligentSalesAssistantAPI Endpoints

| Endpoint | Metod | Beskrivning |
|----------|-------|-------------|
| `/api/auth/login` | POST | Generera JWT-token |
| `/api/research` | POST | Hämta företagsdata från BolagsAPI |
| `/api/research/cache` | GET | Hämta cachad företagsdata |
| `/api/research/cache` | DELETE | Rensa cachad företagsdata |
| `/api/websites` | GET | Lista alla genererade webbplatser |
| `/api/websites` | POST | Generera en ny webbplats |
| `/api/websites/{id}` | GET | Hämta webbplatsinformation |
| `/api/websites/{id}` | PUT | Regenerera webbplats |
| `/api/websites/{id}` | DELETE | Ta bort webbplats |
| `/api/content/drafts` | POST | Skapa innehållsutkast (kräver websiteId) |
| `/api/content/drafts` | GET | Lista alla utkast |
| `/api/content/drafts/{id}` | GET | Hämta specifikt utkast |
| `/api/content/drafts/{id}` | DELETE | Ta bort utkast |

### IntelligentSalesAssistant.ContentEngine Endpoints

| Endpoint | Metod | Beskrivning |
|----------|-------|-------------|
| `/api/content/generate` | POST | Generera AI-textinnehåll |
| `/api/content/websites` | POST | Generera strukturerat webbplatsinnehåll |

Scalar interaktiv dokumentation finns tillgänglig på `http://localhost:5267/scalar/v1` när tjänsterna körs.

---

## 🛡️ Exception Middleware (Felhantering)

Plattformen implementerar en centraliserad Custom Exception Middleware för robust felhantering och säkerhet. Istället för råa systemstackspår fångar denna middleware upp alla fel och omvandlar dem till standardiserade **RFC 7807 Problem Details**-svar.

**Arkitektur:** Middlewareregistreringen sker i `Program.cs`. Den använder en `try-catch`-struktur runt `next(context)`-delegaten vilket säkerställer att alla undantag som uppstår under en request fångas upp, loggas och mappas till ett strukturerat `ProblemDetails`-svar via klassen `Microsoft.AspNetCore.Mvc.ProblemDetails`.

**Viktiga fördelar:**
- **Säkerhet:** Förhindrar att känslig systeminformation läcks till klienten
- **Konsekvens:** Ger ett enhetligt felformat för alla mikrotjänster
- **Tydlighet:** Mappar specifika domänfel (t.ex. `FileOperationException`, `CompanyNotFoundException`) till korrekta HTTP-statuskoder

**Så här testar och verifierar du:**
1. Autentisera dig via `/api/auth/login` för att erhålla en JWT-token
2. Anropa `POST /api/research` med ett ogiltigt eller icke-existerande organisationsnummer (t.ex. `"000"`)
3. Kontrollera svaret: API:et returnerar ett strukturerat JSON-objekt med fälten `title`, `status` och `detail` istället för en ostrukturerad felsida

**Exempel på felsvar:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Företag hittades inte",
  "status": 404,
  "detail": "Företag med organisationsnummer 000 hittades inte i BolagsAPI"
}
```

---

## 🔒 Säkerhet

### Autentiseringsflöde
1. Användaren loggar in via `/api/auth/login` och får en JWT-token
2. JWT-token skickas med i headern `Authorization: Bearer {token}` för efterföljande anrop
3. IntelligentSalesAssistantAPI validerar JWT-token och utvinner användaridentiteten
4. IntelligentSalesAssistantAPI bifogar en intern API-nyckel vid anrop till ContentEngine
5. ContentEngine validerar API-nyckeln innan förfrågan behandlas

### Säkerhetsfunktioner
- JWT-tokens med 2 timmars giltighetstid
- Rollbaserad behörighet (Admin och Seller)
- API-nyckelvalidering för kommunikation mellan mikrotjänster
- Indatavalidering med Data Annotations
- SQL-injektionsskydd via Entity Framework Core
- Rate limiting (10 anrop/minut per endpoint)

### Så här sätter du API-nyckeln lokalt (User Secrets)
Under lokal utveckling lagras alla känsliga nycklar utanför projektmappen med hjälp av .NET User Secrets för att förhindra att de oavsiktligt checkas in i Git.
1. Gå till API-projektets katalog:
   ```bash
   cd IntelligentSalesAssistantAPI
   ```
2. Sätt API-nyckeln för kommunikation med Service B (Content Engine):
   ```bash
   dotnet user-secrets set "LlmProxySettings:ApiKey" "din_lokala_hemliga_nyckel"
   ```
Denna nyckel läses automatiskt in i `IConfiguration` under utveckling via det unika `UserSecretsId` i `.csproj`.

### Så här sätter du API-nyckeln i produktion (Environment Variables)
Applikationen är driftsatt i den delade miljön env-joco-inventory i resursgruppen rg-isa-prod.

Känsliga nycklar binds som miljövariabler, där t.ex. LlmProxySettings__ApiKey mappar mot LlmProxySettings:ApiKey.

Cost Optimization (Scale to Zero): Båda applikationerna är konfigurerade med min-replicas: 0 och max-replicas: 3. Detta innebär att behållarna automatiskt skalar ner till 0 instanser vid inaktivitet för att helt eliminera rullande molnkostnader.

### CI/CD via GitHub Actions
Pipelinen (.github/workflows/deploy.yml) bygger, testar och driftsätter båda mikrotjänsterna automatiskt vid pull requests och push-händelser till dev och main-brancherna. För att detta ska fungera behöver du:
1. Skapa en Service Principal i Azure CLI:
   ```bash
   az ad-sp create-for-rbac --name "github-actions-deploy" --role contributor --scopes /subscriptions/fdd80a6b-225f-4078-a232-5c3272145e4c/resourceGroups/rg-isa-prod --sdk-auth
   ```
2. Kopiera den resulterande JSON-koden.
3. Gå till ditt GitHub-arkiv: **Settings > Secrets and variables > Actions**.
4. Skapa en ny secret med namnet `AZURE_CREDENTIALS` och klistra in JSON-koden.

### Skriftlig säkerhetsgaranti
Härmed intygas och garanteras att:
- Inga råa API-nycklar eller hemligheter är eller kommer att checkas in i Git-arkivet (all lokal konfiguration sker via User Secrets eller miljöspecifika platshållare).
- Alla API-klienter och HTTP-handlers (`ApiKeyHandler`, `BolagsApiAuthHandler`) samt vår globala middleware (`CustomExceptionMiddleware`) är manuellt och programmatiskt verifierade att **aldrig** logga känsliga HTTP-headers (t.ex. `Authorization`, `X-Api-Key`) eller råa request-kroppar innehållande användardata och tokens. Endast säkra, anonymiserade felmeddelanden loggas i systemet.

---

## 📁 Projektstruktur

```
intelligent-sales-assistant/
├── IntelligentSalesAssistantAPI/             # Huvud-API
│   ├── Controllers/                          # API-Controllers
│   │   ├── AuthController.cs                 # JWT-Autentisering
│   │   ├── CompanyResearchController.cs      # BolagsAPI-integration
│   │   ├── WebsiteGeneratorController.cs     # Webbplatsgenerering
│   │   └── ContentDraftController.cs         # Innehållsutkast
│   ├── Services/                             # Affärslogik
│   │   ├── Enrichment/                       # Företagsresearch
│   │   ├── WebsiteGenerator/                 # Webbplatsgenerering
│   │   └── ContentDraft/                     # Innehållsutkast
│   ├── Http/Clients/                         # Typed HTTP-klienter
│   ├── DTOs/                                 # Data Transfer Objects
│   ├── Data/                                 # Databaskontext
│   ├── Models/                               # Entitetsmodeller
│   │   └── CompanyWebsite.cs                 # Webbplatsentitet
│   ├── Middleware/                           # Custom Middleware
│   ├── Filters/                              # Action Filters
│   ├── Exceptions/                           # Custom Exceptions
│   ├── Migrations/                           # EF Core-migreringar
│   ├── ServiceA.db                           # SQLite-databas
│   └── Program.cs                            # Applikationens startpunkt
│
├── IntelligentSalesAssistant.ContentEngine/  # AI Content Engine (Service B)
│   ├── Controllers/                          # API-Controllers
│   │   └── ContentController.cs              # AI-textgenerering
│   ├── ApiClients/                           # Gemini-klient
│   │   ├── GeminiClient.cs                   # Gemini API-integration
│   │   └── IGeminiClient.cs                  # Interface
│   ├── Security/                             # API-nyckelvalidering
│   │   └── RequireApiKeyAttribute.cs         # API-nyckelfilter
│   ├── Middleware/                           # Custom Middleware
│   ├── DTOs/                                 # Data Transfer Objects
│   └── Program.cs                            # Applikationens startpunkt
│
├── Site/                                     # Genererat innehåll
│   ├── template/                             # Webbplatsmallar
│   │   └── index.html                        # Basmall
│   ├── generated/                            # Genererade webbplatser
│   │   └── {company-name}/                   # Företagsspecifika mappar
│   │       └── index.html                    # Genererad webbsida
│   └── drafts/                               # Innehållsutkast
│       └── {company-name}/                   # Företagsspecifika mappar
│           └── {type}_{timestamp}.txt        # Utkastsfiler
│
└── README.md                                 # Denna fil
```

---

## 🧪 Testning

### Manuell testning med Scalar

1. Starta båda tjänsterna
2. Gå till `http://localhost:5267/scalar/v1`
3. Klicka på "Authorize" och fyll i din JWT-token
4. Testa endpoints direkt via det interaktiva gränssnittet

### Exempel på testflöde

**Komplett generering av hemsida:**
1. POST `/api/auth/login` - Autentisera och hämta JWT-token
2. POST `/api/research` - Hämta företagsdata (sparas i cache)
3. POST `/api/websites` - Generera webbplats baserad på cache
4. GET `/api/websites` - Lista alla genererade webbplatser
5. Öppna den genererade webbplatsen i en webbläsare

**Skapa innehållsutkast:**
1. POST `/api/auth/login` - Autentisera
2. POST `/api/research` - Hämta företagsdata
3. POST `/api/websites` - Generera webbplats
4. POST `/api/content/drafts` - Skapa utkast (skicka med websiteId från steg 3)
5. GET `/api/content/drafts/{id}` - Visa det genererade utkastet

---

## 🤝 Bidra till projektet

Detta är ett portföljprojekt för att demonstrera mikrotjänstarkitektur och AI-integration. Feedback och förbättringsförslag är varmt välkomna!

---

## 👤 Författare

**Joco Borghol**
- LinkedIn: [linkedin.com/in/joco-borghol-777b59386](https://www.linkedin.com/in/joco-borghol-777b59386)
- GitHub: [@JocoBorghol](https://github.com/JocoBorghol)

---

## 🙏 Tack till

- **Google Gemini AI** - AI-genererat innehåll
- **BolagsAPI** - Företagsdata i realtid
- **Scalar** - Interaktiv API-dokumentation
- **Polly** - Resiliens och transientfelhantering

---

<div align="center">

**Byggd med .NET 9 och modern mikrotjänstarkitektur**

[⬆ Tillbaka till toppen](#intelligent-sales-assistant-platform)

</div>
