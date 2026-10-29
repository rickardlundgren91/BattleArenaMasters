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
   **Paketnamn** (om Play frågar): `com.battlearenamasters.game`, exakt så. Kan aldrig ändras.
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

**Innan:** allt i steg 4 (Appinnehåll) och steg 5 (Butiksinformation, med skärmdumpar) måste vara ifyllt. KLART 26 okt
för 0.1. Men 0.2 har konton och onlinespel, så några rutor måste göras om: se **0.2** nedan.

**a. Testargrupp: KLAR 26 okt.** Gruppen är `bam-playtesters@googlegroups.com` (https://groups.google.com/g/bam-playtesters).
Kolla bara att **Gruppinställningar > Vem kan se medlemmar** står på **Gruppchefer**, så att testarna inte ser varandras Gmail.

**0.2. NYTT: gör de här innan du skickar till granskning (d)**

1. **Hemsidan.** Säg till mig så mergar jag PR #12. Kolla sedan att https://battlearenamasters.com/privacy säger
   "Last updated: 27 October 2026" och att https://battlearenamasters.com/delete-account öppnas.
2. **Inloggningen för testarna (Google Cloud, inte Play Console).** Öppna https://console.cloud.google.com inloggad som
   support@, välj projektet **bamcloudgame** > **Google Auth Platform** > **Målgrupp** (Audience) > **Publicera app** >
   **Bekräfta**. Står det redan **I produktion** är det klart. Utan det får alla som inte står på listan Testanvändare
   "Fel 403: access_denied" när de loggar in.
3. **Datasäkerhet:** **Policy och program > Appinnehåll > Datasäkerhet > Hantera**.
   - *Datainsamling och säkerhet:* samlar in data **Ja**. Krypterad under överföring **Ja**.
     Kontoskapande: kryssa bara **OAuth**. Bocka ur "Min app tillåter inte att användare skapar konto".
     **Webbadress för att radera konto:** `https://battlearenamasters.com/delete-account`.
     Kan användare begära att data raderas utan att radera kontot: **Ja**.
   - *Datatyper:* behåll de fem från 0.1 (**Ungefärlig plats**, **Appinteraktioner**, **Diagnostik**, **Annan
     prestandadata för appar**, **Enhets-id eller andra id:n**) och kryssa **också** i:
     **Personuppgifter > Namn**, **Personuppgifter > Användar-id:n**, **Appaktivitet > Andra åtgärder**.
   - *Varje ny datatyp* (Namn, Användar-id:n, Andra åtgärder): **Samlas in** (inte delas), **inte** tillfälligt behandlad,
     **Användarna kan välja**. Syfte:
     - Namn: **Appfunktioner** + **Kontohantering**
     - Användar-id:n: **Appfunktioner** + **Kontohantering** + **Bedrägeriförebyggande, säkerhet och efterlevnad**
     - Andra åtgärder: **Appfunktioner** + **Bedrägeriförebyggande, säkerhet och efterlevnad**
     - Appinteraktioner (finns redan, syfte Analys): lägg till **Appfunktioner** + **Bedrägeriförebyggande, säkerhet och efterlevnad**
     - De övriga behåller bara **Analys**.
   - **Spara.** Hela facit på engelska: `data-safety.md`.
4. **Innehållsklassificering:** **Appinnehåll > Innehållsklassificering > Redigera** (eller **Starta ny enkät**). Svara
   som förut, men på **Kan användare interagera eller utbyta innehåll** svarar du **Ja**. Följdfrågor: chatt **Nej**,
   delar plats **Nej**, digitala köp **Nej**. **Skicka** > **Använd klassificering**.
5. **Åtkomst till appen:** **Appinnehåll > Åtkomst till appen > Hantera**. Välj **Alla eller vissa funktioner är
   begränsade** > **Lägg till instruktioner**. Namn: `Online play (optional)`. Användarnamn och lösenord: lämna tomma
   (eller kryssa "Inga inloggningsuppgifter krävs" om rutan finns). Övrig information, klistra in:
   ```
   The whole game can be played offline against bots without signing in. Online features (friends, invites,
   online matches, Top 100) need a sign-in with Google Play Games: Settings > Account > Sign in, or the profile chip
   on the home screen. Any Google account works. We do not use our own usernames or passwords.
   ```
   **Spara.**
6. **Målgrupp och innehåll:** ändra inget (13–15, 16–17, 18+). Frågar Play om användarinteraktion: inget chattande, bara
   spelarnamn, vänförfrågningar och matcher.
7. **Butiksinformation** (steg 5): byt **Kort beskrivning** och **Fullständig beskrivning** mot de nya i
   `store-listing.md` (den gamla säger "Offline – no account" och "seven heroes"). **Butiksinställningar > Taggar**: lägg
   till **Flerspelare**. Nya skärmdumpar från onlinemenyn är bra men inget krav.

**b. Skapa det slutna spåret:**
1. Vänstermenyn: **Testa och släpp** > **Testning** > **Sluten testning** (Closed testing).
2. Det finns redan ett spår som heter **Closed testing - Alpha**: klicka **Hantera spår** (Manage track).
3. Fliken **Länder/regioner** > **Lägg till länder/regioner** > kryssa **alla** (eller minst där testarna bor) > **Lägg till**.
4. Fliken **Testare** > välj **Google-grupper** > skriv `bam-playtesters@googlegroups.com` > **Spara ändringar**.
   **Feedback-URL eller e-postadress:** `support@battlearenamasters.com`.
5. Under **Hur testare går med** finns en **opt-in-länk** (play.google.com/apps/testing/...). Den kan du kopiera redan nu,
   men den fungerar först när Google har godkänt versionen. Skicka den till mig, så lägger jag den på hemsidan.

**c. Versionen: 0.2.0 (6)**
- Gå till **Intern testning** > fliken **Versioner** > vid **0.2.0 (6)** klickar du **Befordra version** >
  **Sluten testning - Alpha**. Samma AAB, du behöver inte bygga om.
- Versionskommentarer: klistra in "Closed test release notes 0.2" från `store-listing.md` (med `<en-US>`-raderna)
  > **Nästa** > **Spara**.

**d. Skicka till granskning:**
1. Vänstermenyn: **Publiceringsöversikt** (Publishing overview).
2. Under **Ändringar som inte har skickats för granskning** ska du se: sluten version, Datasäkerhet,
   Innehållsklassificering, Åtkomst till appen och Butiksinformation. Klicka **Skicka X ändringar för granskning** > bekräfta.
3. Första granskningen kan ta **några dagar** (ibland upp till en vecka). Du får mejl när den är klar.
   Avvisar Google för att granskaren inte kunde logga in: säg till, då publicerar vi Play Games-tjänsterna.

**e. När den är godkänd och under de 14 dagarna:**
- Skicka opt-in-länken till mig (hemsidan) och posta i Discord. Testarna: gå med i gruppen > öppna länken > **Bli testare** > installera.
- Logga in med ett testkonto som **inte** finns på din Play Games-testarlista och kolla att inloggningen fungerar.
- Håll **minst 12 testare** anmälda hela tiden; skaffa gärna 15–20. En testare som lämnar börjar om på sina 14 dagar.
- Be testarna spela några gånger, också online, och ge feedback. Google frågar om det när du ansöker.
- Efter 14 dagar: **Översikt** (Dashboard) > **Ansök om produktion** (Apply for production).

## Senare: nya versioner
Bygg en ny AAB i Unity (versionskoden höjs automatiskt) > **Intern testning** > **Skapa ny version** > dra in filen >
skriv vad som är nytt > **Spara och publicera**.

## Att komma ihåg
- Intern testning räknas **inte** till kravet på 12 testare i 14 dagar; det gäller **sluten testning** (steg 7).
- Ändrar vi vad spelet samlar in måste `data-safety.md`, integritetspolicyn och Data safety i Play uppdateras först.
