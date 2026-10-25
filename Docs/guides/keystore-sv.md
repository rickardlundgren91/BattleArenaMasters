# Skapa uppladdningsnyckeln (keystore) för BAM

Nyckeln bevisar för Google Play att en ny version kommer från dig. Du gör detta **en gång**. Tar ungefär 10 minuter.

## Innan du börjar

1. Hitta på **ett starkt lösenord** (minst 12 tecken). Skriv ner det på papper och spara det i en lösenordshanterare
   om du har en. Du kan använda samma lösenord för keystoren och för nyckeln, det är enklast.
2. Skapa en mapp **utanför** Unity-projektet och utanför alla git-mappar, till exempel:
   `C:\Users\Rickard\BAM-nycklar`
   (Inte i `C:\Users\Rickard\BattleArenaMasters`, inte i OneDrive-mappar som delas med andra.)

## Skapa nyckeln i Unity

1. Öppna projektet `BattleArenaMasters` i Unity.
2. Menyn **Edit > Project Settings...**
3. Klicka **Player** i listan till vänster.
4. Klicka på fliken med **Android-gubben** (robotikonen).
5. Fäll ut **Publishing Settings**.
6. Klicka **Keystore Manager...**
7. I fönstret som öppnas: klicka **Keystore...** uppe till vänster > **Create New** > **Anywhere...**
8. Gå till `C:\Users\Rickard\BAM-nycklar`, skriv filnamnet `bam-upload.keystore` och klicka **Spara**.
9. Fyll i:
   - **Password** och **Confirm password**: ditt lösenord.
   - **Alias**: `bam-upload`
   - **Password** och **Confirm password** (under New Key Values): samma lösenord igen.
   - **Validity (years)**: låt stå på **50**.
   - **First and Last Name**: Rickard Lundgren
   - **Organization**: Battle Arena Masters
   - **Country Code (XX)**: SE
   - Resten kan vara tomt.
10. Klicka **Add Key**.
11. Om Unity frågar om du vill använda den nya nyckeln för projektet: välj **No** / **Nej**.
    (BAM-menyn frågar efter nyckeln varje gång du bygger, så sökvägen och lösenordet sparas aldrig i projektet.)
12. Stäng Keystore Manager och Project Settings.

## Två säkerhetskopior (viktigt)

Tappar du filen **och** lösenordet blir det krångligt att släppa uppdateringar. Gör två kopior av
`bam-upload.keystore` direkt:

1. **Kopia 1:** ett USB-minne som du förvarar hemma, i en mapp som heter `BAM-nycklar`.
2. **Kopia 2:** en privat molnlagring som bara du kommer åt (t.ex. din egen Google Drive eller OneDrive,
   i en mapp som inte är delad med någon).

Lösenordet sparar du separat (papper + lösenordshanterare), **inte** i en textfil bredvid nyckeln.

## Aldrig på GitHub

- Lägg **aldrig** keystore-filen i projektmappen, i en git-mapp eller på GitHub, och skicka den inte i chatten.
- Skriv aldrig lösenordet i en fil i projektet.
- BAM:s byggfönster vägrar bygga om keystoren ligger inne i projektmappen.

## Vad händer vid första uppladdningen till Google Play (Play App Signing)

När du laddar upp den första AAB-filen i Play Console slår Google på **Play App Signing** (det är standard för nya
appar, du behöver bara godkänna):

- Google skapar och förvarar en egen **app-signeringsnyckel**. Det är den som signerar appen som spelarna laddar ner.
- Din `bam-upload.keystore` blir **uppladdningsnyckeln**: Google använder den bara för att kontrollera att nya
  versioner kommer från dig.
- Om du någon gång tappar uppladdningsnyckeln kan du be Google om en ny via Play Console
  (**Setup > App signing / Konfiguration > Appsignering**), men det tar tid. Därför säkerhetskopiorna.

Alla framtida versioner av BAM måste byggas med **samma** `bam-upload.keystore`.
