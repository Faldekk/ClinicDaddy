# Drug Compare

**Drug Compare** is a local WPF desktop application for checking known interactions between active pharmaceutical substances using a PostgreSQL database.

The application is designed as an educational clinical decision-support prototype. It does not diagnose, prescribe, recommend treatment, or replace the judgment of a physician or pharmacist.

Special shoutout to **@RiKKy44** for helping with the architecture design of the first prototype.

---

## Medical Disclaimer

> This application is an educational clinical decision-support prototype. It does not replace physician or pharmacist judgment.

Important safety note:

> Missing interaction data does not mean that a drug combination is safe. It only means that no matching interaction was found in the local database.

Every result must be clinically verified by qualified medical personnel.

---

## Project Purpose

Drug Compare helps a medical user verify whether selected active substances have known interactions in a local DDInter-based interaction database.

The application is based on **active substances**, not only brand names. A user can search for a drug/product name, review detected active substances, manually add or correct substances, and then check interactions between all accepted active substances.

The project is currently a **local-first clinical decision-support prototype** focused on:

* local data processing,
* PostgreSQL-backed medical datasets,
* clear separation between UI, application logic, and database access,
* safety-oriented medical messaging,
* no cloud processing,
* no external LLM or API dependency.

---

## Current Application Workflow

```text
Drug/product name
    ↓
Detected active substance(s)
    ↓
Accepted active substance list
    ↓
Interaction check
    ↓
Severity result + warning + history + report export
```

Example workflow:

```text
Drug name: Ibuprom
Detected active substance: Ibuprofen

Manual active substance: Warfarin

Check interactions
Result: Ibuprofen + Warfarin → Major / Moderate / Unknown depending on local DDInter data
```

---

## Current Features

Implemented features include:

* WPF desktop interface
* tab-based UI layout
* local-only application mode
* PostgreSQL connection through Npgsql
* drug/product name lookup
* active substance detection
* manual active substance entry
* accepted active substance workflow
* interaction checking between selected substances
* severity display with UI badges
* clinical warning when no interaction is found
* startup database statistics window
* database status tab
* data management tab
* interaction check history
* report export to `.txt`
* repository-based data access layer
* service contract separation
* application-level interaction analysis service
* synonym-ready substance lookup
* local audit log support

---

## Tech Stack

* C#
* .NET 8
* WPF
* MVVM
* CommunityToolkit.Mvvm
* PostgreSQL
* Npgsql
* Microsoft.Extensions.DependencyInjection
* Microsoft.Extensions.Configuration.Json

---

## Architecture Overview

The project is being refactored toward a cleaner layered architecture.

Current direction:

```text
WPF Views
    ↓
ViewModels
    ↓
Application Services / Use Cases
    ↓
Service Contracts
    ↓
Repositories
    ↓
PostgreSQL
```

The goal is to keep UI logic, application workflow logic, and database access separated.

---

## Current Project Structure

```text
DrugCompare
│
├── Models
│   ├── ActiveSubstanceItem.cs
│   ├── ActiveSubstanceSynonymItem.cs
│   ├── AuditLogItem.cs
│   ├── DatabaseStatusResult.cs
│   ├── DataManagementStatusResult.cs
│   ├── DataSourceVersionItem.cs
│   ├── DrugLookupResult.cs
│   ├── InteractionAnalysisResult.cs
│   ├── InteractionHistoryItem.cs
│   └── InteractionResult.cs
│
├── Services
│   ├── Contracts
│   │   ├── IAuditLogService.cs
│   │   ├── IDataManagementService.cs
│   │   ├── IDatabaseStatusService.cs
│   │   ├── IDrugLookupService.cs
│   │   ├── IInteractionCheckerService.cs
│   │   ├── IInteractionHistoryService.cs
│   │   ├── ISubstanceLookupService.cs
│   │   └── ISubstanceSynonymService.cs
│   │
│   ├── Application
│   │   ├── AuditLogService.cs
│   │   └── InteractionAnalysisService.cs
│   │
│   └── PostgresDrugDataService.cs
│
├── Repositories
│   ├── Contracts
│   │   ├── IAuditLogRepository.cs
│   │   ├── IDataManagementRepository.cs
│   │   ├── IDatabaseStatusRepository.cs
│   │   ├── IDrugRepository.cs
│   │   ├── IInteractionHistoryRepository.cs
│   │   ├── IInteractionRepository.cs
│   │   └── ISubstanceRepository.cs
│   │
│   ├── PostgresAuditLogRepository.cs
│   ├── PostgresDataManagementRepository.cs
│   ├── PostgresDatabaseStatusRepository.cs
│   ├── PostgresDrugRepository.cs
│   ├── PostgresInteractionHistoryRepository.cs
│   ├── PostgresInteractionRepository.cs
│   └── PostgresSubstanceRepository.cs
│
├── ViewModels
│   ├── DatabaseStatsViewModel.cs
│   └── MainViewModel.cs
│
├── Views
│   ├── DatabaseStatusView.xaml
│   ├── DataManagementView.xaml
│   ├── HistoryView.xaml
│   ├── InteractionCheckView.xaml
│   └── SettingsView.xaml
│
├── Converters
│   └── SeverityToBrushConverter.cs
│
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── DatabaseStatsWindow.xaml
├── DatabaseStatsWindow.xaml.cs
├── appsettings.example.json
└── DrugCompare.csproj
```

---

## Architectural Status

The project originally started as a simpler WPF prototype with direct service access to PostgreSQL. It has since been refactored into a more modular structure.

Current architectural improvements include:

### View separation

The main UI is split into separate WPF `UserControl` views:

```text
InteractionCheckView
HistoryView
DatabaseStatusView
DataManagementView
SettingsView
```

`MainWindow.xaml` now acts mostly as a shell with tabs.

---

### ViewModel layer

`MainViewModel` handles UI state and commands, including:

```text
FindDrugCommand
AddManualSubstanceCommand
CheckInteractionsCommand
LoadHistoryCommand
LoadDatabaseStatusCommand
LoadDataManagementCommand
ExportCurrentReportCommand
```

The ViewModel is still central, but more business workflow logic is being moved into application services.

---

### Application service layer

Application-level workflow logic is separated into dedicated services.

Current application services:

```text
InteractionAnalysisService
AuditLogService
```

`InteractionAnalysisService` coordinates:

```text
interaction checking
history saving
summary message generation
highest severity calculation
```

`AuditLogService` handles structured local audit entries.

---

### Service contracts

Application-facing capabilities are defined through small interfaces instead of one large service.

Current service contracts include:

```text
IDrugLookupService
ISubstanceLookupService
IInteractionCheckerService
IDatabaseStatusService
IDataManagementService
IInteractionHistoryService
ISubstanceSynonymService
IAuditLogService
```

This makes the project easier to extend and test.

---

### Repository layer

PostgreSQL-specific SQL logic is being moved into repository classes.

Current repositories include:

```text
PostgresDrugRepository
PostgresSubstanceRepository
PostgresInteractionRepository
PostgresInteractionHistoryRepository
PostgresDatabaseStatusRepository
PostgresDataManagementRepository
PostgresAuditLogRepository
```

This separates database queries from UI and application workflow logic.

`PostgresDrugDataService` currently acts as a facade that connects service contracts to repository implementations.

---

## Database Concept

The application uses two main medical data sources:

### EMA Data

Used for mapping:

```text
drug/product name → active substance → manufacturer
```

### DDInter Data

Used for mapping:

```text
active substance A + active substance B → interaction severity
```

Together, these sources allow the application to support this workflow:

```text
drug name → active substance(s) → substance interaction check
```

---

## PostgreSQL Database

Default database name:

```text
drug_compare_db
```

The application expects a local PostgreSQL database.

Main database tables:

```text
drugs
active_substances
drug_active_substances
substance_interactions
active_substance_synonyms
interaction_check_history
data_source_versions
audit_logs
```

---

## Main Database Tables

### `drugs`

Stores medicine/product names.

```text
id
name
normalized_name
manufacturer
source
created_at
```

Purpose:

```text
drug/product lookup
```

---

### `active_substances`

Stores active pharmaceutical substances.

```text
id
name
normalized_name
ddinter_id
source
created_at
```

Purpose:

```text
canonical active substance records used for interaction checking
```

---

### `drug_active_substances`

Stores the relation between drugs and active substances.

```text
id
drug_id
active_substance_id
```

Purpose:

```text
drug/product name → active substance(s)
```

Example:

```text
Ibuprom → Ibuprofen
```

---

### `substance_interactions`

Stores known interactions between active substances.

```text
id
substance_a_id
substance_b_id
severity
source
last_updated
```

Purpose:

```text
active substance A + active substance B → interaction severity
```

Interaction pairs are stored in ordered form:

```text
substance_a_id < substance_b_id
```

This prevents duplicate pairs such as:

```text
Ibuprofen + Warfarin
Warfarin + Ibuprofen
```

from being stored twice.

---

### `active_substance_synonyms`

Stores manually curated substance synonyms.

```text
id
active_substance_id
synonym
normalized_synonym
source
created_at
```

Purpose:

```text
alias/synonym → canonical active substance
```

Examples:

```text
Acetaminophen → Paracetamol
Aspirin → Acetylsalicylic acid
Epinephrine → Adrenaline
```

This table is important because EMA and DDInter may use different names for the same substance.

---

### `interaction_check_history`

Stores local history of interaction checks.

```text
id
accepted_substances_json
results_json
highest_severity
created_at
```

Purpose:

```text
save previous checks for review and traceability
```

---

### `data_source_versions`

Stores metadata about imported datasets.

```text
id
source_name
file_name
imported_at
records_imported
notes
source_url
checksum
import_status
error_message
```

Purpose:

```text
track imported EMA/DDInter versions and import status
```

The Data Management tab uses this table to show:

```text
latest EMA import
latest DDInter import
record counts
file names
import status
recent imports
```

---

### `audit_logs`

Stores local audit-like events.

```text
id
action
details_json
created_at
```

Purpose:

```text
record important local actions performed in the application
```

Currently planned/logged actions include:

```text
DrugSearched
SubstanceAccepted
InteractionChecked
ReportExported
DatabaseStatsViewed
```

---

## PostgreSQL Fuzzy Search

The application can use PostgreSQL `pg_trgm` for fuzzy search.

Run once:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

Recommended indexes:

```sql
CREATE INDEX IF NOT EXISTS ix_drugs_normalized_name_trgm
ON drugs
USING gin (normalized_name gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_active_substances_normalized_name_trgm
ON active_substances
USING gin (normalized_name gin_trgm_ops);
```

---

## Database Setup

Create a PostgreSQL database:

```sql
CREATE DATABASE drug_compare_db;
```

Then create the required tables using project SQL scripts or manually prepared schema scripts.

The expected connection string format is:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=drug_compare_db;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

---

## Configuration

Create a local `appsettings.json` file in the WPF project folder:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=drug_compare_db;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

Replace:

```text
YOUR_PASSWORD
```

with your local PostgreSQL password.

Do not commit your real `appsettings.json` file.

Use:

```text
appsettings.example.json
```

as a safe template.

---

## Running the Application

Restore packages:

```powershell
dotnet restore
```

Build the project:

```powershell
dotnet build
```

Run the application:

```powershell
dotnet run
```

Or open the solution in Visual Studio and run the WPF project.

---

## Required NuGet Packages

The project uses:

```powershell
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.FileExtensions
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Npgsql
```

---

## Data Import Workflow

### EMA Data

EMA data is used to import:

```text
DrugName
ActiveSubstance
Manufacturer
```

This data populates:

```text
drugs
active_substances
drug_active_substances
```

---

### DDInter Data

DDInter data is used to import:

```text
DDInterID_A
Drug_A
DDInterID_B
Drug_B
Level
```

After preprocessing, DDInter data is imported as:

```text
ddinter_id_a
substance_a
ddinter_id_b
substance_b
level
```

This data populates:

```text
active_substances
substance_interactions
```

---

## Data Management Status

The application includes a Data Management tab that displays metadata from `data_source_versions`.

It shows:

```text
latest EMA import
latest DDInter import
source file name
records imported
import timestamp
import status
recent import history
```

This is intended to make the prototype more transparent about which local datasets are currently being used.

---

## Important Data Limitation

EMA and DDInter may use different names for the same active substance.

Examples:

```text
EMA: Paracetamol
DDInter: Acetaminophen
```

```text
EMA: Acetylsalicylic acid
DDInter: Aspirin
```

Because of this, some interactions may not be found unless substance names are normalized or synonym mapping is added.

The current architecture includes support for:

```text
active_substance_synonyms
```

to improve matching.

---

## UI Overview

The application UI is organized into tabs:

```text
Interaction Check
History
Database Status
Data Management
Settings
```

---

### Interaction Check

Allows the user to:

```text
search drug names
view detected active substances
accept substances
add manual active substances
check interactions
export current report
clear current case
```

Interaction results include:

```text
Substance A
Substance B
Severity
Message
Source
```

Severity is displayed using visual badges.

---

### History

Displays recent locally saved interaction checks.

Shows:

```text
date
highest severity
accepted substances
results
```

Data is stored in PostgreSQL in the `interaction_check_history` table.

---

### Database Status

Displays local database statistics:

```text
drug count
active substance count
drug-substance relation count
substance interaction count
```

A startup statistics window is also shown when the application opens.

---

### Data Management

Displays imported dataset metadata:

```text
EMA import status
DDInter import status
recent imports
record counts
file names
import timestamps
```

---

### Settings

Displays current prototype configuration and medical safety notes.

Future settings may include:

```text
database connection diagnostics
data import settings
synonym management
report export settings
clinical warning preferences
```

---

## Report Export

The application can export the current interaction check as a text report.

The report includes:

```text
generated date
accepted active substances
detected interactions
highest severity
medical disclaimer
safety warning
```

Reports are intended for educational/prototype use only.

---

## Safety Logic

Drug Compare intentionally avoids unsafe language.

It should not say:

```text
No interactions = safe
```

Instead, it uses:

```text
No known interaction was found in the local database.
Missing interaction data does not mean that the combination is safe.
```

---

## Security and Privacy

The application is local-only by design.

It does not send data to:

```text
cloud APIs
external LLMs
remote medical services
```

The PostgreSQL database runs locally unless the user explicitly configures another server.

---

## Repository Notes

The following files should not be committed:

```text
appsettings.json
*.csv
*.xlsx
*.backup
*.dump
bin/
obj/
.vs/
```

The repository should include:

```text
README.md
appsettings.example.json
.cs
.xaml
.csproj
.sln
database schema scripts
small sample seed scripts
```

Large EMA/DDInter datasets should not be committed directly to GitHub.

---

## Current Status

The project currently includes:

```text
local PostgreSQL-backed interaction checking
tab-based WPF UI
separated WPF views
MVVM-based UI logic
service contracts
application services
repository-based data access
startup database statistics
database status view
data management view
interaction history
report export
severity badges
audit log backend
synonym-ready lookup layer
```

The project is still being actively developed and refactored.

---

## Planned Improvements

Planned next steps:

```text
1. Add full Audit Log UI.
2. Add UI for managing active substance synonyms.
3. Add database/schema.sql.
4. Add sample_seed.sql.
5. Add unit tests.
6. Add Serilog file logging.
7. Add clinical case workspace.
8. Add data provenance details per interaction result.
9. Add importer status/error reporting.
10. Improve report formatting.
```

---

## Known Limitations

Current limitations:

```text
dataset quality depends on imported EMA/DDInter files
substance names may differ between sources
manual synonym mapping is still limited
no user authentication
no full clinical context engine
no dosage/frequency checking
no patient-specific risk scoring
no formal medical device certification
```

---

## Future Target Architecture

Long-term target:

```text
DrugCompare.App
    WPF Views, ViewModels, converters

DrugCompare.Application
    use cases, application services, DTOs

DrugCompare.Core
    entities, value objects, contracts

DrugCompare.Infrastructure
    PostgreSQL repositories, importers, file system services

DrugCompare.Tests
    unit tests and integration tests
```

This would make the project more maintainable and closer to a real product prototype.

---

## Startup Prototype Roadmap

### Phase 1 — MVP

```text
local PostgreSQL database
drug lookup
manual substance entry
interaction checking
history
report export
startup database stats
```

### Phase 2 — Product Prototype

```text
synonym management UI
data management dashboard
audit logs
unit tests
structured logging
database schema scripts
```

### Phase 3 — Clinical Prototype

```text
case workspace
clinical risk factors
renal/hepatic impairment warnings
pregnancy warnings
data provenance per result
manual review flags
```

### Phase 4 — Demo-Ready Prototype

```text
installer
demo seed database
screenshots
pitch deck
demo video
technical architecture diagram
clear safety disclaimer
```

---

## Example Test Flow

1. Run PostgreSQL.
2. Make sure `drug_compare_db` exists.
3. Make sure EMA and DDInter data are imported.
4. Start the WPF application.
5. Review startup database statistics.
6. Open the Interaction Check tab.
7. Enter a drug name.
8. Accept detected active substances.
9. Manually add another active substance if needed.
10. Click `Check interactions`.
11. Review detected interaction warnings.
12. Export the current report if needed.
13. Check saved result in the History tab.
14. Review dataset metadata in the Data Management tab.

---

## License and Data Source Notes

This project is a student/educational prototype.

Before using EMA, DDInter, or any medical dataset in a public, commercial, or clinical environment, verify the licensing and usage terms of each dataset.

---

## Final Note

Drug Compare is not a clinical authority. It is a local software prototype that helps surface known substance-substance interaction records from imported datasets.

Every result must be clinically verified by qualified medical personnel.
