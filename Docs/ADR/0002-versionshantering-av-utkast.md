# ADR 0002: Versionshantering av AI-utkast via Non-Destructive Editing

* **Status:** Accepterat
* **Datum:** 2026-05-20
* **Författare:** Joco Borghol

## Kontext och Problemställning
Nern säljare genererar ett säljutkast eller en pitch via AI-funktionen behöver de ofta kunna justera texten manuellt under pågående kunddialog. Ett grundläggande affärskrav är att det ursprungliga AI-genererade innehållet aldrig får gå förlorat. Användaren måste sömlöst kunna jämföra ändringar, ångra sina redigeringar eller återgå till originalversionen.

Jag behövde fastställa en lagringsstrategi som stöder denna versionshantering utan att kompromissa med databasens prestanda och storlek.

## Beslut
Jag har implementerat ett **Non-Destructive Editing-mönster** baserat på en **hybrid-lagringsmodell**:
1. All strukturerad metadata (ID-kopplingar, tidsstämplar, samt de valda parametrarna Triggers och Tonalitet) lagras i SQLite-databasen.
2. Det faktiska textinnehållet lagras som råa textfiler (`.txt`) direkt på serverns filsystem.

## Motivering

### 1. Resurseffektiv hybrid-lagring och spårbarhet
Att lagra stora mängder ostrukturerad råtext direkt i databasrader leder över tid till sämre sökprestanda och en onödigt stor databasfil. Genom att flytta textinnehållet till filsystemet och endast spara sökvägar i SQLite bibehålls databasens prestanda.

Valda **Triggers** och **Tone** sparas däremot som strukturerad data i databasen. Detta möjliggör framtida kvalitetsutvärderingar och analyser av genereringsmönster (exempelvis för att identifiera "Garbage in, garbage out"-scenarier), utan att applikationen behöver läsa av de externa textfilerna.

### 2. Strikt separation mellan original och modifierat innehåll
Systemet hanterar versioner genom att hålla isär filerna logiskt:
- Vid den initiala genereringen sparas innehållet på disken med suffixet `-original.txt` och sökvägen registreras i kolumnen `OriginalContentPath`.
- Om säljaren redigerar texten via ett PUT-anrop lämnas originalfilen orörd. Det nya innehållet skrivs till en ny fil med suffixet `-modified.txt`, och dess sökväg registreras i `ModifiedContentPath`.
- Vid läsning prioriterar applikationen den modifierade sökvägen om den är tillgänglig, annars faller vyn tillbaka på originaltexten.

### 3. Säker och tillförlitlig återställningsfunktion
Separationen av data gör att funktionen för att återställa till originalet (POST /restore) kan utföras med minimal logisk komplexitet:
- Den modifierade textfilen (`-modified.txt`) raderas permanent från disken.
- Kolumnen `ModifiedContentPath` uppdateras till `null` i databasen.
- Användaren återfår omedelbart tillgång till det ursprungliga AI-genererade utkastet utan att systemet behöver hantera tunga historiktabeller eller komplexa rollback-skript.