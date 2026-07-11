# Pravila rada za Codex

Radi oprezno i u malim koracima.

## Osnovna pravila

- Prvo analiziraj projekat i napiši plan.
- Ne menjaj fajlove dok ne objasniš koje fajlove bi menjao i zašto.
- Menjaj samo fajlove direktno povezane sa zadatkom.
- Ne refaktoriši nepovezane delove.
- Ne menjaj poslovnu logiku koju korisnik nije tražio.
- Posle svake izmene prikaži diff.
- Posle izmene pokreni build ako je moguće.

## Zabranjeno bez posebne potvrde

Ne menjaj bez posebne potvrde:

- appsettings.json
- appsettings.Development.json
- connection stringove
- licence
- API ključeve
- Google Drive / Dropbox podešavanja
- .csproj fajlove
- Entity Framework migracije
- bazu podataka
- produkcione konfiguracije
- fajlove van root foldera projekta

## Baza podataka

- Ne pokreći `dotnet ef database update` bez ručne potvrde.
- Ne menjaj postojeće migracije bez ručne potvrde.
- Ako treba nova migracija, prvo objasni:
  - koji entitet se menja,
  - koje kolone se dodaju/menjaju,
  - da li postoji rizik za postojeće podatke.

## Komande

Dozvoljeno za proveru:

- dotnet build
- dotnet test
- gradlew assembleDebug

Ne pokretati bez posebne potvrde:

- dotnet ef database update
- npm install
- gradlew clean
- komande za brisanje fajlova/foldera
- komande koje pristupaju internetu
- komande koje menjaju sistemska podešavanja

## Stil izmena

Korisnik voli ovaj format:

- Ako je fajl mali do oko 500 linija i promena je velika, prikaži ceo fajl.
- Ako je promena velika samo u jednoj metodi, prikaži celu metodu.
- Ako je promena mala, napiši tačno:
  - dodaj iznad,
  - dodaj ispod,
  - zameni ovo,
  - zameni celu metodu.

## Jezik

- Objašnjenja piši na srpskom.
- UI tekstovi u Windows aplikacijama treba da budu na srpskom, prirodno i razumljivo.