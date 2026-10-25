# Bygga BAM för Google Play

## En gång: ställ in projektet

1. Kontrollera att **Android Build Support** finns: öppna **Unity Hub > Installs**, klicka kugghjulet vid
   **6000.6.5f1 > Add modules** och se att **Android Build Support** (med OpenJDK och Android SDK & NDK Tools) är ikryssat.
   Saknas det: kryssa i, klicka **Install** och starta om Unity.
2. Öppna projektet i Unity och välj menyn **BAM > Setup Project**.
   - Unity kan fråga om du vill spara scenen som är öppen. Välj **Save** (eller **Don't Save** om du inte ändrat något).
   - Unity frågar om mallens exempel ska tas bort (TutorialInfo, Readme, SampleScene med flera). Välj **Ta bort**.
     De hamnar i papperskorgen.
   - En ruta säger hur många inställningar som ändrades. Detaljer står i **Console** (Window > General > Console),
     rader som börjar med `[BAM.Build]`. Gula rader är varningar, läs dem.

Du kan köra **BAM > Setup Project** igen när som helst; den ändrar bara det som inte redan stämmer.

## Varje gång du vill ha en ny version till Play

1. Menyn **BAM > Build AAB (internal test)**.
2. Första gången: Unity kan behöva byta till Android. Klicka **Byt till Android** och vänta (kan ta flera minuter).
3. Ett litet fönster öppnas:
   - **Keystore-fil**: klicka **Välj...** och välj `C:\Users\Rickard\BAM-nycklar\bam-upload.keystore`
     (Unity kommer ihåg sökvägen till nästa gång).
   - **Keystore-lösenord** och **Nyckelns lösenord**: ditt lösenord (sparas aldrig).
   - **Nyckelns alias**: `bam-upload`
   - Klicka **Bygg AAB**.
4. Vänta. Första bygget tar ofta 10 till 20 minuter, senare byggen går fortare.
5. När det är klart öppnas mappen och en ruta visar var filen ligger:
   `C:\Users\Rickard\BattleArenaMasters\Builds\BAM-0.1.0-<nummer>.aab`
   Numret (versionskoden) höjs automatiskt för varje lyckat bygge. Google Play kräver att det alltid blir högre.
   Misslyckas bygget sätts numret tillbaka.
6. Ladda upp filen: **Play Console > BAM > Testa och släpp > Testning > Intern testning > Skapa ny version**,
   dra in `.aab`-filen, skriv vad som är nytt och klicka **Nästa > Spara och publicera**.

## Snabbtest på din egen telefon (utan Play)

- **BAM > Build test APK (own phone)** bygger `Builds\BAM-0.1.0-test.apk`. Ingen nyckel behövs.
  Den kan inte laddas upp till Play, den är bara för dig.
- **BAM > Build test APK and run on phone (USB)** installerar och startar spelet direkt om telefonen är kopplad med
  USB och **USB-felsökning** är påslagen (Inställningar > Om telefonen > tryck 7 gånger på Byggnummer >
  Utvecklaralternativ > USB-felsökning).
- Har telefonen redan en version från Play internt test måste du avinstallera den först (olika signaturer).

## Grafiknivåer (för den som skriver spelkoden)

Kvalitetsnivåerna i **Project Settings > Quality** är exakt två:

| Index | Namn | URP-asset | Innehåll |
|---|---|---|---|
| 0 | Low | `Assets/BAM/Settings/BAM_URP_Low.asset` | render scale 0.8, inga skuggor, ingen MSAA, ingen HDR |
| 1 | High | `Assets/BAM/Settings/BAM_URP_High.asset` | render scale 1, skuggor från huvudljuset, MSAA 2x, ingen HDR |

Index är samma som `BAM.Core.GraphicsQuality` (Low = 0, High = 1); spelet anropar
`QualitySettings.SetQualityLevel((int)BamSettings.Graphics)`. Ingen post-processing på någon nivå.

## Om något går fel

- **"Android saknas"**: se steg 1 överst.
- **Fel som nämner keystore, signing eller password**: fel lösenord eller alias. Försök igen.
- **Fel om Android SDK / API level 36**: öppna **Edit > Preferences > External Tools** och se att
  "Android SDK Tools Installed with Unity" är ikryssat. Fråga Claude och klistra in den röda raden från Console.
- **Annat**: kopiera de röda raderna i Console (högerklick > Copy) och skicka dem till Claude.
