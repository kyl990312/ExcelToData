# TableAutomationTool 실행 가이드

## 목차
1. [시스템 개요](#시스템-개요)
2. [프로젝트 구조](#프로젝트-구조)
3. [설치 및 환경 설정](#설치-및-환경-설정)
4. [Excel 파일 준비](#excel-파일-준비)
5. [ExcelToData 사용법](#exceltodata-사용법)
6. [CodeGenerator 사용법](#codegenerator-사용법)
7. [EnumGenerator 사용법](#enumgenerator-사용법)
8. [통합 워크플로우](#통합-워크플로우)
9. [고급 설정](#고급-설정)
10. [문제 해결](#문제-해결)
11. [실전 예제](#실전-예제)

## 시스템 개요

TableAutomationTool은 Excel 파일을 기반으로 다양한 형태의 코드와 데이터를 자동 생성하는 도구 모음입니다.

### 주요 도구들

| 도구명 | 입력 | 출력 | 목적 |
|--------|------|------|------|
| **ExcelToData** | Excel 파일 (.xlsm) | Binary/JSON 파일 | 데이터 추출 및 변환 |
| **CodeGenerator** | Binary 파일 (.bin) | C#/C++ 클래스 | 코드 자동 생성 |
| **EnumGenerator** | Excel 파일 (.xlsm) | Enum 코드 (.cs) | 열거형 생성 |

### 데이터 플로우

```
Excel 파일 → ExcelToData → Binary 파일 → CodeGenerator → C#/C++ 클래스
     ↓
EnumGenerator → Enum 파일
```

## 프로젝트 구조

```
TableAutomationTool/
├── BinaryParser/               # 바이너리 읽기/쓰기 라이브러리
│   ├── BinaryReader.cs         # 바이너리 → Dictionary 변환
│   ├── BinaryWriter.cs         # Dictionary → 바이너리 변환
│   ├── BinarySchema.cs         # 바이너리 스키마 파싱
│   └── Cpp/                    # C++ 바이너리 리더
│       ├── BinaryReader.h
│       └── BinaryReader.cpp
├── ExcelParser/                # Excel 파싱 라이브러리
│   ├── ExcelGenerator.cs       # Excel 파일 생성
│   ├── TableConverter.cs       # 테이블 데이터 변환
│   ├── TableHelper.cs          # 테이블 유틸리티
│   ├── CustomDataTable.cs      # 커스텀 데이터 테이블
│   ├── TableHeader.cs          # 테이블 헤더 정보
│   └── UserEnum/               # 사용자 정의 Enum 관리
│       ├── EnumDB.cs
│       ├── EnumCache.cs
│       └── SharedEnumMemoryHandler.cs
├── Generator/                  # 코드/데이터 생성 코어
│   ├── CodeGenerator/          # 코드 생성기
│   │   ├── CodeGenerator.cs    # 기본 코드 생성기
│   │   ├── CSharpGenerator.cs  # C# 클래스 생성기
│   │   ├── CppGenerator.cs     # C++ 클래스 생성기
│   │   └── EnumCodeGenerator.cs # Enum 코드 생성기
│   └── DataExtractor/          # 데이터 추출기
│       ├── StructuredBinaryExtractor.cs # 바이너리 추출기
│       ├── JsonExtractor.cs    # JSON 추출기
│       └── BytesExtractorFromGSheet.cs # Google Sheets 추출기
├── CodeGenerator/              # 코드 생성 CLI 애플리케이션
│   ├── Program.cs
│   ├── CodeGenerateConfig.cs
│   └── Commands/
├── ExcelToData/                # 데이터 추출 CLI 애플리케이션
│   ├── Program.cs
│   ├── DataExtractConfig.cs
│   └── Commands/
├── EnumGenerator/              # Enum 생성 CLI 애플리케이션
│   ├── Program.cs
│   ├── EnumGeneratorConfig.cs
│   └── Commands.cs
├── GAuth/                      # Google API 인증 라이브러리
│   ├── GAuthService.cs
│   └── GSheetHandler.cs
├── Common/                     # 공통 유틸리티
│   ├── Command/                # 커맨드 패턴
│   └── Utils/                  # 유틸리티 클래스
├── TableAutomationToolShared/  # 공유 설정 및 경로
│   ├── SystemDefine.cs
│   ├── SystemPath.cs
│   └── SharedCommands.cs
└── Tester/                     # 테스트 프로젝트
```

### 핵심 라이브러리

| 프로젝트 | 설명 |
|----------|------|
| **BinaryParser** | 구조화된 바이너리 형식 읽기/쓰기, C++ 호환 리더 포함 |
| **ExcelParser** | ClosedXML 기반 Excel 파싱 및 테ーブル 변환 |
| **Generator** | 코드 생성기 및 데이터 추출기 핵심 로직 |
| **GAuth** | Google Sheets API 연동 |
| **Common** | 로거, 커맨드 패턴 등 공통 유틸리티 |

## 파이프라인 아키텍처

### 다이어그램 범례

| 표기 | 의미 |
|------|------|
| 파란색 사각형 | Program (실행 프로그램) |
| 점선 타원 | Data (데이터 저장소) |

### 전체 흐름 다이어그램

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  EnumGenerator  │     │   ExcelToData   │     │  CodeGenerator  │
│    (Program)    │     │    (Program)    │     │    (Program)    │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         │ Write                 │ Write/Read            │ Read/Write
         ▼                       ▼                       │
    ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐          │
    │       EnumBinary (EnumInfo)           │◀─────────┤ Read
    └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘          │
                       │                                │
                       ▼                                │
    ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐          │
    │            DataBinary                 │◀─────────┤ Read
    └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘          │
                       │                                │
                       ▼                                │
    ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐          │
    │              Code                     │◀─────────┘ Write
    └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘
                       │
                       ▼
    ╔═══════════════════════════════════════╗
    ║             External                  ║
    ╚═══════════════════════════════════════╝
```

### 데이터 흐름 요약

| Program | Read | Write |
|---------|------|-------|
| EnumGenerator | - | EnumBinary |
| ExcelToData | EnumBinary | DataBinary |
| CodeGenerator | EnumBinary, DataBinary | Code |

### 공유 데이터 저장소

| 저장소 | 목적 | Write | Read |
|--------|------|-------|------|
| **EnumBinary** | 사용자 정의 Enum 타입 정보 | EnumGenerator | ExcelToData, CodeGenerator |
| **DataBinary** | 변환된 게임 데이터 | ExcelToData | CodeGenerator |
| **Code** | 자동 생성된 C# 소스 코드 | CodeGenerator | External |

## 설치 및 환경 설정

### 1. 시스템 요구사항

**필수 요구사항**
- Windows 10/11, macOS 10.15+, 또는 Linux
- .NET 9.0 Runtime
- 최소 512MB RAM
- 100MB 이상 디스크 공간

**권장 사항**
- Visual Studio 2022 또는 VS Code (개발시)
- 4GB 이상 RAM (대용량 Excel 파일 처리시)

### 2. 빌드 및 실행

**소스에서 빌드**
```bash
# 리포지토리 클론
git clone https://github.com/kyl990312/ExcelToData.git
cd ExcelToData

# 솔루션 빌드
dotnet build TableAutomationTool.sln --configuration Release

# 실행 파일 위치
# bin/Release/net9.0/ExcelToData.exe
# bin/Release/net9.0/CodeGenerator.exe
# bin/Release/net9.0/EnumGenerator.exe
```

**직접 실행**
```bash
# 각 프로젝트 디렉토리에서
cd ExcelToData
dotnet run

cd ../CodeGenerator  
dotnet run

cd ../EnumGenerator
dotnet run
```

### 3. 프로젝트 구조 설정

권장 프로젝트 디렉토리 구조:
```
MyProject/
├── Data/
│   ├── Excel/              # 원본 Excel 파일들
│   │   ├── PlayerData.xlsm
│   │   ├── ItemData.xlsm
│   │   └── SystemEnum.xlsm
│   └── Binary/             # 변환된 Binary 파일들
├── Generated/
│   ├── Classes/            # 자동 생성된 클래스들
│   ├── Enums/              # 자동 생성된 Enum들
│   └── Managers/           # 테이블 관리자 (미래 확장)
├── Config/                 # 설정 파일들
│   ├── BinaryConfig.json
│   ├── JsonConfig.json
│   ├── CodeGenerateConfig.json
│   └── EnumGeneratorConfig.json
└── Tools/                  # 실행 파일들
    ├── ExcelToData.exe
    ├── CodeGenerator.exe
    └── EnumGenerator.exe
```

## Excel 파일 준비

### 1. 기본 Excel 구조

Excel 파일은 다음과 같은 구조를 따라야 합니다:

```
| Id    | Name     | Level | Skills        | #Comment |
| Int32 | String   | Int32 | String[]      | String   |
| 1     | "Warrior"| 10    | [Slash,Shield]| "전사"    |
| 2     | "Mage"   | 8     | [Fireball]    | "마법사"  |
| //    | 주석행   |       |               |          |
| 3     | "Archer" | 12    | [Arrow,Aim]   | "궁수"    |
| EOF   |          |       |               |          |
```

### 2. 규칙 및 제약사항

**헤더 규칙**
- **1행**: 컬럼명 (PascalCase 권장)
- **2행**: 데이터 타입 (System. 접두사 포함)
- **3행부터**: 실제 데이터

**특수 마커**
- `#`: 컬럼명 앞에 붙여서 해당 컬럼 제외
- `//`: 행의 첫 번째 셀에 붙여서 해당 행 제외 (주석)
- `EOF`: 데이터 끝을 표시

**지원하는 데이터 타입**
- **숫자**: Int16, Int32, Int64, Single, Double
- **문자열**: String
- **배열**: 타입[] (예: Int32[], String[])
- **열거형**: 사용자 정의 Enum 타입

**배열 표기법**
```
[값1,값2,값3]  # 올바른 형식
[값1, 값2, 값3]  # 공백 포함 가능
```

### 3. Excel 파일 예제

**PlayerData.xlsm**
```
| Id    | Name     | Level | Hp   | Mp   | Skills          |
| Int32 | String   | Int32 | Int32| Int32| String[]        |
| 1     | Warrior  | 10    | 100  | 20   | [Slash,Shield]  |
| 2     | Mage     | 8     | 60   | 100  | [Fireball,Heal] |
| 3     | Archer   | 12    | 80   | 40   | [Arrow,Aim]     |
| EOF   |          |       |      |      |                 |
```

**ItemData.xlsm**
```
| Id    | Name        | Type     | Rarity  | Price |
| Int32 | String      | ItemType | ItemRarity| Int32 |
| 1001  | Iron Sword  | Weapon   | Common  | 100   |
| 1002  | Magic Staff | Weapon   | Rare    | 500   |
| 2001  | Leather Armor| Armor   | Common  | 150   |
| EOF   |             |          |         |       |
```

**SystemEnum.xlsm** (Enum 정의용)
```
| enumType   | member    | value |
| String     | String    | Int32 |
| ItemType   | Weapon    | 1     |
| ItemType   | Armor     | 2     |
| ItemType   | Accessory | 3     |
| ItemRarity | Common    | 0     |
| ItemRarity | Rare      | 1     |
| ItemRarity | Epic      | 2     |
| EOF        |           |       |
```

## ExcelToData 사용법

### 1. 기본 실행

```bash
# 실행 파일 경로로 이동
cd Tools
./ExcelToData.exe
```

### 2. 첫 실행 (설정 파일 생성)

첫 실행시 자동으로 설정 파일이 생성됩니다:

**Config/BinaryConfig.json**
```json
{
  "SrcDirectory": "Data",
  "DstDirectory": "Output"
}
```

**Config/JsonConfig.json**
```json
{
  "SrcDirectory": "Data", 
  "DstDirectory": "Output"
}
```

### 3. 설정 파일 편집

실제 프로젝트 경로에 맞게 설정 파일을 편집합니다:

```json
{
  "SrcDirectory": "C:/MyProject/Data/Excel",
  "DstDirectory": "C:/MyProject/Data/Binary"
}
```

### 4. 재실행

설정 파일 편집 후 다시 실행하면 Excel 파일들이 Binary/JSON으로 변환됩니다:

```bash
./ExcelToData.exe
```

**실행 로그 예시**
```
[2024-04-05 10:30:15] [ExcelToData] Start Extract Data
[2024-04-05 10:30:15] Success to load. (file path : C:/MyProject/Data/Excel/PlayerData.xlsm)
[2024-04-05 10:30:15] Start Convert Worksheet (Sheet1)
[2024-04-05 10:30:15] Save File Byte C:/MyProject/Data/Binary/Sheet1.bin
[2024-04-05 10:30:15] Finish Convert Worksheet (Sheet1)
[2024-04-05 10:30:16] [ExcelToData] Complete Extract Binary (C:/MyProject/Data/Excel)
[2024-04-05 10:30:16] [ExcelToData] Quit Program
```

### 5. 출력 파일 확인

변환 완료 후 다음 파일들이 생성됩니다:

```
Data/Binary/
├── PlayerData.bin      # UTF-8 인코딩된 JSON 바이너리
├── ItemData.bin
└── SystemEnum.bin

Data/Json/             # JSON 형식도 함께 생성된 경우
├── PlayerData.json    # 사람이 읽을 수 있는 JSON
├── ItemData.json
└── SystemEnum.json
```

**PlayerData.bin 내용 (JSON 형식)**
```json
[
  {
    "Id": 1,
    "Name": "Warrior",
    "Level": 10,
    "Hp": 100,
    "Mp": 20,
    "Skills": ["Slash", "Shield"]
  },
  {
    "Id": 2,
    "Name": "Mage", 
    "Level": 8,
    "Hp": 60,
    "Mp": 100,
    "Skills": ["Fireball", "Heal"]
  }
]
```

## CodeGenerator 사용법

### 1. 기본 실행

```bash
cd Tools
./CodeGenerator.exe
```

### 2. 첫 실행 (설정 파일 생성)

**Config/CodeGenerateConfig.json**
```json
{
  "BinaryDirectory": "Binary",
  "ClassOutputDirectory": "Generated/Classes",
  "ManagerOutputDirectory": "Generated/Managers", 
  "ClassNamespace": "Generated.Data",
  "ManagerNamespace": "Generated.Managers",
  "ReadFileExtension": "*.bin"
}
```

### 3. 설정 파일 편집

프로젝트 구조에 맞게 설정을 조정합니다:

```json
{
  "BinaryDirectory": "C:/MyProject/Data/Binary",
  "ClassOutputDirectory": "C:/MyProject/Generated/Classes",
  "ManagerOutputDirectory": "C:/MyProject/Generated/Managers",
  "ClassNamespace": "MyProject.Data",
  "ManagerNamespace": "MyProject.Managers",
  "ReadFileExtension": "*.bin"
}
```

### 4. 재실행

설정 완료 후 재실행하면 C# 클래스들이 생성됩니다:

```bash
./CodeGenerator.exe
```

**실행 로그 예시**
```
[2024-04-05 10:35:20] [CodeGenerator] Start Generate Code
[2024-04-05 10:35:20] [GenerateCodeCommand] Generated PlayerData.cs
[2024-04-05 10:35:20] [GenerateCodeCommand] Generated ItemData.cs
[2024-04-05 10:35:20] [GenerateCodeCommand] Generated 2 classes.
[2024-04-05 10:35:20] [CodeGenerator] Quit Program
```

### 5. 생성된 코드 확인

**Generated/Classes/PlayerData.cs**
```csharp
// 자동 생성된 코드 - 수정하지 마세요
// 생성 시간: 2024-04-05 10:35:20

using System;
using System.Collections.Generic;

namespace MyProject.Data
{
    public class PlayerData
    {
        // 기본 생성자
        public PlayerData()
        {
        }

        // 모든 필드 초기화 생성자
        public PlayerData(int id, string name, int level, int hp, int mp, string[] skills)
        {
            Id = id;
            Name = name;
            Level = level;
            Hp = hp;
            Mp = mp;
            Skills = skills;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
        public int Level { get; private set; }
        public int Hp { get; private set; }
        public int Mp { get; private set; }
        public string[] Skills { get; private set; }
    }
}
```

## EnumGenerator 사용법

### 1. 기본 실행

```bash
cd Tools  
./EnumGenerator.exe
```

### 2. 첫 실행 (설정 파일 생성)

**Config/EnumGeneratorConfig.json**
```json
{
  "EnumFilePath": "SystemEnum.xml",
  "DstDirectory": "Enums/",
  "CodeExtension": ".cs"
}
```

### 3. 설정 파일 편집

```json
{
  "EnumFilePath": "C:/MyProject/Data/Excel/SystemEnum.xlsm",
  "DstDirectory": "C:/MyProject/Generated/Enums/",
  "CodeExtension": ".cs"
}
```

### 4. 재실행

```bash
./EnumGenerator.exe
```

**실행 로그 예시**
```
[2024-04-05 10:40:15] [ExcelToData] Start Extract Data
[2024-04-05 10:40:15] [GenerateEnumCommand] Create Enums from SystemEnum.xlsm>Sheet1 : ItemType,ItemRarity
[2024-04-05 10:40:15] [GenerateEnumCommand] Complete Create Enums SystemEnum.xlsm
[2024-04-05 10:40:15] [ExcelToData] Quit Program
```

### 5. 생성된 Enum 확인

**Generated/Enums/Sheet1.cs**
```csharp
public enum ItemType
{
    Weapon = 1,
    Armor = 2,
    Accessory = 3
}

public enum ItemRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2
}
```

## 통합 워크플로우

### 1. 전체 파이프라인 실행

```bash
# 1단계: Excel을 Binary로 변환
cd Tools
./ExcelToData.exe

# 2단계: Enum 생성 (필요시)
./EnumGenerator.exe

# 3단계: Binary를 C# 클래스로 변환
./CodeGenerator.exe
```

### 2. 배치 스크립트 작성

**Windows (run_all.bat)**
```batch
@echo off
echo Starting TableAutomationTool Pipeline...

echo Step 1: Converting Excel to Binary...
ExcelToData.exe
if errorlevel 1 goto error

echo Step 2: Generating Enums...
EnumGenerator.exe
if errorlevel 1 goto error

echo Step 3: Generating C# Classes...
CodeGenerator.exe
if errorlevel 1 goto error

echo Pipeline completed successfully!
goto end

:error
echo Error occurred during pipeline execution!

:end
pause
```

**Linux/macOS (run_all.sh)**
```bash
#!/bin/bash
echo "Starting TableAutomationTool Pipeline..."

