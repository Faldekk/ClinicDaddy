# Drug Compare

**Drug Compare** is a WPF desktop application for checking known interactions between active pharmaceutical substances using a local PostgreSQL database.

The application is designed as a clinical decision-support prototype. It does not diagnose, prescribe, or replace the judgment of a physician or pharmacist.

Special shoutout to @RiKKy44 for helping with designing an architecture of a first prototype.

---

## Purpose

The application helps a physician verify whether selected active substances have known interactions in a local DDInter-based interaction database.

The workflow is based on active substances, not only brand names. A physician can search for a drug name, review detected active substances, manually add or correct active substances, and then check interactions between the accepted substances.

---

## Medical Disclaimer

> This application is an educational clinical decision-support prototype. It does not replace physician or pharmacist judgment.

Important safety note:

> Missing interaction data does not mean that a drug combination is safe. It only means that no matching interaction was found in the local database.

---

## Main Features

* WPF desktop interface
* Local-only processing
* PostgreSQL database integration
* Drug name lookup
* Active substance detection
* Manual active substance entry
* Active substance acceptance workflow
* Interaction checking between selected substances
* Risk/severity display
* Clinical verification warning
* Local data sources:

  * EMA medicine/product data
  * DDInter interaction data

---

## How It Works

1. The user enters a drug name.
2. The application searches the local PostgreSQL database.
3. If the drug is found, the application displays its active substance or substances.
4. The physician can accept detected active substances.
5. The physician can manually add additional active substances.
6. After confirmation, the application checks known interactions between all accepted active substances.
7. The application displays interaction severity and a clinical verification message.

Example workflow:

```text
Drug name: Ibuprom
Detected active substance: Ibuprofen

Manual active substance: Warfarin

Check interactions
Result: Ibuprofen + Warfarin → Major / Moderate / Unknown depending on local DDInter data
```

---

## Tech Stack

* C#
* .NET 8
* WPF
* MVVM-style architecture
* PostgreSQL
* Npgsql
* CommunityToolkit.Mvvm
* Microsoft.Extensions.DependencyInjection
* Microsoft.Extensions.Configuration.Json

---

## Project Structure

```text
DrugCompare
│
├── Models
│   ├── ActiveSubstanceItem.cs
│   ├── DrugLookupResult.cs
│   └── InteractionResult.cs
│
├── Services
│   ├── IDrugDataService.cs
│   ├── MockDrugDataService.cs
│   └── PostgresDrugDataService.cs
│
├── ViewModels
│   └── MainViewModel.cs
│
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── appsettings.example.json
└── DrugCompare.csproj
```

---

## Database Concept

The application uses two main data sources:

### EMA data

Used for mapping:

```text
drug/product name → active substance → manufacturer
```

### DDInter data

Used for mapping:

```text
active substance A + active substance B → interaction severity
```

---

## PostgreSQL Database Schema

The application expects the following main tables:

```text
drugs
active_substances
drug_active_substances
substance_interactions
data_source_versions
```

### `drugs`

Stores product/medicine names.

```text
id
name
normalized_name
manufacturer
source
created_at
```

### `active_substances`

Stores active substances.

```text
id
name
normalized_name
ddinter_id
source
created_at
```

### `drug_active_substances`

Stores the relation between drugs and their active substances.

```text
id
drug_id
active_substance_id
```

### `substance_interactions`

Stores known DDInter-based interactions between active substances.

```text
id
substance_a_id
substance_b_id
severity
source
last_updated
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

## Database Setup

Create a PostgreSQL database:

```sql
CREATE DATABASE drug_compare_db;
```

Then create the required tables using the SQL scripts prepared for the project.

The expected connection string format is:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=drug_compare_db;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

Do not commit your real `appsettings.json` file.

Use:

```text
appsettings.example.json
```

as a safe template.

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

The real `appsettings.json` file is ignored by Git because it can contain private credentials.

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
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Npgsql
```

---

## Data Import Workflow

### 1. EMA data

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

### 2. DDInter data

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

A future version should include:

```text
active_substance_synonyms
```

to improve matching.

---

## Current Status

Implemented:

* WPF user interface
* Drug lookup workflow
* Manual active substance entry
* Accepted active substance list
* PostgreSQL connection through Npgsql
* Local interaction checking logic
* EMA-based drug/substance database structure
* DDInter-based substance interaction structure

In progress / planned:

* Better synonym handling
* Improved fuzzy search
* Better UI severity badges
* Import automation from CSV
* Interaction history
* PDF/CSV report export
* Unit tests

---

## Example Test Flow

1. Run PostgreSQL.
2. Make sure `drug_compare_db` exists.
3. Make sure EMA and DDInter data are imported.
4. Start the WPF application.
5. Enter a drug name.
6. Accept detected active substances.
7. Manually add another active substance if needed.
8. Click `Check interactions`.
9. Review any detected interaction warnings.

---

## Security and Privacy

The application is local-only.

It does not send data to:

* cloud APIs
* external LLMs
* remote medical services

The PostgreSQL database runs locally unless the user configures another server.

---

## Repository Notes

The following files should not be committed:

```text
appsettings.json
*.csv
*.xlsx
bin/
obj/
.vs/
```

The repository should include:

```text
appsettings.example.json
README.md
.cs
.xaml
.csproj
.sln
```

---

## License / Data Source Notes

This project is a student/educational prototype.

Before using EMA or DDInter data in a public, commercial, or clinical environment, verify the licensing and usage terms of each dataset.

---

## Final Note

Drug Compare is not a clinical authority. It is a local software prototype that helps surface known substance-substance interactions from imported datasets. Every result must be clinically verified by qualified medical personnel.