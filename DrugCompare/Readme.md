# MedCompare

MedCompare to desktopowa aplikacja napisana w C# i WPF, której celem jest lokalne sprawdzanie informacji medycznych związanych z lekami, substancjami czynnymi, interakcjami oraz klasyfikacją ICD.

Projekt powstał jako aplikacja edukacyjna/prototypowa. Chciałem stworzyć coś bardziej praktycznego niż zwykły CRUD — aplikację, która korzysta z realnych danych medycznych, działa lokalnie i pokazuje, jak można połączyć programowanie desktopowe, bazę danych i podstawy systemów wspomagania decyzji klinicznych.

Aplikacja nie korzysta z chmury ani z zewnętrznego API w trakcie działania. Dane są przechowywane lokalnie w bazie danych.

---

## Co potrafi aplikacja

Aktualnie MedCompare zawiera kilka głównych modułów:

* sprawdzanie interakcji pomiędzy substancjami czynnymi,
* wyszukiwanie leków i substancji,
* przeglądanie danych z polskiego rejestru produktów leczniczych,
* wyszukiwanie kodów ICD,
* podgląd historii i logów zdarzeń,
* tryb lokalny z bazą SQLite do wersji portable.

---

## Główne moduły

### Interaction Checker

To główny moduł aplikacji. Pozwala wyszukać lub ręcznie dodać substancje czynne, zaakceptować je do analizy, a następnie sprawdzić, czy w lokalnej bazie istnieją znane interakcje między wybranymi substancjami.

Aplikacja pokazuje:

* nazwę pierwszej substancji,
* nazwę drugiej substancji,
* poziom/severity interakcji,
* źródło danych,
* komunikat opisujący wynik.

Ważne jest to, że aplikacja nie mówi, że dane połączenie jest “bezpieczne”, jeśli nie znajdzie interakcji. Brak wyniku oznacza tylko, że w lokalnej bazie nie znaleziono pasującego rekordu.

---

### Drug Explorer

Ten moduł służy do przeglądania informacji o lekach oraz powiązanych substancjach czynnych. Jest to część rozwijana równolegle z bazą leków i importem danych.

Docelowo ma pomagać szybko sprawdzić, jakie substancje czynne są powiązane z danym produktem leczniczym.

---

### Polish Drug Registry

Moduł oparty na danych z polskiego Rejestru Produktów Leczniczych.

Pozwala wyszukiwać produkty lecznicze po:

* nazwie produktu,
* substancji czynnej,
* numerze pozwolenia,
* nazwie znormalizowanej.

W bazie znajdują się m.in.:

* nazwa produktu,
* substancje czynne,
* moc,
* postać farmaceutyczna,
* podmiot odpowiedzialny,
* numer pozwolenia,
* link do ChPL,
* link do ulotki.

---

### ICD Looker

Moduł do wyszukiwania kodów ICD. W aktualnej wersji używam danych ICD-11 w języku polskim.

Można wyszukiwać po:

* kodzie ICD,
* nazwie choroby,
* opisie,
* kategorii/rozdziale.

Przykładowe zapytania:

```text
cukrzyca
astma
nadciśnienie
depresja
```

---

## Dane

Aplikacja korzysta z lokalnych danych zaimportowanych do bazy SQLite.

Aktualna baza portable zawiera:

| Dane                  |                       Tabela | Liczba rekordów |
| --------------------- | ---------------------------: | --------------: |
| Substancje czynne     |          `active_substances` |           3 628 |
| Interakcje substancji |     `substance_interactions` |         627 553 |
| ICD-11 PL             |                  `icd_codes` |          34 222 |
| Polski rejestr leków  | `polish_drug_registry_items` |          22 785 |

Dane pochodzą z przygotowanych wcześniej plików importowych CSV. Projekt był początkowo oparty o PostgreSQL, ale później dodałem tryb SQLite, żeby można było łatwiej uruchomić aplikację na innym komputerze bez instalowania bazy danych.

---

## Tryby działania

### PostgreSQL

PostgreSQL był używany jako główna baza podczas developmentu. Ułatwiał import dużych plików, testowanie zapytań i rozwijanie struktury danych.

Przykładowy connection string:

```json
{
  "Database": {
    "Provider": "PostgreSQL"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=drug_compare_db;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

---

### SQLite portable

Tryb SQLite został dodany po to, żeby aplikację można było spakować do ZIP-a i uruchomić na innym komputerze bez pgAdmina, PostgreSQL i ręcznego importowania bazy.

W tym trybie aplikacja korzysta z pliku:

```text
data/medcompare.db
```

Przykładowy `appsettings.json`:

```json
{
  "Database": {
    "Provider": "SQLite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/medcompare.db"
  }
}
```

Dzięki temu aplikacja może działać lokalnie z jednym plikiem bazy.

---

## Technologie

W projekcie użyłem:

* C#,
* .NET 8,
* WPF,
* MVVM,
* CommunityToolkit.Mvvm,
* Dependency Injection,
* PostgreSQL,
* SQLite,
* Npgsql,
* Microsoft.Data.Sqlite.

---

## Struktura projektu

Najważniejsze foldery:

```text
DrugCompare/
├── Models/
├── Repositories/
├── Services/
├── Services/Contracts/
├── ViewModels/
├── Views/
├── Database/
├── database/sqlite/
├── data/
└── appsettings.json
```

Najważniejsze elementy:

```text
Models/                       modele danych
Repositories/                 dostęp do bazy danych
Services/                     logika aplikacji
Services/Contracts/           interfejsy usług
ViewModels/                   logika widoków
Views/                        widoki WPF
Database/SqliteConnectionFactory.cs
database/sqlite/schema_sqlite.sql
data/medcompare.db
```

---

## Repozytoria danych

Aplikacja ma osobne repozytoria dla PostgreSQL i SQLite.

Przykłady repozytoriów PostgreSQL:

```text
PostgresIcdCodeRepository
PostgresPolishDrugRegistryRepository
PostgresAuditLogRepository
PostgresInteractionRepository
```

Przykłady repozytoriów SQLite:

```text
SqliteIcdCodeRepository
SqlitePolishDrugRegistryRepository
SqliteAuditLogRepository
SqliteInteractionRepository
```

W `App.xaml.cs` aplikacja sprawdza ustawienie:

```json
"Provider": "SQLite"
```

i na tej podstawie wybiera odpowiednie repozytoria.

---

## Uruchomienie lokalne

W folderze projektu:

```powershell
dotnet build
dotnet run
```

Dla trybu SQLite trzeba mieć plik:

```text
data/medcompare.db
```

oraz odpowiednio ustawiony `appsettings.json`.

---

## Tworzenie bazy SQLite

Baza SQLite jest tworzona na podstawie pliku:

```text
database/sqlite/schema_sqlite.sql
```

Komendy:

```powershell
sqlite3 .\data\medcompare.db ".read .\database\sqlite\schema_sqlite.sql"
sqlite3 .\data\medcompare.db ".tables"
```

Sprawdzenie liczby rekordów:

```powershell
sqlite3 .\data\medcompare.db "SELECT COUNT(*) FROM active_substances;"
sqlite3 .\data\medcompare.db "SELECT COUNT(*) FROM substance_interactions;"
sqlite3 .\data\medcompare.db "SELECT COUNT(*) FROM icd_codes;"
sqlite3 .\data\medcompare.db "SELECT COUNT(*) FROM polish_drug_registry_items;"
```

---

## Publikacja wersji portable

Aplikację można opublikować jako samodzielny build dla Windows:

```powershell
dotnet publish .\DrugCompare.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\publish\portable
```

Następnie trzeba skopiować bazę i konfigurację:

```powershell
New-Item -ItemType Directory -Path .\publish\portable\data -Force

Copy-Item .\data\medcompare.db .\publish\portable\data\medcompare.db -Force
Copy-Item .\appsettings.json .\publish\portable\appsettings.json -Force
```

Finalny ZIP powinien wyglądać mniej więcej tak:

```text
MedCompare-portable.zip
├── DrugCompare.exe
├── appsettings.json
└── data/
    └── medcompare.db
```

Po wypakowaniu ZIP-a aplikację można uruchomić przez:

```text
DrugCompare.exe
```

---

## Status projektu

Projekt jest w fazie prototypu. Najważniejsze funkcje lokalne już działają, ale część modułów nadal jest rozwijana.

Aktualnie najważniejsze rzeczy, które są zrobione:

* aplikacja WPF z modułami,
* import danych medycznych,
* lokalna baza SQLite,
* sprawdzanie interakcji,
* wyszukiwanie ICD,
* wyszukiwanie produktów z polskiego rejestru,
* podstawowa obsługa logów,
* przygotowanie wersji portable.

Rzeczy do dalszego rozwoju:

* pełniejsze dopracowanie Drug Explorer,
* lepszy panel Data Management dla SQLite,
* lepszy panel Database Status dla SQLite,
* automatyzacja importu danych,
* poprawa UI,
* lepsze komunikaty dla użytkownika,
* instalator albo wygodniejszy release ZIP.

---

## Ważna informacja medyczna

MedCompare nie jest certyfikowanym wyrobem medycznym.

Aplikacja jest prototypem edukacyjnym i nie powinna być używana jako jedyne źródło decyzji medycznych.

Brak znalezionej interakcji w lokalnej bazie nie oznacza, że dane połączenie leków lub substancji jest bezpieczne. Oznacza tylko, że aplikacja nie znalazła pasującego rekordu w danych, które aktualnie posiada.

W razie wątpliwości decyzje medyczne powinny być konsultowane z lekarzem lub farmaceutą.

---

## Autor

Projekt tworzony jako aplikacja edukacyjna i praktyczny eksperyment z lokalnym systemem wspomagania informacji medycznej.

Celem było połączenie:

* aplikacji desktopowej,
* lokalnej bazy danych,
* realnych danych lekowych,
* prostego systemu sprawdzania interakcji,
* wersji portable możliwej do uruchomienia na innym komputerze.
