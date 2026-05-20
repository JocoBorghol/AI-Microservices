# ADR 0001: Val av molnhosting för distribuerade mikrotjänster

* **Status:** Accepterat
* **Datum:** 2026-05-20
* **Författare:** Joco Borghol

## Kontext och Problemställning
Systemet är uppdelat i två fristående mikrotjänster som behöver samverka i en säker och resurseffektiv molnmiljö:
1. **Service A (Huvud-API)**: Hanterar säljarkonton, databaslager och externa endpoints för klientapplikationen.
2. **Service B (Content Engine)**: Ansvarar för direktkommunikation med Google Gemini API, promptbyggnad och bearbetning av AI-genererat innehåll.

Jag behövde besluta om en lämplig driftsättningsplattform inom Azure som stöder denna mikrotjänstarkitektur. De huvudsakliga alternativen var Azure App Service och **Azure Container Apps (ACA)**.

## Beslut
Jag har valt **Azure Container Apps (ACA)** som gemensam driftsättningsplattform för båda tjänsterna. Arkitekturen förstärks med **Azure Key Vault** för centraliserad hantering av hemligheter, där behörigheter styrs strikt via **Azure RBAC** (rollbaserad åtkomstkontroll) istället för traditionella åtkomstprinciper (Access Policies).

## Motivering

### 1. Isolerad miljö via intern Ingress
Genom att placera både Service A och Service B i samma gemensamma Container Apps-miljö (Managed Environment) kan jag isolera baksidestjänsten helt från det publika internet.
- Service B konfigureras med **intern Ingress** (en `.internal`-adress), vilket gör den oåtkomlig utifrån.
- Service A konfigureras med **extern Ingress** för att kunna ta emot legitima anrop från klienter.
- Detta möjliggör säker och snabb kommunikation mellan tjänsterna över det interna virtuella nätverket, utan behov av publika endpoints eller komplexa brandväggsregler för Service B.

### 2. Identitetsbaserad säkerhet (Managed Identity & RBAC)
För att minimera risken för läckta API-nycklar har alla känsliga strängar avlägsnats från applikationernas konfigurationsfiler.
- Service B har tilldelats en **System-Assigned Managed Identity**.
- Genom Azure RBAC har jag tilldelat denna identitet rollen **Key Vault Secrets User** begränsat till produktionsvalvet (`kv-isa-content-prod`).
- Vid runtime injiceras `GeminiSettings--ApiKey` automatiskt i containern via en säker Key Vault-referens. Detta garanterar att inga hemligheter lagras i källkodshistoriken.

### 3. Automatisk skalning och kostnadskontroll (Scale-to-Zero)
Eftersom verktyget används periodiskt under säljarnas kundsamtal varierar belastningen kraftigt över dygnet.
- Med hjälp av inbyggd KEDA-skalning i ACA kan tjänsterna automatiskt skala ned till **0 aktiva instanser** när ingen trafik registreras.
- Detta eliminerar löpande kostnader under inaktivitet, vilket är kritiskt för att inte riskera studentkontots begränsade budget.
- Vid nya anrop startar instanserna upp omedelbart. Jag har satt en hård övre gräns till **max 2 instanser** för att förhindra oväntade kostnadstoppar.

### 4. Garanterad miljökonsekvens via Docker
Genom att paketera applikationerna i Docker-containers säkerställer jag att systemet fungerar identiskt under lokal utveckling i Docker Desktop som vid driftsättning i produktion via den automatiserade CI/CD-pipelinen i GitHub Actions.