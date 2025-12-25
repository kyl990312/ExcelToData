# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TableAutomationTool is a .NET 9.0 solution that automates the conversion of Excel files to game data formats (Binary/JSON) and auto-generates C# classes and enums. The project is primarily documented in Korean (see README.md).

## Build and Run Commands

**Build entire solution:**
```bash
dotnet build TableAutomationTool.sln --configuration Release
```

**Run individual tools:**
```bash
# From project root
cd ExcelToData && dotnet run
cd CodeGenerator && dotnet run
cd EnumGenerator && dotnet run
cd Tester && dotnet run
```

**Run compiled executables (after build):**
```bash
# From Output/Release/net9.0/ or Output/Debug/net9.0/
./ExcelToData.exe
./CodeGenerator.exe
./EnumGenerator.exe
```

**Development workflow:**
```bash
# 1. Generate enums first (other code may depend on them)
dotnet run --project EnumGenerator

# 2. Convert Excel to Binary
dotnet run --project ExcelToData

# 3. Generate C# classes from Binary
dotnet run --project CodeGenerator
```

## Solution Architecture

The solution is organized into two main groups:

### Console Applications (Entry Points)
- **ExcelToData**: Converts Excel files (.xlsm) to Binary/JSON data files
- **CodeGenerator**: Generates C# classes from Binary files
- **EnumGenerator**: Generates C# enums from Excel files
- **Tester**: Testing/debugging tool

### Library Projects (Shared Components)
- **Common**: Core utilities (Logger, FileLoader, Command pattern abstractions)
- **ExcelParser**: Excel file parsing and data extraction (TableCell, TableHeader, CustomDataTable)
- **BinaryParser**: Binary file reading/writing operations
- **Generator**: Code generation utilities and templates
- **GAuth**: Google authentication for Google Sheets integration

## Core Design Patterns

### Command Pattern
The codebase uses a Command pattern for organizing operations:

```
CommandInvoker (abstract)
  ├── CreateCommands() → List<ICommand>
  └── ExecuteAll() → executes each command

Each console app implements:
  - ETDCommandInvoker (ExcelToData)
  - CGCommandInvoker (CodeGenerator)
  - Each creates app-specific Command implementations
```

**Key Files:**
- `Common/Command/CommandInvoker.cs` - Base invoker
- `Common/Command/Command.cs` - ICommand interface and base class
- Each console app's `Commands/` folder - Specific implementations

### Singleton Pattern
Used for shared utilities like Logger:
- `Common/Utils/Logger.cs` - Centralized logging
- `Common/Utils/Singleton.cs` - Generic singleton base

## Configuration Files

Each tool uses JSON configuration files (auto-generated on first run):

**ExcelToData:**
- `Config/BinaryConfig.json` - Excel to Binary conversion settings
- `Config/JsonConfig.json` - Excel to JSON conversion settings

**CodeGenerator:**
- `Config/CodeGenerateConfig.json` - Code generation settings (namespaces, paths)

**EnumGenerator:**
- `Config/EnumGeneratorConfig.json` - Enum generation settings

**Important:** Config files specify absolute paths. Update these when changing project structure.

## Data Flow Pipeline

```
Excel (.xlsm) → [ExcelToData] → Binary (.bin) + JSON (.json)
     ↓
[EnumGenerator] → C# Enums (.cs)
     ↓
Binary (.bin) → [CodeGenerator] → C# Classes (.cs)
```

## Excel File Format Rules

Excel files must follow a specific structure (see README.md for full details):
- **Row 1**: Column names (PascalCase recommended)
- **Row 2**: Data types (Int32, String, String[], custom enums, etc.)
- **Row 3+**: Actual data
- **Special markers**:
  - `#` prefix on column name = exclude column
  - `//` in first cell = comment out row
  - `EOF` = end of data marker

## Testing

The `Tester` project is used for development and debugging. When adding new features or fixing bugs in the parsers or generators, add corresponding tests here.

## Code Review Rules

The project has Copilot review rules configured (`.github/copilot-review-config.json`):
- **CrashCheck**: Detects null references, division by zero, out of bounds errors
- **LogicCheck**: Reviews incorrect conditions, wrong loop logic, off-by-one errors
- Excludes: `bin/`, `obj/` directories

## Output Directory Structure

After running the pipeline:
```
Output/
├── Debug/ or Release/
│   └── net9.0/
│       ├── *.exe (executables)
│       ├── Config/ (configuration files)
│       └── Data/
│           ├── Binary/ (*.bin files)
│           └── Json/ (*.json files)
```

## Important Notes

- **Target Framework**: .NET 9.0
- **Primary Language**: C# with Korean documentation
- **Main Documentation**: README.md (comprehensive Korean guide)
- **Config First**: All tools auto-generate config files on first run - edit these before re-running
- **Execution Order Matters**: Run EnumGenerator → ExcelToData → CodeGenerator for full pipeline
