# Google Play Console: steg för steg (för Rickard)

> Inte juridisk rådgivning. Knapparnas namn kan skilja lite; står Play Console på engelska heter de som inom parentes.
> Allt du ska klistra in finns i filerna i `Docs/play/` (på engelska, eftersom spelet är på engelska).

## 0. Innan du börjar
- Du behöver ett Google Play-utvecklarkonto (25 USD en gång) på https://play.google.com/console.
  Nya privata konton måste verifiera identitet och en Android-telefon (appen **Play Console** på telefonen) innan man kan
  publicera. Följ det Play Console visar på startsidan.
- AAB-filen byggs i Unity med **BAM > Build AAB (internal test)** och hamnar i `Builds/`.

## 1. Skapa appen
1. Öppna https://play.google.com/console och klicka **Skapa app** (Create app).
2. **Appnamn:** `Battle Arena Masters`
3. **Standardspråk:** `Engelska (USA) – en-US`
4. **App eller spel:** **Spel**. **Kostnadsfri eller betald:** **Kostnadsfri** (går inte att ändra till betald senare).
5. Kryssa i båda deklarationerna (riktlinjer för utvecklare och amerikanska exportlagar).
6. Klicka **Skapa app**.

## 2. Testare för intern testning
1. Vänstermenyn: **Testa och släpp** > **Testning** > **Intern testning** (Test and release > Testing > Internal testing).
2. Fliken **Testare** > **Skapa e-postlista**. Namn: `BAM testare`. Skriv in Gmail-adresserna (din egen, Simons och
   andra testare, högst 100), tryck Enter efter varje. Klicka **Spara ändringar**.
3. Kryssa i listan `BAM testare` och klicka **Spara**.
4. Under **Hur testare går med i testet** klickar du **Kopiera länk**. Skicka länken till testarna (se steg 6).
5. **Feedback-URL eller e-postadress:** `support@battlearenamasters.com`.

## 3. Ladda upp första versionen (och Play App Signing)
1. Samma sida, **Intern testning** > **Skapa ny version** (Create new release).
2. **App-signering av Google Play** visas första gången: låt standardvalet vara (Google skapar och förvarar
   appsigneringsnyckeln) och klicka **Fortsätt** / **Nästa**. Din egen nyckel från Unity (keystore-filen) blir då bara
   *uppladdningsnyckel*. Tappar du bort den kan du be Google om en ny, men spara den ändå på två ställen.
3. **Appaket:** dra in filen `Builds/BAM-....aab` från Unity-projektet.
   Paketnamnet `com.battlearenamasters.game` låses nu för alltid.
4. **Versionsnamn** fylls i av sig själv. **Versionskommentarer:** klistra in texten under
   "Internal test release notes" i `store-listing.md` (med `<en-US>`-raderna).
5. Klicka **Nästa**. Gula varningar går bra; röda fel måste fixas (skicka dem till mig).
6. Klicka **Spara och publicera** (eller **Starta utrullning till intern testning**).
   Om Play säger att något i "Appinnehåll" saknas: gör steg 4 och kom tillbaka.

## 4. Appinnehåll (krävs)
Vänstermenyn: **Policy och program** > **Appinnehåll** (Policy and programs > App content). Gör varje ruta:

| Ruta | Vad du väljer |
|---|---|
| **Integritetspolicy** | `https://battlearenamasters.com/privacy` > Spara |
| **Annonser** | **Nej, min app innehåller inga annonser** |
| **Åtkomst till appen** | **Alla funktioner är tillgängliga utan särskild åtkomst** |
| **Innehållsklassificering** | Följ `content-rating.md` (kategori Spel, svara som i tabellen) > Skicka |
| **Målgrupp och innehåll** | **13–15, 16–17 och 18 år och äldre**. Inget under 13. Se `content-rating.md`. |
| **Datasäkerhet** | Följ `data-safety.md` exakt (fyra datatyper, alla "valfria", syfte "Analys") |
| **Annonserings-id** | **Nej** |
| **Myndighetsappar** | Nej |
| **Finansiella funktioner** | Appen har inga finansiella funktioner |
| **Hälsa** | Appen har inga hälsofunktioner |
| **Nyhetsappar** | Nej |

## 5. Butiksinformation
Vänstermenyn: **Väx** > **Butiksnärvaro** > **Huvudbutiksinformation** (Grow users > Store presence > Main store listing).
Klistra in från `store-listing.md`: namn, kort beskrivning, fullständig beskrivning. Ladda upp
`store-icon-512.png` som appikon och `feature-graphic-1024x500.png` som grafik. Skärmdumpar (minst 2, stående) tar vi
när spelet går på telefonen. Under **Butiksinställningar**: kategori **Spel > Action**, e-post
`support@battlearenamasters.com`, webbplats `https://battlearenamasters.com`. Klicka **Spara**.
(För intern testning visas sidan inte för någon, men Play vill ofta ha den ifylld.)

## 6. Testarna installerar
1. Testaren öppnar länken från steg 2 på sin Android-telefon, inloggad med samma Gmail som står i listan.
2. Klicka **Bli testare** (Accept invite), sedan **ladda ned från Google Play**.
3. Första gången kan det ta upp till en timme innan appen syns i Play.

## Senare: nya versioner
Bygg en ny AAB i Unity (versionskoden höjs automatiskt) > **Intern testning** > **Skapa ny version** > dra in filen >
skriv vad som är nytt > **Spara och publicera**.

## Att komma ihåg
- Intern testning räknas **inte** till kravet på 12 testare i 14 dagar som nya privata konton har innan produktion;
  det kravet gäller **sluten testning** (closed testing) och tar vi när spelet är redo.
- Ändrar vi vad spelet samlar in måste `data-safety.md`, integritetspolicyn och Data safety i Play uppdateras först.
