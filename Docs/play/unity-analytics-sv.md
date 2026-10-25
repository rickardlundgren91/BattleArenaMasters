# Koppla Unity-projektet till Unity Cloud och slå på Analytics (för Rickard)

Gör det här när huvudtråden har lagt till Analytics-paketet i projektet. Inget skickas ändå förrän en spelare trycker
"Yes, share", och aldrig från Unity-editorn.

## 1. Koppla projektet (i Unity)
1. Öppna projektet `BattleArenaMasters` i Unity.
2. Uppe till höger: klicka på **molnikonen** / ditt konto och kontrollera att du är inloggad med ditt Unity-konto.
3. Menyn **Edit** > **Project Settings...** > **Services** i listan till vänster.
4. Välj din **Organization** i rullistan.
5. Klicka **Create project** (eller **Use an existing Unity Cloud project** om du redan skapat ett) och sedan **Link**
   / **Create project ID**. Namn: `Battle Arena Masters`.
6. Frågar Unity om appen riktar sig till barn under 13 (**COPPA**): välj **No**.
7. Under **Services** finns **Cloud Diagnostics**: låt den vara **avstängd** (den följer inte spelarens samtycke).

## 2. Slå på Analytics (i webbläsaren)
1. Gå till https://cloud.unity.com och öppna projektet **Battle Arena Masters**.
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

## 3. Säg till när det är klart
Skriv i tråden att projektet är kopplat. Data syns i **Analytics > Dashboards** några timmar efter att någon spelat
med "Yes, share" på en telefon.
