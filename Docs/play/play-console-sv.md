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
Vänstermenyn: **Väx användare** > **Butiksnärvaro** > **Butiksinformation** > **Standardbutiksinformation** (Grow users > Store presence > Store listings > Default store listing). Hittar du den inte: skriv "store listing" i sökrutan högst upp i Play Console.
Klistra in från `store-listing.md`: namn, kort beskrivning, fullständig beskrivning. Ladda upp
`store-icon-512.png` som appikon och `feature-graphic-1024x500.png` som grafik.

**Skärmdumpar (minst 2, stående), tar du på telefonen:**
1. Starta spelet och ta skärmdumpar (oftast **av/på-knappen + volym ned** samtidigt): en från huvudmenyn eller
   hjältevalet och två–tre mitt i en match.
2. Play godtar inte bilder där långsidan är mer än dubbelt så lång som kortsidan. Moderna telefoner tar 20:9
   (t.ex. 1080x2400), så de måste beskäras: öppna bilden i **Google Foto** > **Redigera** > **Beskär** > välj
   formatet **9:16** > dra rutan över det viktiga > **Klart** > **Spara som kopia**.
3. Ladda upp kopiorna under **Telefonskärmdumpar** (PNG eller JPEG, 320–3840 pixlar per sida). Under **Butiksinställningar**: kategori **Spel > Action**, e-post
`support@battlearenamasters.com`, webbplats `https://battlearenamasters.com`. Klicka **Spara**.
(För intern testning visas sidan inte för någon, men Play vill ofta ha den ifylld.)

## 6. Testarna installerar
1. Testaren öppnar länken från steg 2 på sin Android-telefon, inloggad med samma Gmail som står i listan.
2. Klicka **Bli testare** (Accept invite), sedan **ladda ned från Google Play**.
3. Första gången kan det ta upp till en timme innan appen syns i Play.

## 7. Sluten testning (krävs innan spelet kan bli publikt)
Nya privata konton måste köra en **sluten test med minst 12 testare som varit anmälda utan avbrott i minst 14 dagar**
innan man kan ansöka om produktion. Intern testning räknas inte. Klockan börjar först när testarna kan gå med, alltså
efter att Google har godkänt den slutna versionen, så starta så tidigt som möjligt.

**Innan:** allt i steg 4 (Appinnehåll) och steg 5 (Butiksinformation, med skärmdumpar) måste vara ifyllt. Den slutna
testen granskas av Google, och Data safety krävs här (den behövs inte för bara intern testning).

**a. Testargrupp (enklast med en Google-grupp, då kan spelarna gå med själva):**
1. Gå till https://groups.google.com, inloggad med kontot som ska äga gruppen > **Skapa grupp**.
2. **Gruppnamn:** `BAM Testers`. **Gruppens e-post:** t.ex. `bam-testers` (blir `bam-testers@googlegroups.com`) > **Nästa**.
3. **Vem kan söka efter gruppen:** Alla på webben. **Vem kan gå med i gruppen:** **Alla kan be om att få gå med**
   (du godkänner) eller **Vem som helst kan gå med** (enklast) > **Nästa** > hoppa över att lägga till medlemmar > **Skapa grupp**.
4. Spelare går med via gruppens länk (`https://groups.google.com/g/bam-testers`) med samma Google-konto som de har i Play.

(Alternativ: en e-postlista i Play Console som i steg 2, men då måste du skriva in varje adress själv.)

**b. Skapa det slutna spåret:**
1. Vänstermenyn: **Testa och släpp** > **Testning** > **Sluten testning** (Closed testing).
2. Det finns redan ett spår som heter **Closed testing - Alpha**: klicka **Hantera spår** (Manage track).
   (Eller **Skapa spår** om du vill ha ett eget namn.)
3. Fliken **Länder/regioner** > **Lägg till länder/regioner** > kryssa **Sverige** och de länder där dina testare bor
   (eller alla) > **Lägg till**.
4. Fliken **Testare** > välj **Google-grupper** > skriv `bam-testers@googlegroups.com` > **Spara ändringar**.
   **Feedback-URL eller e-postadress:** `support@battlearenamasters.com`.
5. Under **Hur testare går med** finns en **opt-in-länk** (play.google.com/apps/testing/...). Den skickar du till spelarna
   efter att de gått med i gruppen. Där trycker de **Bli testare** och laddar sedan ned spelet från Play.

**c. Versionen:**
- **Enklast:** gå till **Intern testning** > fliken **Versioner** > vid din version klickar du **Befordra version** >
  **Sluten testning - Alpha** (Promote release). Samma AAB används, du behöver inte bygga om.
- **Eller ny AAB:** i spåret **Sluten testning** > **Skapa ny version** > dra in en ny AAB från Unity (versionskoden
  måste vara högre än alla tidigare uppladdade).
- Skriv versionskommentarer (från `store-listing.md`) > **Nästa** > **Spara**.

**d. Skicka till granskning:**
1. Vänstermenyn: **Publiceringsöversikt** (Publishing overview).
2. Under **Ändringar som inte har skickats för granskning** klickar du **Skicka X ändringar för granskning** > bekräfta.
3. Första granskningen av en ny app kan ta **några dagar** (ibland upp till en vecka). Du får mejl när den är klar.

**e. Under de 14 dagarna:**
- Håll **minst 12 testare** anmälda hela tiden; skaffa gärna 15–20 så att det finns marginal om någon hoppar av.
  En testare som lämnar och går med igen börjar om på sina 14 dagar.
- Be testarna spela några gånger och ge feedback (Discord eller mejl). Google frågar om det när du ansöker.
- Efter 14 dagar: **Översikt** (Dashboard) > **Ansök om produktion** (Apply for production). Formuläret frågar hur du
  hittade testarna, vad de tyckte, vad du ändrade och varför spelet är redo. Svaret kommer oftast inom 7 dagar.

## Senare: nya versioner
Bygg en ny AAB i Unity (versionskoden höjs automatiskt) > **Intern testning** > **Skapa ny version** > dra in filen >
skriv vad som är nytt > **Spara och publicera**.

## Att komma ihåg
- Intern testning räknas **inte** till kravet på 12 testare i 14 dagar; det gäller **sluten testning** (steg 7).
- Ändrar vi vad spelet samlar in måste `data-safety.md`, integritetspolicyn och Data safety i Play uppdateras först.
