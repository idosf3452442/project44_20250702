# Project44 - Universal File Parser

## Overview
Universal file parser that converts various sanctions list formats (XML, CSV, Excel) into standardized JSON.

## Current Status
- ✅ XML parsing (fully implemented)
- 🔄 CSV parsing (in development)  
- 🔄 Excel parsing (planned)

## Files Structure
- `Program.cs` - Main application entry point
- `FileLogger.cs` - Logging functionality
- `XmlStructureDiscovery.cs` - XML structure discovery
- `SmartFieldMapper.cs` - Smart field mapping logic
- `IdentifierExtractor.cs` - ID extraction and generation

## Technology Stack
- C# (.NET Framework/Core)
- Newtonsoft.Json for JSON processing
- System.Xml.Linq for XML parsing

## Usage
1. Configure input/output folder paths in Program.cs
2. Place sanctions files in input folder
3. Run the application
4. Check output folder for generated JSON files

## Next Development Steps
1. Implement CSV parser
2. Implement Excel parser
3. Add configuration-driven field mapping
4. Enhance address and date extraction
