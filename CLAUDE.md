# CLAUDE.md — Project44

## Project Overview

Project44 is a **C# console application** (.NET Framework 4.8) that parses international sanctions lists and watchlists (XML, CSV, Excel) and extracts structured profile data into standardized JSON output.

**Supported data sources**: OFAC SDN, UN Consolidated Sanctions (UNSCR), EU Consolidated Lists, Canadian PSTEL, NACTA (Pakistan), and generic XML/CSV/Excel formats.

## Repository Structure

All source files live in the **root directory** (no subdirectories). The project is a single Visual Studio solution with one console application project.

```
project44_20250702/
├── project44.sln              # Visual Studio solution (VS 2017+)
├── project44.csproj           # MSBuild project file (.NET Framework 4.8)
├── App.config                 # .NET runtime configuration
├── packages.config            # NuGet dependencies (legacy format)
│
├── Program.cs                 # Entry point — orchestrates file processing pipeline
├── DataParser.cs              # Universal file parser (XML, CSV, Excel detection)
├── DataModels.cs              # DTOs: NameExtractionResult, AddressExtractionResult, etc.
│
├── NameExtractor.cs           # Extracts first/middle/last/full names
├── AddressExtractor.cs        # Extracts structured addresses (street, city, country, postal)
├── AliasExtractor.cs          # Extracts aliases across OFAC, EU, Canadian, generic formats
├── IdentifierExtractor.cs     # Extracts CNIC, passport, national ID, SSN with regex validation
├── FatherNameExtractor.cs     # Extracts father/husband names (cultural/patronymic patterns)
├── DataExtractor.cs           # DateExtractor — extracts birth, death, listing dates
├── RecordTypeExtractor.cs     # Classifies records as INDIVIDUAL or ENTITY
├── RecordIdExtractor.cs       # Finds/generates record identifiers (SHA256 hashing)
│
├── FileLogger.cs              # Dual console + file logging utility
└── README.md
```

## Architecture

**Pattern**: Modular monolith using an **Extractor Pipeline** pattern.

```
Input Files (XML/CSV/Excel)
    → DataParser (format detection + structure discovery)
    → Dictionary<string, object> records
    → Parallel extraction by domain:
        ├── NameExtractor
        ├── AddressExtractor
        ├── AliasExtractor
        ├── IdentifierExtractor
        ├── FatherNameExtractor
        ├── DateExtractor (DataExtractor.cs)
        ├── RecordTypeExtractor
        └── RecordIdExtractor
    → Aggregated profile object
    → JSON output (Newtonsoft.Json)
```

- **All extractor classes use static methods** — they are stateless utility classes.
- **Namespace**: `ConsoleApp1` (legacy naming, used throughout).
- **Data intermediate format**: `Dictionary<string, object>` — the universal record representation passed between parsers and extractors.

## Build & Run

**Framework**: .NET Framework 4.8 (Windows / MSBuild)

```bash
# Build (requires MSBuild or Visual Studio)
msbuild project44.sln /p:Configuration=Debug
msbuild project44.sln /p:Configuration=Release

# Or via dotnet CLI if SDK-style migration is done
# Currently uses legacy .csproj format — MSBuild is required
```

**Output paths**: `bin\Debug\` or `bin\Release\`

**Single external dependency**: `Newtonsoft.Json 13.0.3` (via NuGet `packages.config`)

## Configuration

There are **no .env files or external config files**. Configuration is hardcoded in `Program.cs`:

- **Input folder path**: Currently set to a Windows-specific path (`D:\Faizan\...`)
- **Output folder path**: Derived by replacing `input` with `output` in the input path
- **Record limit**: Variable controlling how many records to process (0 = all)

When working on this project, be aware that paths need to be updated for different environments.

## Testing

**No formal test framework** is configured. Validation happens at runtime through:
- Try-catch error handling in extractors
- File-based logging (timestamped logs in output folder)
- Console output for progress tracking

## Linting / Formatting

**No linting or formatting tools** are configured (no .editorconfig, StyleCop, or Roslyn analyzers).

## Key Conventions

### Code Style
- **Static utility classes** for all extractors — no instance state, no dependency injection
- **Multi-strategy extraction**: Each extractor tries multiple strategies in priority order (e.g., NameExtractor tries UNSCR patterns, then individual fields, then full name parsing)
- **Defensive coding**: Extensive null checks, try-catch blocks, and fallback strategies
- **Detailed logging**: Use `FileLogger` methods (`LogMessage`, `LogProgress`, `LogSuccess`, `LogError`, `LogWarning`) for all diagnostic output

### Naming
- Extractor classes: `{Domain}Extractor` (e.g., `NameExtractor`, `AddressExtractor`)
- Result models: `{Domain}Result` or `{Domain}ExtractionResult` (defined in `DataModels.cs`)
- Note: `DataExtractor.cs` actually contains the **DateExtractor** class

### Adding New Extractors
1. Create a new `{Domain}Extractor.cs` file with static extraction methods
2. Add corresponding result model(s) to `DataModels.cs`
3. Integrate the extractor call in `Program.cs` within the record processing loop
4. Use `FileLogger` for any diagnostic output

### Data Format Handling
- XML parsing uses `System.Xml.Linq` (`XDocument`, `XElement`)
- `DataParser.XmlStructureDiscovery` auto-discovers XML schema without requiring predefined schemas
- CSV and Excel parsers exist as **placeholders only** (not yet implemented)

## Git Workflow

- **3 commits** in history as of initial setup (July 2025)
- No CI/CD pipelines configured
- No branch protection rules observed
- Commit messages should be descriptive of the change

## Important Notes

- The project targets **Windows** (.NET Framework 4.8) — it is not cross-platform (.NET Core/5+)
- File paths in `Program.cs` are Windows-specific and hardcoded
- The `ConsoleApp1` namespace is a legacy artifact — do not rename without updating all files
- `DataExtractor.cs` is misnamed — it contains date extraction logic (`DateExtractor` class)
- Regex patterns in extractors (especially `IdentifierExtractor`) are carefully tuned for specific ID formats — modify with caution
