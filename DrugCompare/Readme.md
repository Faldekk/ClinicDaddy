# ClinicDaddy / DrugCompare

Lokalna aplikacja desktopowa WPF (.NET 8) do przeglądania polskiego rejestru leków, ICD, interakcji oraz dokumentów ChPL. Zawiera lokalne wyszukiwanie źródeł i opcjonalne tworzenie odpowiedzi RAG przez Ollama.

> **Ważne:** aplikacja jest narzędziem pomocniczym. Nie zastępuje ChPL, lekarza ani farmaceuty. Brak wyniku w lokalnej bazie nie potwierdza bezpieczeństwa terapii.

## Status projektu i kolejność prac

Projekt jest w trakcie przygotowania do MVP. Poniższy opis obejmuje aktualny stan roboczy; nie oznacza pełnej akceptacji klinicznej ani ukończonego wydania.

Najpierw domykamy aplikację desktopową:

1. Spójna, schludna szata graficzna w stylu aplikacji medycznej: jasne tło, czytelne elementy i prosty kod stylów.
2. Nawigacja, układy widoków oraz zachowanie kontrolek przy zmianie rozmiaru okna.
3. Obsługa błędów, stany ładowania, anulowanie operacji i ustawienia.
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

## Architektura

```text
WPF Views / ViewModels
        ↓
Application services
        ↓
Repository contracts
        ↓
SQLite infrastructure
        ↓
LocalAppData/ClinicDaddy/data/medcompare.db
```

`data/medcompare.db` jest bazą startową. Przy pierwszym uruchomieniu aplikacja kopiuje ją do `%LocalAppData%\ClinicDaddy\data\medcompare.db`; dalsza praca odbywa się na kopii użytkownika. Kopie bezpieczeństwa trafiają do `%LocalAppData%\ClinicDaddy\backups`.

## Wymagania

- Windows;
- .NET 8 SDK do pracy deweloperskiej;
- opcjonalnie [Ollama](https://ollama.com/) i lokalny model, np. `qwen3:8b`, dla generowania odpowiedzi RAG.

## Uruchomienie

```powershell
dotnet restore
dotnet run --project .\DrugCompare.csproj
```

Testy:

```powershell
dotnet test .\DrugCompare.Tests\DrugCompare.Tests.csproj --configuration Release
```

## Publikacja

Jedynym wspieranym sposobem budowania paczki jest:

```powershell
.\scripts\Publish-Portable.ps1
```

Wynik trafia do `artifacts\ClinicDaddy-win-x64`. Aby świadomie nadpisać istniejącą paczkę:

```powershell
.\scripts\Publish-Portable.ps1 -Clean
```

Foldery `publish` oraz `artifacts` są artefaktami lokalnymi i nie należą do repozytorium.

## Struktura

```text
Application/       Modele, kontrakty i usługi aplikacyjne
Infrastructure/    SQLite oraz implementacje repozytoriów
Features/          Moduły funkcjonalne i ich widoki
Views/             Widoki aplikacyjne niezwiązane z pojedynczą funkcją
ViewModels/        ViewModele okien głównych
database/          Schemat i migracje SQLite
data/              Lokalna baza startowa (nie wersjonowana)
scripts/           Powtarzalne czynności deweloperskie
DrugCompare.Tests/ Testy automatyczne
```

## Dane i RAG

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
