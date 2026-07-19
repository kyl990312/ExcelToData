# TableAutomationTool 시스템 구조

**작성일**: 2026-02-22  
**프로젝트**: TableAutomationTool (.NET 9.0)  
**GitHub**: https://github.com/kyl990312/ExcelToData

---

## 1. 시스템 개요

TableAutomationTool은 Excel 및 Google Sheets 데이터를 게임 런타임에서 사용 가능한 Binary, JSON, C# 코드로 변환하는 자동화 파이프라인 시스템이다. 변환된 데이터와 코드는 Unity 프로젝트의 Local 저장소로 출력되며, GameDataTool(Editor)과 GameDataManager를 통해 게임 런타임에서 활용된다.

---

## 2. 전체 구조도

```
                         Commands
                    ┌─────────────────────┐
                    │  ExtractBinary All   │
                    │  ExtractBinary Each  │
                    │  Code Generate All   │
                    │  Code Generate Each  │
                    └──────────┬──────────┘
                               ▼
               ┌──────────────────────────────┐
               │     TableAutomationTool      │
               │                              │
               │  ┌────────────────────┐      │       ┌─────────────────────┐
               │  │  EnumGenerator     │      │       │                     │
               │  └────────────────────┘      │       │  GameDataTool       │
               │  ┌────────────────────┐      │       │  (Editor)           │
               │  │  ExcelToData       │──┐   │       │                     │
               │  └────────────────────┘  │   │       └─────────────────────┘
               │  ┌────────────────────┐  │   │                 ▲
               │  │  CodeGenerator     │──┤   │                 │
               │  └────────────────────┘  │   │       ┌─────────┴───────────┐
               │                          │   │       │                     │
               │              ┌───────┐   │   │       │  GameDataManager    │
               │              │Binary │◄──┤   │       │                     │
               │              └───────┘   │   │       └─────────────────────┘
               │              ┌───────┐   ├───┼──►  Local Storage
               │              │Scripts│◄──┘   │
               │              └───────┘       │       UnityProject 영역
               └──────────────────────────────┘
```

---

## 3. 핵심 컴포넌트

### 3.1 TableAutomationTool (데이터 파이프라인)

TableAutomationTool은 세 개의 실행 프로그램으로 구성된다. 각 프로그램은 Command Pattern 기반의 CommandInvoker를 통해 작업을 수행한다.

**EnumGenerator** — Excel에서 사용자 정의 Enum을 추출하여 EnumBinary와 C# enum 코드를 생성한다. 다른 프로그램들이 Enum 타입 정보에 의존하므로 가장 먼저 실행해야 한다.

**ExcelToData** — Excel 파일(.xlsm) 또는 Google Sheets를 읽어 구조화된 Binary(.bin)와 JSON 데이터로 변환한다. Local 파일과 Google Sheets 두 가지 데이터 소스를 지원한다.

**CodeGenerator** — 변환된 Binary 파일을 읽어 타입 안전한 C# 클래스를 자동 생성한다. 생성된 클래스에는 프로퍼티, 생성자, ToString(), Equals(), GetHashCode() 메서드가 포함된다.

### 3.2 출력물

**Binary** — 컬럼 기반 저장 방식의 바이너리 데이터 파일이다. 게임 런타임에서 BinaryParser를 통해 로딩되며, 캐시 효율성과 쿼리 성능이 최적화되어 있다.

**Scripts** — CodeGenerator와 EnumGenerator가 생성한 C# 소스 코드이다. 데이터 클래스와 Enum 정의가 포함된다.

### 3.3 Local Storage

Binary 데이터와 Scripts가 저장되는 공유 저장소 영역이다. TableAutomationTool의 출력 결과물이 이곳에 적재되며, Unity 프로젝트에서 이를 읽어들여 사용한다.

### 3.4 UnityProject 영역

**GameDataTool (Editor)** — Unity Editor 상에서 Binary 데이터를 조회하고 관리하기 위한 GUI 에디터 도구이다. 데이터 Diff 비교, 개별 로딩 등의 기능을 제공한다.

**GameDataManager** — 게임 런타임에서 Binary 데이터를 메모리에 로딩하고, 생성된 C# 클래스를 통해 타입 안전하게 접근할 수 있도록 하는 매니저 컴포넌트이다.

---

## 4. Commands (실행 명령 체계)

구조도 상단에 표시된 4가지 Command는 시스템이 지원하는 실행 모드를 나타낸다.

| Command | 대상 | 설명 |
|---------|------|------|
| **ExtractBinary All** | ExcelToData | 모든 Excel 파일을 일괄 Binary로 변환 |
| **ExtractBinary Each** | ExcelToData | 지정된 개별 Excel 파일만 Binary로 변환 |
| **Code Generate All** | CodeGenerator | 모든 Binary 파일로부터 일괄 코드 생성 |
| **Code Generate Each** | CodeGenerator | 지정된 개별 Binary 파일로부터 코드 생성 |

All 모드는 전체 데이터를 일괄 처리하는 배치 실행 방식이며, Each 모드는 변경된 파일만 선택적으로 처리하는 개별 실행 방식이다.

---

## 5. 데이터 흐름

### 5.1 전체 파이프라인 실행 순서

```
[1] EnumGenerator 실행
    └─ Excel(Enum 시트) → EnumBinary + Enum C# 코드

[2] ExcelToData 실행
    └─ Excel / Google Sheets → Binary(.bin) + JSON(.json)

[3] CodeGenerator 실행
    └─ Binary(.bin) + EnumBinary → C# 클래스(.cs)

[4] Local Storage에 결과물 적재
    └─ Binary + Scripts → Unity 프로젝트 접근 가능

[5] Unity에서 활용
    └─ GameDataManager: 런타임 데이터 로딩
    └─ GameDataTool: Editor에서 데이터 관리
```

### 5.2 데이터 의존 관계

```
EnumGenerator ──write──► EnumBinary ◄──read── ExcelToData
                                    ◄──read── CodeGenerator

ExcelToData ──write──► DataBinary ◄──read── CodeGenerator

CodeGenerator ──write──► C# Code ──► UnityProject
```

| 데이터 저장소 | 생성자 | 소비자 |
|--------------|--------|--------|
| EnumBinary | EnumGenerator | ExcelToData, CodeGenerator |
| DataBinary | ExcelToData | CodeGenerator |
| C# Code | CodeGenerator, EnumGenerator | UnityProject |

---

## 6. 라이브러리 의존 구조

```
Common (기반 라이브러리)
├── Logger, FileLoader, Singleton
├── Command Pattern (ICommand, CommandInvoker)
│
├── ExcelParser (Excel 파싱)
│   └── TableHeader, TableHelper, CustomDataTable
│
├── BinaryParser (바이너리 직렬화)
│   └── BinaryWriter, BinaryReader, BinaryHelper
│
├── Generator (코드 생성 & 데이터 추출)
│   └── CSharpClassGenerator, StructuredBinaryExtractor
│
└── GAuth (Google OAuth 인증)
    └── GAuthService, GAuth.Core
```

실행 프로그램들은 위 라이브러리들을 조합하여 사용한다.

| 실행 프로그램 | 참조 라이브러리 |
|--------------|---------------|
| ExcelToData | Common, ExcelParser, BinaryParser, Generator, GAuth |
| CodeGenerator | Common, BinaryParser, Generator |
| EnumGenerator | Common, ExcelParser, Generator |

---

## 7. 설정 파일 구조

각 프로그램은 JSON 기반 설정 파일을 사용하며, 최초 실행 시 자동 생성된다.

| 설정 파일 | 대상 프로그램 | 주요 설정 |
|-----------|-------------|-----------|
| BinaryConfig.json | ExcelToData | SrcDirectory, DstDirectory, IsLoadFromLocal, IsLoadFromGSheet |
| JsonConfig.json | ExcelToData | SrcDirectory, DstDirectory |
| CodeGenerateConfig.json | CodeGenerator | BinaryDirectory, ClassOutputDirectory, ClassNamespace |
| EnumGeneratorConfig.json | EnumGenerator | EnumFilePath, DstDirectory, CodeExtension |

---

## 8. 핵심 설계 패턴

**Command Pattern** — 각 실행 프로그램의 작업 단위를 Command로 캡슐화한다. CommandInvoker가 Command 목록을 생성하고 순차 실행하는 구조이다. 새로운 작업 추가 시 기존 코드를 수정하지 않고 Command 클래스만 추가하면 된다.

**Template Method Pattern** — DataExtractorBase가 데이터 추출의 골격을 정의하고, 구체적인 직렬화 방식(Binary, JSON)은 하위 클래스에서 구현한다.

**Config-First Pattern** — 모든 도구가 최초 실행 시 기본 설정 파일을 자동 생성한다. 사용자는 설정 파일을 편집한 후 재실행하는 방식으로 동작한다.

**Columnar Storage** — Binary 파일은 행(Row) 기반이 아닌 열(Column) 기반으로 데이터를 저장한다. 게임에서 "모든 아이템 ID 조회"와 같은 컬럼 단위 쿼리에 최적화되어 있다.

---

## 9. Excel 파일 규칙

TableAutomationTool이 처리하는 Excel 파일은 다음 구조를 따라야 한다.

| 행 | 용도 | 예시 |
|----|------|------|
| 1행 | 컬럼명 (PascalCase 권장) | Id, Name, Level, Skills |
| 2행 | 데이터 타입 | Int32, String, Int32, String[] |
| 3행~ | 실제 데이터 | 1, Warrior, 10, [Slash,Block] |

**특수 마커**:  `#` 접두사(컬럼 제외), `//` 행 주석, `EOF` 데이터 종료

**지원 타입**: Int16, Int32, Int64, Single, Double, String, Boolean, 배열(String[], Int32[] 등), 사용자 정의 Enum
