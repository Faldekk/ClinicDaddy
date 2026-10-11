# ClinicDaddy / DrugCompare

Lokalna aplikacja desktopowa WPF (.NET 8) do przeglądania polskiego rejestru leków, ICD, interakcji oraz dokumentów ChPL. Zawiera lokalne wyszukiwanie źródeł i opcjonalne tworzenie odpowiedzi RAG przez Ollama.

> **Ważne:** aplikacja jest narzędziem pomocniczym. Nie zastępuje ChPL, lekarza ani farmaceuty. Brak wyniku w lokalnej bazie nie potwierdza bezpieczeństwa terapii.

Dokumentacja opisuje stan kodu na **12 października 2026 r.** Nazwa produktu to ClinicDaddy; projekt, przestrzenie nazw i plik wykonywalny nadal używają nazwy DrugCompare.

## Spis treści

- [Status projektu](#status-projektu-i-kolejność-prac)
- [Funkcje](#funkcje)
- [Architektura](#architektura)
- [Wymagania i uruchomienie](#wymagania)
- [Konfiguracja](#konfiguracja)
- [Publikacja](#publikacja)
- [Struktura kodu](#struktura)
- [SQLite i przepływy danych](#sqlite-i-przepływy-danych)
- [RAG i Ollama](#dane-i-rag)
- [Bezpieczeństwo](#bezpieczeństwo-i-ograniczenia)
- [Testy i diagnostyka](#testy-i-diagnostyka)

## Status projektu i kolejność prac

Projekt jest w trakcie przygotowania do MVP. Poniższy opis obejmuje aktualny stan roboczy; nie oznacza pełnej akceptacji klinicznej ani ukończonego wydania.

Najpierw domykamy aplikację desktopową:

1. Spójna, schludna szata graficzna w stylu aplikacji medycznej: jasne tło, czytelne elementy i prosty kod stylów.
2. Nawigacja, układy widoków oraz zachowanie kontrolek przy zmianie rozmiaru okna.
3. Obsługa błędów, stany ładowania i anulowanie operacji; ustawienia wymagają dalszego rozwoju.
4. Stabilność uruchamiania, kompilacja i testy regresji funkcji niezwiązanych z rozbudową RAG lub danych.

**RAG i bazę danych odkładamy na końcowy etap.** Do tego czasu nie rozszerzamy korpusu, nie prowadzimy kolejnych testów jakości odpowiedzi i nie zmieniamy statusów weryfikacji dokumentów. Dotychczasowe funkcje oraz testy pozostają w projekcie.

Końcowy etap obejmie ocenę zgodności odpowiedzi ze źródłami, kontrolę parsera i fragmentacji ChPL, kompletność oraz aktualność bazy, przegląd niezatwierdzonych dokumentów i testy całego przepływu. Dopiero potem przygotujemy finalną paczkę portable i sprawdzimy ją na czystym Windows.

Dotychczasowe testy techniczne nie potwierdzają poprawności klinicznej całej bazy. Import dokumentu nie nadaje mu automatycznie statusu `reviewed` ani `verified`.

## Funkcje

- Polski Rejestr Leków z wyszukiwaniem części nazw;
- ICD Looker;
- Interaction Checker i historia sprawdzeń;
- ChPL Navigator: dokumenty, sekcje i eksport;
- Evidence Assistant: wyszukiwanie fragmentów źródeł, statusy weryfikacji oraz opcjonalny RAG przez Ollama;
- status bazy, audit log oraz backup i bezpieczny restore SQLite.

Ogólny import EMA/DDInter jest obecnie wyłączony: startup rejestruje `DisabledDataManagementService` i `DisabledDataManagementRepository`. Odczyt już istniejących interakcji oraz osobny importer ChPL pozostają dostępne.

Widok ustawień jest informacyjny, nie stanowi pełnego edytora utrwalanych preferencji. Historia ma kontrakt i repozytorium zapisu, ale **obecny `InteractionCheckerViewModel` nie wywołuje zapisu historii po sprawdzeniu**. Widok odczytuje istniejące rekordy; zapis zdarzenia audytowego nie jest rekordem historii.

## Architektura

Aplikacja składa się z jednego projektu WPF i osobnego projektu testów. Warstwy są podziałem odpowiedzialności w kodzie, nie oddzielnymi mikroserwisami ani osobnymi bibliotekami. Ollama jest opcjonalną usługą działającą poza procesem aplikacji.

```text
MainWindow / widoki XAML
          |
          v
ViewModele + polecenia MVVM
          |
          v
Kontrakty i usługi Application
          |
          +--> Repozytoria Infrastructure/SQLite --> lokalny plik SQLite
          |
          +--> ChPL: HTTP --> PDF --> tekst --> sekcje --> zapis/eksport
          |
          +--> RAG: FTS5 --> zatwierdzone fragmenty --> Ollama --> walidator
```

### Prezentacja i MVVM

- `MainWindow.xaml` jest powłoką: menu, zwijana nawigacja i przełączanie zakładek.
- `MainViewModel` udostępnia ViewModele modułów oraz operacje wspólne: historię, audyt i zarządzanie danymi. Import partii jest wydzielony do `MainViewModel.ChplImport.cs`.
- Moduły w `Features/` mają własne widoki i ViewModele; widoki przekrojowe znajdują się w `Views/`.
- `CommunityToolkit.Mvvm` dostarcza obserwowalne właściwości i polecenia synchroniczne/asynchroniczne.
- Zasoby i style współdzielone są w `App.xaml`; część stylów jest lokalna dla modułów. `Presentation/` zawiera zachowania klawiatury, konwertery i kontrolowane otwieranie linków.
- ICD, rejestr i kontroler interakcji blokują kolidujące operacje podczas pracy. Historia i audyt mają niezależne stany ładowania. Zmiana zaakceptowanych substancji unieważnia wcześniejsze wyniki interakcji.

MVVM nie oznacza tu całkowitej niezależności od WPF: część ViewModeli korzysta nadal z dialogów plikowych, komunikatów i timera UI. Projekt nie jest przygotowany do uruchamiania całego interfejsu poza Windows.

### Usługi i repozytoria

| Obszar | Odpowiedzialność |
| --- | --- |
| `Application/Models` | Modele produktów, substancji, wyników, bazy wiedzy i odpowiedzi RAG. |
| `Application/Services/Contracts` | Operacje używane przez ViewModele. |
| `Application/Services/Implementations` | Orkiestracja, przegląd wiedzy, import oraz komunikacja z Ollama. |
| `Application/Repositories/Contracts` | Interfejsy dostępu do danych. |
| `Infrastructure/SQLite` | SQL, połączenia, migracje, repozytoria i backup/restore. |
| `Infrastructure/SQLite/KnowledgeBase` | Dokumenty, sekcje, fragmenty, review i import partii. |
| `Infrastructure/SQLite/Rag` | Retrieval przez FTS5. |

`DrugDataService` współdzieli implementację kontraktów wyszukiwania leków/substancji, interakcji i historii, delegując operacje do repozytoriów. SQL używa parametrów dla wartości użytkownika. Singleton fabryki połączeń nie oznacza jednego stale otwartego połączenia — repozytoria otwierają i zwalniają połączenia dla operacji.

### Startup i wstrzykiwanie zależności

Punktem składania zależności jest `App.xaml.cs`. Rejestracje używają głównie singletonów, więc stan modułów pozostaje podczas przełączania zakładek.

1. `OnStartup` odczytuje `appsettings.json` z katalogu pliku wykonywalnego.
2. Rejestruje konfigurację, repozytoria, usługi, ViewModele i główne okno w `Microsoft.Extensions.DependencyInjection`.
3. `SqliteConnectionFactory` rozwiązuje ścieżkę aktywnej bazy.
4. `SqliteDatabaseInitializer.InitializeAsync()` konfiguruje SQLite i wykonuje brakujące migracje.
5. Dopiero po powodzeniu inicjalizacji pokazywane jest `MainWindow`.
6. Przy zamknięciu zwalniany jest `ServiceProvider`.

Błędy startu są raportowane dialogiem i logiem. Globalny handler wyjątków UI zapisuje diagnostykę, ale nie gwarantuje odzyskania spójnego stanu po każdym błędzie.

## Wymagania

- Windows;
- SDK .NET obsługujące `net8.0-windows` do pracy deweloperskiej i dostęp do zależności NuGet przy pierwszym restore;
- opcjonalnie [Ollama](https://ollama.com/) i lokalny model, np. `qwen3:8b`, dla generowania odpowiedzi RAG.

## Uruchomienie

Polecenia uruchamiaj z katalogu zawierającego `DrugCompare.csproj`.

**Baza startowa `data/medcompare.db` nie jest wersjonowana.** Sam klon repozytorium nie zapewnia działającej bazy produktów, ICD i interakcji. Dostarcz zgodną bazę do tego katalogu albo skonfiguruj ścieżkę do istniejącego pliku. Migracje w `database/` rozwijają głównie bazę wiedzy — nie odtwarzają całego korpusu klinicznego od zera.

```powershell
dotnet restore .\DrugCompare.csproj
dotnet build .\DrugCompare.csproj --configuration Release
dotnet run --project .\DrugCompare.csproj
```

Testy:

```powershell
dotnet test .\DrugCompare.Tests\DrugCompare.Tests.csproj --configuration Release
```

Jeżeli build zgłasza blokadę `DrugCompare.exe`, zamknij uruchomione instancje aplikacji i zakończ debugowanie przed ponowną kompilacją.

## Konfiguracja

```json
{
  "Database": { "Provider": "SQLite" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=data/medcompare.db" },
  "Rag": {
    "Ollama": {
      "Enabled": true,
      "Endpoint": "http://127.0.0.1:11434/",
      "Model": "qwen3:8b",
      "TimeoutSeconds": 180
    }
  }
}
```

`Database:Provider` nie przełącza obecnie implementacji na inny silnik — startup rejestruje SQLite. Konfiguracja nie używa providerów zmiennych środowiskowych ani argumentów CLI. Po zmianie `appsettings.json` uruchom aplikację ponownie; edytuj plik używany przez daną kompilację/paczkę.

## Publikacja

Jedynym wspieranym sposobem budowania paczki jest:

```powershell
.\scripts\Publish-Portable.ps1
```

Wynik trafia do `artifacts\ClinicDaddy-win-x64`. Aby świadomie nadpisać istniejącą paczkę:

```powershell
.\scripts\Publish-Portable.ps1 -Clean
```

Skrypt buduje Release, `win-x64`, self-contained i sprawdza obecność EXE, konfiguracji oraz bazy startowej. Takie wydanie nie wymaga osobnej instalacji runtime .NET. Baza startowa musi jednak być dostępna przed publikacją.

`-Clean` usuwa dotychczasową zawartość katalogu wydania. Cel musi leżeć w podkatalogu `artifacts`; skrypt odrzuca reparse points w kontrolowanej ścieżce. Nie przechowuj tam jedynych kopii danych. Katalog nie musi istnieć w checkout — powstaje podczas publikacji.

`publish/` i `artifacts/` są ignorowanymi katalogami wynikowymi, nie źródłami aplikacji. Przed dystrybucją sprawdź migracje, start na czystym Windows i zachowanie z istniejącą bazą użytkownika. Nie publikuj prywatnej historii, logów ani backupów.

## Struktura

```text
Application/       Modele, kontrakty i usługi aplikacyjne
Infrastructure/    SQLite oraz implementacje repozytoriów
Features/          Moduły funkcjonalne i ich widoki
Presentation/      Zachowania klawiatury, konwertery, WebDocumentLauncher
Views/             Widoki aplikacyjne niezwiązane z pojedynczą funkcją
ViewModels/        MainViewModel oraz orkiestracja importu partii
database/          Schemat i migracje SQLite
data/              Lokalna baza startowa (nie wersjonowana)
sqlite_export/     Lokalne pliki referencyjne CSV, jeśli dostępne
scripts/           Powtarzalne czynności deweloperskie
DrugCompare.Tests/ Testy automatyczne
```

Główne pliki wejściowe to `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `DrugCompare.csproj` i `appsettings.json`. Struktura jest względna wobec katalogu projektu aplikacji.

## SQLite i przepływy danych

### Ścieżki i trwałość

| Zasób | Lokalizacja / zachowanie |
| --- | --- |
| Baza startowa | `data/medcompare.db` w katalogu danej kompilacji/paczki. |
| Domyślna aktywna baza | `%LocalAppData%\ClinicDaddy\data\medcompare.db`; kopiowana z bazy startowej tylko przy braku kopii użytkownika. |
| Niestandardowa baza | Ścieżka z connection string; względna jest rozwiązywana wobec katalogu aplikacji. Brak automatycznego przeniesienia do LocalAppData. |
| Pobrane PDF ChPL | `%LocalAppData%\ClinicDaddy\ChPL`. |
| Ręczny backup | Plik wybrany w dialogu zapisu. |
| Backup przed importem partii | `%LocalAppData%\ClinicDaddy\backups`. |
| Backup przed restore | `medcompare-before-restore-*.db` obok aktywnej bazy. |
| Raporty i eksporty | Lokalizacja wybrana przez użytkownika. |

Nowa paczka nie nadpisuje istniejącej domyślnej bazy użytkownika. „Portable” opisuje dystrybucję binariów, **nie izolację danych w folderze paczki**: domyślna baza jest współdzielona przez uruchomienia na tym samym koncie Windows.

Główne obszary danych to produkty/substancje (`drugs`, `active_substances` i relacje/synonimy), `substance_interactions`, `polish_drug_registry_items`, `icd_codes`, `interaction_history`, `audit_logs` oraz korpus `chpl_documents` → `chpl_sections` → `knowledge_chunks`. Indeks `knowledge_chunks_fts` służy wyszukiwaniu FTS5.

Inicjalizator włącza foreign keys, WAL, `synchronous=NORMAL` i czas oczekiwania na blokadę. Migracje `001`–`004` są wykonywane transakcyjnie, a ich identyfikatory trafiają do `schema_migrations`; wykonane migracje nie są ponawiane przy każdym starcie.

### Backup i restore

Backup używa SQLite Backup API. Restore odrzuca wskazanie aktywnego pliku jako źródła, wykonuje `PRAGMA integrity_check`, tworzy kopię sprzed przywrócenia, czyści pule połączeń i zastępuje aktywny plik. Po restore należy ponownie uruchomić aplikację.

`integrity_check` nie potwierdza kompletności schematu, autentyczności, aktualności ani poprawności klinicznej danych. Przywracaj zaufane kopie i zachowuj kopię sprzed podmiany. Nie podmieniaj ręcznie aktywnej bazy podczas pracy aplikacji. Backup może zawierać historię/audyt — nie jest plikiem do publicznego Git.

### Rejestr, ICD i interakcje

Rejestr używa parametryzowanego `LIKE` w nazwach, substancjach i wybranych polach rejestracyjnych; preferuje dopasowanie dokładne i prefiks. To wyszukiwanie częściowe, nie pełna korekta literówek. ICD przeszukuje kod/nazwę i obsługuje filtr rozdziału.

Interaction Checker wykrywa substancje produktu, a użytkownik zatwierdza listę. Dla każdej pary repozytorium szuka interakcji po lokalnych ID, następnie po znormalizowanych nazwach. `DDInterId` istnieje w modelu, ale nie jest obecnie głównym kluczem tej kwerendy. Severity pochodzi z rekordu interakcji, nie z modelu językowego. Brak rekordu oznacza brak znalezionych danych, nie bezpieczeństwo połączenia.

### ChPL: dokument → sekcje → fragmenty

1. Użytkownik wybiera lokalny PDF albo bezpośredni adres PDF; rejestr może przekazać kontekst produktu.
2. `ChplPdfDownloader` pobiera HTTP(S), sprawdza odpowiedź/nagłówek PDF i limit transferu 50 MB. Plik tymczasowy jest promowany dopiero po zakończeniu pobierania.
3. `PdfTextExtractor` używa PdfPig; nie zawiera OCR dla samych skanów.
4. `ChplSectionParser` rozpoznaje sekcje, a `ChplRuleTagger` uzupełnia metadane pomocnicze.
5. `KnowledgeBaseIngestionService` tworzy rekordy dokumentu i sekcji, hashe oraz metadane parsera.
6. `SqliteAtomicKnowledgeBaseIngestionRepository` zapisuje dokument, sekcje i fragmenty transakcyjnie.
7. Importowane dane otrzymują `needs_review`, niezależnie od etykiet dostarczonego dokumentu.

Eksportery CSV/JSON działają na sparsowanym dokumencie. Neutralizacja formuł w CSV zmienia reprezentację eksportową, nie dane źródłowe ani JSON.

## Dane i RAG

Retrieval jest **leksykalny, oparty na SQLite FTS5**; nie używa bazy wektorowej ani embeddingów. `QueryIntentClassifier` preferuje odpowiednie sekcje ChPL. Filtry produktu, sekcji i statusów ograniczają wyniki, ale nie gwarantują kompletności dowodów.

```text
Pytanie + filtry
  -> SqliteFtsRagRetriever
  -> fragmenty reviewed/verified
  -> OllamaRagAnswerService /api/chat
  -> RagAnswerValidator
  -> maksymalnie jedna próba naprawy
  -> wnioski z cytatami albo jawny błąd/brak dowodów
```

Evidence Assistant pokazuje dostępność Ollama oraz skonfigurowanego modelu. Po uruchomieniu widoku połączenie jest sprawdzane automatycznie; przycisk **Sprawdź Ollama** ponawia sprawdzenie. Brak serwera lub modelu nie blokuje wyszukiwania źródeł. Podczas generowania widać czas oczekiwania i można użyć **Anuluj odpowiedź**. Timeout, błędny format oraz odrzucone cytowania mają osobne komunikaty; aplikacja nie zastępuje błędu gotową odpowiedzią.

Do modelu trafiają maksymalnie cztery zatwierdzone fragmenty (po 900 znaków). Generowanie ma kontekst 4096 tokenów, ograniczoną długość i wyłączony osobny tok rozumowania. Są to ustawienia dla krótkiej redakcji lokalnych źródeł, nie pełnego przeglądu wszystkich dokumentów. Poprawne cytowanie identyfikatora nie potwierdza poprawności klinicznej twierdzenia.

Każdy wniosek modelu musi również zawierać dosłowny cytat (`evidenceQuote`) z fragmentu rzeczywiście wysłanego do Ollama. Walidacja toleruje tylko różnice w białych znakach, nie parafrazy cytatów. Widok pokazuje wnioski z dowodami, bez osobnego niezweryfikowanego podsumowania. Nadal konieczna jest ocena zgodności znaczenia przez człowieka — zgodny cytat nie gwarantuje poprawnej interpretacji. Nie są zmieniane statusy przeglądu ani treść bazy.

Model nie generuje osobnego podsumowania: właściwość `Summary` po generowaniu składa się wyłącznie z jego zwalidowanych wniosków (jest pusta przy niewystarczających dowodach). Powielone cytaty oraz więcej niż cztery wnioski są odrzucane, z jedną próbą naprawy przez model.

Test `LiveRagAcceptanceTests` można włączyć przez ustawienie `CLINICDADDY_LIVE_DB` (pełna ścieżka aktywnej bazy) i `CLINICDADDY_RAG_REPORT` (docelowy plik JSON). Czyta SQLite w trybie ReadOnly, wywołuje rzeczywistą Ollama i zapisuje odpowiedź wraz ze źródłami do ręcznej oceny. Domyślnie jest pomijany.

### Diagnostyka niezatwierdzonych dokumentów

Test `UnreviewedMultiProductBenchmark` bada APAP dla dzieci FORTE, Ibuprom RR MAX i APAP migrena. Wymaga dodatkowego, jawnego `CLINICDADDY_DIAGNOSTIC_UNREVIEWED=1`. Pobiera fragmenty `needs_review` bez ich zatwierdzania. Wyłącznie na kopiach w pamięci testu dostosowuje status do kontraktu generatora; w raporcie zachowuje rzeczywiste statusy z SQLite. Kod testów nie jest częścią aplikacji. Normalny generator nadal odrzuca `needs_review`.

```powershell
$env:CLINICDADDY_LIVE_DB = "$env:LOCALAPPDATA\ClinicDaddy\data\medcompare.db"
$env:CLINICDADDY_RAG_REPORT = "$PWD\artifacts\rag-check\diagnostic-report.json"
$env:CLINICDADDY_DIAGNOSTIC_UNREVIEWED = '1'
New-Item -ItemType Directory -Path artifacts\rag-check -Force | Out-Null
dotnet test DrugCompare.Tests\DrugCompare.Tests.csproj -c Release --filter FullyQualifiedName~UnreviewedMultiProductBenchmark
Remove-Item Env:\CLINICDADDY_DIAGNOSTIC_UNREVIEWED
```

Użyj nowej nazwy raportu, aby zachować wcześniejsze wyniki. Każdy przypadek jest zapisywany osobno również po błędzie; surowe odpowiedzi trafiają obok raportu. Błędy techniczne powodują niepowodzenie testu po ukończeniu całego zestawu. Sukces techniczny NIE jest zatwierdzeniem medycznym; ocena znaczenia jest oznaczona `pending_manual_review`.

W zakładce **Zarządzanie danymi** można pobierać brakujące ChPL w partiach od 10 do 500 produktów. Import tworzy kopię SQLite przed pobraniem, pokazuje postęp i przyczyny błędów. Anulowanie zachowuje ukończone zapisy. **Ponów błędy** ponownie próbuje pobrać nieudane pozycje; kolejna zwykła partia przechodzi dalej w rejestrze. Kolejka błędów i pozycja partii są zachowywane w ramach bieżącej sesji aplikacji, a raporty importu trafiają do dziennika operacji.

Importer uzupełnia brakujące dokumenty. Nie wykrywa jeszcze nowych wersji już zapisanych ChPL. Dokumenty bez rozpoznawalnego tekstu lub sekcji wymagają ręcznego sprawdzenia.

Dokumenty ChPL oraz fragmenty RAG mają statusy `needs_review`, `reviewed`, `verified` i `rejected`. Normalny generator używa wyłącznie pobranych źródeł `reviewed` lub `verified`; odpowiedzi bez prawidłowego cytowania są odrzucane.

Zmiana review obejmuje dokument, sekcje i fragmenty oraz zapis audytu w transakcji. Nazwa recenzenta jest wpisywana przez operatora, nie jest uwierzytelnioną rolą ani podpisem cyfrowym.

### Uruchomienie Ollama i kontrakt odpowiedzi

Po zainstalowaniu i uruchomieniu Ollama pobierz model zgodny z konfiguracją:

```powershell
ollama pull qwen3:8b
ollama list
```

Jeśli serwer nie działa, uruchom aplikację Ollama lub `ollama serve`; nie uruchamiaj drugiej instancji przy zajętym porcie. Domyślny endpoint to `http://127.0.0.1:11434/`. Timeout jest ograniczony do 30–600 sekund (domyślnie 180).

`RagAnswer` zawiera `Findings` i `InsufficientEvidence`; każdy `RagFinding` ma `Claim`, `SourceIds`, `EvidenceQuote`. Żądanie używa JSON, `stream=false`, `think=false`, limitu generowania 768 i temperatury 0.1. Walidator sprawdza ID, statusy, długość i dosłowność cytatu, liczbę wniosków oraz powtórzenia. **Nie dowodzi zgodności znaczenia twierdzenia z cytatem.**

Konfiguracja dopuszcza zdalny HTTP(S), więc pytanie i fragmenty mogą opuścić komputer. Nie wysyłaj danych identyfikujących pacjenta do niezatwierdzonego endpointu. Aplikacja nie ma gotowego mechanizmu anonimizacji ani uwierzytelniania takiej transmisji.

**DDInter nie jest obecnie dodatkowym zabezpieczeniem severity w RAG.** Połączenie zostało odłożone. Nie zakładaj, że odpowiedź modelu jest automatycznie porównywana z bazą interakcji.

## Bezpieczeństwo i ograniczenia

- `Presentation/WebDocumentLauncher.cs` jest wspólną granicą otwierania źródeł, ChPL i ulotek: dopuszcza bezwzględne HTTP(S), odrzuca lokalne/UNC/file/custom-scheme cele, znaki kontrolne i dane logowania w URI; do systemu przekazuje zwalidowany adres kanoniczny.
- `ChplCsvExporter` neutralizuje początkowe `=`, `+`, `-`, `@`, także po białych znakach, znakach kontrolnych i znakach formatu Unicode, przed escapingiem CSV. Nie gwarantuje zachowania dowolnego arkusza po ponownym zapisaniu pliku.
- Limit transferu PDF nie stanowi pełnego limitu czasu/pamięci dekodowania każdego lokalnego dokumentu.
- Baza, PDF, logi, backupy i eksporty polegają na ochronie konta i uprawnieniach plikowych Windows. Projekt nie zapewnia szyfrowania danych ani osobnej autoryzacji użytkowników aplikacji.
- Testy techniczne, poprawne cytowanie i integralność SQLite nie zastępują oceny źródeł ani walidacji klinicznej.

## Testy i diagnostyka

Projekt testów używa MSTest i obejmuje repozytoria/fixtures SQLite, import partii, walidację RAG, odpowiedzi HTTP Ollama, stany operacji ViewModeli, ładowanie WPF/nawigację oraz zabezpieczenia linków i CSV. Testy WPF wymagają Windows. Ich przejście nie jest pełnym ręcznym testem uruchomionej aplikacji.

Przykład ograniczonego zestawu bez rzeczywistego wywołania modelu:

```powershell
dotnet test .\DrugCompare.Tests\DrugCompare.Tests.csproj -c Release --filter "FullyQualifiedName~DocumentSecurityTests|FullyQualifiedName~SearchStateTests|FullyQualifiedName~InteractionStateTests|FullyQualifiedName~HistoryAndAuditStateTests|FullyQualifiedName~ViewLoadingTests"
```

Testy live opisane wyżej są domyślnie pomijane/inconclusive. Flagi diagnostyczne nie są konfiguracją normalnej pracy aplikacji. Raport techniczny wymaga odrębnej oceny znaczenia medycznego.

Logi tekstowe są zapisywane obok pliku wykonywalnego danej kompilacji:

| Plik | Przeznaczenie |
| --- | --- |
| `startup-error.log` | Błędy startu i nieobsłużone wyjątki UI. |
| `app-lifecycle.log` | Etapy startu i zamknięcia. |
| `chpl-import.log` | Pobieranie, ekstrakcja i import dokumentów. |
| `rag-ui.log` | Przebieg generowania i błędy RAG. |

Przy braku uprawnień zapis logów może się nie udać. Audyt w SQLite jest osobnym mechanizmem. Przed udostępnieniem diagnostyki usuń dane wrażliwe i prywatne ścieżki.

Najczęstsze punkty kontroli: zgodna/dostępna baza, właściwy `appsettings.json` przy EXE, pliki migracji, brak blokady EXE przez inną instancję, endpoint Ollama i zgodna nazwa pobranego modelu. Pusty RAG może wynikać też z filtrów albo braku zatwierdzonych źródeł.

## Pozostałe prace do MVP

1. Domknięcie obsługi ChPL, błędów, nawigacji i regresji UI.
2. Uporządkowanie rzeczywistego zapisu historii oraz utrwalanych ustawień.
3. Końcowy przegląd aktualności/kompletności bazy, parsera, fragmentacji i znaczenia odpowiedzi RAG.
4. Ewentualne uzupełnianie odpowiedzi severity z DDInter, z jawnym źródłem i brakami danych — obecnie odłożone.
5. Test całej paczki na czystym Windows i akceptacja zakresu MVP.

Obecność funkcji lub przycisków nie oznacza gotowości do zastosowań klinicznych.
