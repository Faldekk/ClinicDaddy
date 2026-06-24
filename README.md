# ClinicDaddy

**ClinicDaddy** is a local-first Clinical Decision Support System prototype designed to support physicians with faster access to structured and verified medical information.

The application focuses on drug data, active substances, ICD codes, ChPL documents, interaction safety analysis and future source-based RAG functionality.

ClinicDaddy does **not** make autonomous clinical decisions. It supports the physician by organizing information, showing sources and describing medical relationships based only on verified local data.

---

## Purpose

The purpose of ClinicDaddy is to create a medical information support tool that helps physicians access relevant data faster during clinical work.

The system is designed to support:

* searching Polish medicinal products,
* mapping drugs to active substances,
* checking locally stored interaction information,
* browsing ICD codes,
* navigating ChPL documents by sections,
* presenting source-based explanations,
* preparing future RAG-based retrieval from verified medical sources.

The application is not intended to replace the physician’s knowledge, clinical experience, current guidelines or patient-specific assessment.

---

## Core Architecture

ClinicDaddy follows a modular, local-first architecture.

```text
ClinicDaddy
├── Presentation Layer
│   ├── WPF Views
│   ├── ViewModels
│   └── UI navigation
│
├── Application Layer
│   ├── models
│   ├── interfaces
│   ├── commands
│   └── business logic
│
├── Infrastructure Layer
│   ├── SQLite repositories
│   ├── data import services
│   ├── file readers
│   └── local storage
│
└── Local Data Layer
    ├── RPL / URPL data
    ├── ICD data
    ├── ChPL sections
    ├── verified knowledge records
    └── local interaction rules
```

The main architectural principle is separation of responsibility:

```text
UI displays data
ViewModels coordinate user actions
Application layer defines logic and contracts
Infrastructure layer handles storage and external formats
SQLite stores local verified data
```

---

## Local-First Data Strategy

ClinicDaddy is designed as a **local-first** system.

This means that runtime medical explanations should be based on data stored in the local database, not on uncontrolled live internet access.

The intended data flow is:

```text
verified source
→ import / parsing
→ local database
→ review status
→ application modules
→ source-based explanation
```

The system should not generate unsupported medical claims. If there is no verified local source, the system should clearly state that no verified data was found.

---

## Current Main Modules

### Polish Drug Registry

The Polish Drug Registry module stores and displays data from the Polish Register of Medicinal Products.

It contains information such as:

* product name,
* active substance text,
* strength,
* pharmaceutical form,
* marketing authorization holder,
* authorization number,
* ChPL URL,
* leaflet URL,
* source metadata.

This module is the main foundation for Polish drug data.

---

### ICD Looker

The ICD Looker module provides local ICD code search.

It supports:

* searching by ICD code,
* searching by title,
* searching by description,
* chapter filtering,
* parent code display,
* detailed ICD code preview.

The goal is to allow fast access to diagnostic codes without leaving the application.

---

### InteractionSafetyChecker

The InteractionSafetyChecker module is responsible for checking relationships between selected medicinal products or active substances.

The intended flow is:

```text
drug product
→ active substance
→ local verified interaction data
→ source-based explanation
→ physician interpretation
```

The module should never treat missing data as safety.

Required safety rule:

```text
No known interaction was found in the local database.
Missing interaction data does not mean that the combination is safe.
```

Polish version:

```text
Nie znaleziono znanej interakcji w lokalnej bazie.
Brak danych o interakcji nie oznacza, że połączenie jest bezpieczne.
```

---

### ChPL Navigator

The ChPL Navigator is planned as a structured document browser for ChPL files.

Instead of forcing the physician to manually scroll through long PDF documents, ChPL content should be split into sections.

Important sections include:

```text
4.1 Wskazania do stosowania
4.2 Dawkowanie i sposób podawania
4.3 Przeciwwskazania
4.4 Specjalne ostrzeżenia i środki ostrożności
4.5 Interakcje
4.6 Ciąża, laktacja i płodność
4.7 Prowadzenie pojazdów i obsługiwanie maszyn
4.8 Działania niepożądane
4.9 Przedawkowanie
5.1 Właściwości farmakodynamiczne
5.2 Właściwości farmakokinetyczne
5.3 Przedkliniczne dane o bezpieczeństwie
6.1 Wykaz substancji pomocniczych
```

This module is important for future source-based RAG because each ChPL section can become a searchable source chunk.

---

## ChPL Import Tool

The ChPL Import Tool is developed as a separate utility.

Its responsibility is to transform ChPL PDF files into structured data.

Planned processing pipeline:

```text
ChPL PDF
→ raw text extraction
→ section parsing
→ review status
→ JSON / CSV export
→ future import into ClinicDaddy database
```

The importer should not make medical decisions. It only prepares structured source material.

The current purpose of the tool is to support:

* extracting text from ChPL PDF files,
* splitting documents into meaningful sections,
* exporting structured data,
* preparing future local database imports.

---

## RAG Architecture

ClinicDaddy is planned to use RAG as a source retrieval and explanation layer.

RAG is not intended to make autonomous clinical decisions.

The intended RAG flow is:

```text
user question
→ identify drug / substance / ICD / topic
→ retrieve verified local chunks
→ generate source-based explanation
→ show citations / sources
→ include clinical responsibility disclaimer
```

RAG should only use sources stored in the local database.

Allowed source types may include:

```text
RPL / URPL records
ChPL sections
ICD records
reviewed scientific publications
reviewed manual notes
local verified interaction rules
```

If no verified source is available, RAG should not generate a medical explanation from general model knowledge.

Expected behavior:

```text
verified source found
→ answer with sources

no verified source found
→ no medical explanation
→ missing data warning
```

---

## Source-Based Explanation Principle

Every generated medical explanation should be traceable to a source.

A medical explanation should include:

* source type,
* source title,
* section number or chunk reference,
* review status,
* imported date,
* source URL or local reference where available.

Example:

```text
Relation:
substance A + substance B

Description:
...

Potential mechanism:
...

Source-based management information:
...

Sources:
- ChPL, section 4.5
- reviewed scientific publication
- local verified interaction rule

Note:
This information supports clinical assessment but does not replace the physician’s knowledge, experience or final clinical decision.
```

---

## Review Status

ClinicDaddy should distinguish between raw extracted data and verified medical knowledge.

Suggested review statuses:

```text
candidate
needs_review
reviewed
verified
deprecated
not_found
```

Only reviewed or verified records should be used for strong medical summaries.

Candidate records may be stored, but they should not be used as final interaction knowledge without human review.

---

## Planned Data Model

The future database should support traceability and source-based explanations.

Suggested domains:

```text
polish_drug_registry_items
active_substances
drug_substances
active_substance_synonyms

icd_codes

chpl_documents
chpl_sections
chpl_section_keywords

knowledge_sources
knowledge_chunks

substance_facts
local_interaction_rules
management_notes

interaction_history
audit_log
```

### Example Responsibility of Tables

#### `polish_drug_registry_items`

Stores Polish drug registry product data.

#### `active_substances`

Stores normalized active substances.

#### `drug_substances`

Maps medicinal products to active substances.

#### `chpl_documents`

Stores metadata about imported ChPL documents.

#### `chpl_sections`

Stores parsed ChPL sections such as 4.3, 4.4, 4.5 and 6.1.

#### `knowledge_sources`

Stores source metadata.

Example source types:

```text
RPL
ChPL
ICD
ScientificPublication
ManualNote
LocalRule
```

#### `knowledge_chunks`

Stores searchable source fragments used by future RAG.

#### `local_interaction_rules`

Stores reviewed interaction information between substances.

#### `audit_log`

Stores important actions performed in the application.

---

## Data Sources

ClinicDaddy currently focuses on Polish and local verified data.

Current and planned sources:

```text
RPL / URPL
- medicinal products
- active substances
- ChPL links
- leaflet links

ChPL
- structured product information
- interactions
- contraindications
- warnings
- pharmacological sections

ICD
- local diagnosis code lookup

Reviewed scientific sources
- planned future source type for RAG

Local rules
- manually reviewed interaction and relationship records
```

Deprecated or removed direction:

```text
DDInter
EMA imports
unreviewed external interaction datasets
```

The project currently prioritizes RPL, ChPL, ICD and reviewed local sources.

---

## Safety and Responsibility

ClinicDaddy supports clinical information retrieval and relationship explanation.

The system does not:

* diagnose patients,
* prescribe treatment,
* replace clinical guidelines,
* replace physician judgment,
* make autonomous clinical decisions.

The physician remains responsible for interpreting the information in the context of:

* patient condition,
* medical history,
* laboratory results,
* comorbidities,
* current guidelines,
* contraindications,
* therapeutic alternatives,
* clinical experience.

Required safety principle:

```text
Brak danych w bazie nie oznacza braku ryzyka ani bezpieczeństwa danego połączenia.
```

---

## Technical Stack

Current technical direction:

```text
C#
.NET
WPF
MVVM
SQLite
local-first architecture
future local RAG / vector search
```

The application is designed to remain explainable, auditable and modular.

---

## Project Structure

Example structure:

```text
ClinicDaddy/
├── Application/
│   ├── Models/
│   ├── Repositories/
│   ├── Services/
│   └── Contracts/
│
├── Infrastructure/
│   ├── SQLite/
│   ├── Importers/
│   └── Data/
│
├── Presentation/
│   ├── Views/
│   ├── ViewModels/
│   └── Converters/
│
├── data/
│   └── clinicdaddy.db
│
├── database/
│   ├── schema.sql
│   └── sample_seed.sql
│
└── README.md
```

Actual structure may differ during development, but the goal is to keep the architecture modular.

---

## Development Direction

Planned development path:

```text
1. Stabilize Polish Drug Registry.
2. Stabilize ICD Looker.
3. Clean deprecated DDInter / EMA logic.
4. Improve Data Management view.
5. Add ChPL document import pipeline.
6. Add ChPL Navigator.
7. Add reviewed knowledge source storage.
8. Add local RAG over verified chunks.
9. Add source-based relationship explanations.
10. Expand InteractionSafetyChecker using reviewed local rules.
```

---

## Summary

ClinicDaddy is a local-first Clinical Decision Support System prototype focused on verified medical sources, interaction safety, ChPL navigation, ICD lookup and source-based explanations.

The core idea is simple:

```text
faster access to verified medical information
+ clear source traceability
+ no unsupported claims
+ no autonomous clinical decisions
```

ClinicDaddy is designed to support physicians, not replace them.
