# Unity Cloud och Analytics: kontrollera kopplingen och skapa händelserna (för Rickard)

Gör det här när huvudtråden har lagt till Analytics-paketet i projektet. Inget skickas ändå förrän en spelare trycker
"Yes, share", och aldrig från Unity-editorn.

## 1. Kontrollera kopplingen (i Unity)
Projektet är redan kopplat till sitt eget Unity Cloud-projekt. Skapa inget nytt.
1. Öppna projektet `BattleArenaMasters` i Unity.
2. Menyn **Edit** > **Project Settings...** > **Services** i listan till vänster.
3. Kontrollera att det står projektet **BattleArenaMasters**
   (projekt-ID `91299722-1a09-48aa-be30-b51b3161fd68`). Då är allt rätt och du går vidare till steg 2.
4. Står något annat där, eller om Unity frågar vilket projekt du vill använda: välj **BattleArenaMasters**.
   Välj **aldrig "Speltest (1)"**: det är det gamla spelets projekt med ABC2-testdata.
5. Frågar Unity om appen riktar sig till barn under 13 (**COPPA**): välj **No**.
6. Under **Services** finns **Cloud Diagnostics**: låt den vara **avstängd** (den följer inte spelarens samtycke).

## 2. Slå på Analytics (i webbläsaren)
1. Gå till https://cloud.unity.com och öppna projektet **BattleArenaMasters** (inte "Speltest (1)").
2. Vänstermenyn: **Products** (eller **Shortcuts**) > **Analytics** > **Get started** / **Enable**. Godkänn villkoren.
   Hoppar guiden till "Install SDK" så är det redan gjort; klicka vidare tills den väntar på data.
3. Vänstermenyn under Analytics: **Event Manager** > **Custom Events**. Skapa tre händelser exakt så här
   (stora och små bokstäver spelar roll). Parametrar som inte finns skapar du med **Create new parameter**:

| Händelse | Parametrar (typ) |
|---|---|
| `matchStart` | `heroName` (STRING), `difficulty` (STRING), `tutorial` (BOOLEAN) |
| `matchEnd` | `heroName` (STRING), `difficulty` (STRING), `tutorial` (BOOLEAN), `won` (BOOLEAN), `quit` (BOOLEAN), `matchSeconds` (INTEGER), `kills` (INTEGER), `deaths` (INTEGER), `heroLevel` (INTEGER) |
| `tutorialStep` | `stepIndex` (INTEGER), `stepName` (STRING) |

4. Klicka **Save** / **Publish** på varje händelse.
5. Slå på reglaget **Enabled** (längst till höger i listan) för alla tre och bekräfta med **Enable**. En avslagen
   händelse sparas inte: den räknas bara som Invalid och går förlorad. (Så var det fram till 30 okt 2026, då 66 matchStart,
   61 matchEnd och 68 tutorialStep hade blivit Invalid. Reglaget gäller bara miljön **production**, dit spelet skickar.)
6. Kontroll efter en match med "Yes, share": siffran under **Valid** räknas upp inom ett par timmar. Ökar **Invalid** i
   stället för nya matcher, stämmer någon parametertyp inte med tabellen ovan.

## 3. Säg till när det är klart
Skriv i tråden när händelserna är skapade. Data syns i **Analytics > Dashboards** några timmar efter att någon spelat
med "Yes, share" på en telefon.
