# TableAutomationTool 실행 가이드

Excel / Google Sheets로 작성한 게임 데이터 테이블을 **바이너리(.bin)·JSON**으로 변환하고,
그 결과로부터 **C# 클래스**와 **Enum 코드**를 자동 생성하는 도구 모음입니다.

## 목차

1. [시스템 개요](#시스템-개요)
2. [실행 파일 · 명령어 레퍼런스](#실행-파일--명령어-레퍼런스) — **자동화·AI 스킬 작성자는 여기부터**
3. [프로젝트 구조](#프로젝트-구조)
4. [파이프라인 아키텍처](#파이프라인-아키텍처)
5. [설치 및 빌드](#설치-및-빌드)
6. [설정 파일](#설정-파일)
7. [테이블 작성 규칙](#테이블-작성-규칙)
8. [TableAutomationTool — 통합 CLI](#tableautomationtool--통합-cli)
9. [diff — 변경 테이블 목록](#diff--변경-테이블-목록)
10. [TableEditor — GUI](#tableeditor--gui)
11. [Google Sheets 연동](#google-sheets-연동)
12. [통합 워크플로우](#통합-워크플로우)
13. [AI 에이전트 스킬 작성 가이드](#ai-에이전트-스킬-작성-가이드)
14. [문제 해결](#문제-해결)
15. [개별 파일 처리(-each)](INDIVIDUAL_FILE_PROCESSING.md)

---

## 시스템 개요

### 실행 프로그램

| 프로그램 | 형태 | 입력 | 출력 | 목적 |
|---|---|---|---|---|
| **TableAutomationTool** | CLI (통합) | 서브 커맨드 | — | 모든 단계를 한 진입점에서 실행 |
| **TableEditor** | GUI (WPF) | — | — | 테이블 편집 + 추출/생성 실행 |

실행 파일은 이 둘뿐입니다. 예전의 `ExcelToData.exe` · `CodeGenerator.exe` · `EnumGenerator.exe` ·
`Tester.exe`는 제거되었고, 각각의 기능은 통합 CLI의 서브 커맨드가 되었습니다.

| 단계(서브 커맨드) | 입력 | 출력 | 목적 |
|---|---|---|---|
| `enum` | Enum 시트 | `Binary/EnumDB.bin` | 사용자 정의 Enum 정보 수집 |
| `extract` | `.xlsm` / `.xlsx` / GSheet | `.bin`, `.json` | 데이터 추출 및 변환 |
| `generate` | `.bin`, `EnumDB.bin` | C# 클래스 · Enum 코드 | 코드 자동 생성 |

> **Enum 코드(.cs) 파일은 CodeGenerator가 생성합니다.** EnumGenerator는 Enum 정보를
> `Binary/EnumDB.bin`으로 만들어 두는 역할까지만 담당합니다.

### 데이터 플로우

```
                      ┌───────────────┐
Enum 시트 ──────────▶ │ EnumGenerator │ ──▶ Binary/EnumDB.bin ─┐
                      └───────────────┘                        │
                                                               ▼
Excel/GSheet 테이블 ─▶ ┌──────────────┐ ──▶ *.bin ──▶ ┌───────────────┐ ──▶ C# 클래스
                       │  ExcelToData │ ──▶ *.json    │ CodeGenerator │ ──▶ Enum 코드
                       └──────────────┘               └───────────────┘
```

실행 순서는 항상 **EnumGenerator → ExcelToData → CodeGenerator** 입니다.
Enum 정보(`EnumDB.bin`)를 뒤의 두 단계가 모두 읽기 때문입니다.

---

## 실행 파일 · 명령어 레퍼런스

빌드하면 **`Output/<Configuration>/net9.0/`** 한 폴더에 모든 exe가 모입니다.
설정(`Config/`)과 `Binary/EnumDB.bin`을 공유하기 위한 의도적인 배치이므로,
**실행은 항상 이 폴더를 기준으로 합니다.**

```
Output/Release/net9.0/
├── TableAutomationTool.exe   # 통합 CLI  ← 자동화는 이것만 쓰면 됩니다
├── TableEditor.exe           # GUI (Windows 전용)
├── Config/                   # 설정 6종
└── Binary/EnumDB.bin         # enum 단계 산출물
```

### 실행 파일 한눈에 보기

| exe | 인자 | 읽는 설정 | 입력 | 출력 |
|---|---|---|---|---|
| **`TableAutomationTool.exe`** | `<command> [-each\|-f\|--file <이름>]` | 실행하는 단계의 설정 | — | — |
| `TableEditor.exe` | 없음 (GUI) | 설정 전부 | — | — |

### 자동화 시 반드시 알아야 할 제약

통합 CLI는 **비대화형 환경(스크립트·CI·AI 에이전트)에서 어떤 명령도 키 입력을 기다리지 않습니다.**
예전 개별 exe에 있던 `Press any key to exit...` 대기는 exe와 함께 제거되었습니다.

예외는 `diff all` · `diff extract` · `diff generate` 세 가지입니다. 이들은 **대화형 콘솔에서만**
Y/N 확인을 묻습니다. stdin이 리다이렉트된 환경에서는 묻지 않고 즉시 취소하며(종료 코드 0),
인자 없는 `diff`는 어떤 환경에서도 프롬프트가 없습니다. 자세한 내용은
[diff — 변경 테이블 목록](#diff--변경-테이블-목록)을 보십시오.

인자 없는 `diff`는 출력 스트림 규약도 다릅니다 — 결과 목록은 stdout, 나머지 메시지는
전부 stderr로 나갑니다(파이프로 넘기기 위한 설계). `diff`에 다음 단계를 붙여 Y로 진행한
경우에도 파이프라인 로그는 stderr로 나가므로 stdout에는 목록만 남습니다.

### 명령어 전체 목록

**TableAutomationTool.exe**

| 명령 | 별칭 | 하는 일 |
|---|---|---|
| `all` | `pipeline` | enum → extract → generate 전체 실행 |
| `extract` | `etd` | Excel/GSheet → `.bin`, `.bin` → `.json` |
| `generate` | `gen`, `cg` | `.bin` → 클래스 코드 (+ Enum 코드) |
| `enum` | — | Enum 시트 → `Binary/EnumDB.bin` |
| `init-config` | — | 설정 템플릿 6종 생성 |
| `test` | — | `Binary/EnumDB.bin` 공유 메모리 검증 |
| `diff` | — | git 기준 변경된 테이블 목록 출력 (프롬프트 없음) |
| `diff all` | — | 변경분만 전체 파이프라인 (Y/N 확인) |
| `diff extract` | `diff etd` | 변경분만 추출 (Y/N 확인) |
| `diff generate` | `diff gen`, `diff cg` | 변경분만 코드 생성 (Y/N 확인) |
| `help` | `-h`, `--help`, `?` | 도움말 출력 |

> `enum`은 내장 도움말에 "Enum 코드 생성"으로 표시되지만, 실제로 만드는 것은
> **`EnumDB.bin`(Enum 정보 바이너리)** 입니다. Enum `.cs` 파일은 `generate` 단계에서 나옵니다.

**공통 인자**

| 인자 | 사용 가능한 곳 | 설명 |
|---|---|---|
| `-each <이름>` | `extract` · `generate` · `all` | 확장자를 뺀 특정 테이블만 처리 |
| `-f`, `--file <이름>` | 위와 동일 | `-each`의 별칭 |
| `-p`, `--path <폴더>` | `diff` 전용 | 변경 검사 대상 폴더 오버라이드 |

**인자 위치 규칙** (`Common/Command/CommandInvoker.cs`)

- 통합 CLI에서는 `<command>` 다음이면 위치가 자유롭습니다 (`TableAutomationTool.exe gen -f Item`).
- `enum`은 `-each`를 받아도 **무시하고 항상 전체 Enum을 다시 만듭니다.**

**init-config로 생성되는 설정**

`TableAutomationTool.exe init-config` 한 번이면 아래 6종을 모두 생성합니다.
**이미 있는 파일은 전부 건너뜁니다** — 언제 몇 번을 실행해도 기존 설정이 지워지지 않습니다.

| 생성 파일 | 기존 파일이 있으면 |
|---|---|
| `ExcelToDataConfig.json`, `JsonConfig.json`, `GAuthConfig.json` | 건너뜀 (안전) |
| `CodeGeneratorConfig.json` | 건너뜀 (안전) |
| `EnumGeneratorConfig.json` | 건너뜀 (안전) |
| `GitConfig.json` | 건너뜀 (안전) |

> 건너뛴 파일은 `[...Command] Config file already exists at: <경로>` 로 로그에 남습니다.
> 기본값으로 되돌리고 싶다면 해당 파일을 직접 지우고 `init-config`를 다시 실행하십시오.
> 설정이 아예 없는 상태라면 `init-config` 없이 그냥 한 번 실행해도 템플릿이 자동 생성됩니다.

### 복사해서 쓰는 실행 예시

**PowerShell**

```powershell
$Tool = "C:\PROJECT\TableAutomarionTool\Output\Release\net9.0"

& "$Tool\TableAutomationTool.exe" all             # 전체 갱신
& "$Tool\TableAutomationTool.exe" all -each Item  # Item 테이블만 전 과정
& "$Tool\TableAutomationTool.exe" extract         # 데이터만 추출
& "$Tool\TableAutomationTool.exe" gen -f Item     # Item 코드만 생성
& "$Tool\TableAutomationTool.exe" enum            # EnumDB.bin만 갱신
& "$Tool\TableAutomationTool.exe" diff            # 변경된 테이블 목록만 출력

if ($LASTEXITCODE -ne 0) { throw "pipeline failed" }
```

**bash**

```bash
TOOL=/c/PROJECT/TableAutomarionTool/Output/Release/net9.0

"$TOOL/TableAutomationTool.exe" all || exit 1
"$TOOL/TableAutomationTool.exe" all -each Item || exit 1
```

**dotnet으로 직접 실행** (exe 대신 dll — 크로스 플랫폼)

```bash
dotnet "$TOOL/TableAutomationTool.dll" all
```

### 종료 코드

| 코드 | 의미 |
|---|---|
| `0` | 성공 |
| `1` | 알 수 없는 명령, 또는 단계 실행 중 예외 발생 |

`all`은 각 단계가 `1`을 반환하면 즉시 중단합니다.
다만 **설정 오류처럼 예외를 던지지 않고 로그만 남기는 실패는 `0`으로 끝납니다**
(예: `Wrong Src Directory`, `IsUseLoadFromLocal is false.`).
성공 여부를 엄밀히 판정하려면 **종료 코드와 함께 출력 파일의 생성 시각을 확인**하십시오.

---

## 프로젝트 구조

백엔드는 **`TableAutomationTool` 프로젝트 하나**이고, GUI(`TableEditor`)만 별도입니다.
예전의 프로젝트 경계는 그대로 폴더 · 네임스페이스 경계로 남아 있습니다.

```
TableAutomarionTool/                # 저장소 루트 폴더 이름은 그대로 둡니다
├── TableAutomationTool/            # [exe] 통합 백엔드 (net9.0)
│   ├── Cli/Program.cs              #   CLI 진입점 (서브 커맨드 분기)
│   ├── ExcelToData/                #   Excel/GSheet → .bin / .json
│   │   ├── Commands/               #     ETDCommandInvoker, DataExtractCommands
│   │   ├── DataExtractConfig.cs
│   │   └── ProjectDefine.cs        #     검색 패턴: *.xlsm, *.xlsx, *.bin
│   ├── CodeGenerator/              #   .bin → C#/C++ 클래스, Enum 코드
│   │   ├── Commands/               #     CGCommandInvoker, CodeGenerateCommands
│   │   ├── CodeGenerateConfig.cs
│   │   └── ProjectDefine.cs        #     출력 언어 상수(CodeExtension)
│   ├── EnumGenerator/              #   Enum 시트 → Binary/EnumDB.bin
│   ├── Generator/                  #   생성/추출 코어
│   │   ├── CodeGenerator/          #     CSharpGenerator, CppGenerator, EnumCodeGenerator
│   │   └── DataExtractor/          #     StructuredBinaryExtractor, JsonExtractor,
│   │                               #     BytesExtractorFromGSheet
│   ├── ExcelParser/                #   ClosedXML 기반 Excel 파싱
│   │   ├── TableConverter.cs       #     Worksheet/string[][] → (테이블명, 타입, 데이터)
│   │   ├── TableDefine.cs          #     헤더 행 인덱스 등 규약 상수
│   │   └── UserEnum/               #     EnumDB, EnumCache, SharedEnumMemoryHandler
│   ├── BinaryParser/               #   구조화 바이너리 read/write (+ C++ 리더)
│   ├── GAuth/                      #   Google OAuth2 · Drive/Sheets API
│   ├── Vcs/                        #   git 변경 목록 조회 (diff 명령)
│   │   ├── IVcsHandler.cs / GitHandler.cs / GitConfig.cs
│   │   └── Commands/               #     DiffCommands, VcsCommandInvoker
│   ├── Common/                     #   Command 패턴, Logger, FileLoader
│   ├── Shared/                     #   SystemPath, SystemDefine, SharedCommands
│   └── Tester/                     #   EnumDB 공유 메모리 검증 (test 명령)
├── TableEditor/                    # [exe] WPF GUI (net9.0-windows)
│   ├── MainWindow.xaml             #   4탭 레이아웃 (편집/데이터 추출/코드 추출/설정)
│   ├── ViewModels/                 #   Main, Edit, Extract, CodeGen, Settings
│   ├── Services/                   #   ConfigService, PipelineService, UiLog
│   └── Core/Mvvm.cs                #   ViewModelBase, RelayCommand
│
├── Output/<Configuration>/net9.0/  # exe·Config가 모이는 실행 폴더
└── Documents/                      # 설계·기획 문서
```

솔루션 파일은 `TableAutomationTool.sln` 입니다. 예전 이름의 `Automarion` 오타는
**`Automation`으로 교정**되었습니다. 다만 **저장소 루트 폴더 이름(`TableAutomarionTool`)은
외부 설정이 참조하고 있어 그대로 둡니다.**

> `BinaryParser` 네임스페이스는 외부에서 `BinaryParser.BinaryReader` 처럼 정규화된 이름으로
> 참조되던 이력이 있어 통합 후에도 `BinaryParser`로 **동결**되어 있습니다.
>
> ⚠️ **Unity 전용 빌드는 폐기되었습니다.** `netstandard2.1` 타깃, `Configuration=Unity` 빌드
> 구성, `UNITY_BUILD` 조건부 컴파일은 모두 제거되었고 솔루션에는 `TableAutomationTool`과
> `TableEditor` 두 프로젝트만 남습니다. 따라서 **이 저장소에서는 Unity용
> `BinaryParser.dll`을 더 이상 만들 수 없습니다.** 이미 배포된 DLL은 그대로 동작하지만,
> 바이너리 포맷을 바꾸면 Unity 측 DLL을 갱신할 수단이 없습니다.

---

## 파이프라인 아키텍처

### 커맨드 파이프라인

각 단계는 `CommandInvoker`가 커맨드 목록을 구성해 순차 실행하는 구조입니다.

| 단계 | 파이프라인 |
|---|---|
| **enum** | `EnumCacheGenerate`(로컬) 또는 `EnumCacheGenerateFromGSheet` → `GenerateEnumBinary` → *[Dev]* `EnumDBTest` / `EnumDBLoad` |
| **extract** | `LoadEnumDB` → `ExtractBinary`(로컬) → `ExtractBinaryFromGSheet` → `ExtractJson` |
| **generate** | `LoadEnumDB` → `GenerateCSharpCode`(또는 `GenerateCppCode`) → *(옵션)* `GenerateEnumCode` |

- `extract`는 로컬·GSheet 커맨드를 **둘 다 등록**하고, 각 커맨드가 설정의
  `IsLoadFromLocal` / `IsLoadFromGSheet` 플래그를 보고 스스로 실행 여부를 결정합니다.
  둘 다 `true`면 두 소스를 모두 추출합니다.
- `SystemDefine.DevelopmentMode`(기본 `true`)일 때 `enum` 단계가 `EnumDB_BeforeSave.txt`,
  `EnumDB_AfterSave.txt` 덤프를 추가로 남깁니다.

### 공유 데이터 저장소

| 저장소 | 경로 | Write | Read |
|---|---|---|---|
| **EnumBinary** | `Binary/EnumDB.bin` | `enum` | `extract`, `generate` |
| **DataBinary** | `ExcelToDataConfig.DstDirectory` | `extract` | `generate`, JSON 추출 단계 |
| **Json** | `JsonConfig.DstDirectory` | `extract` | 외부(디버깅·확인용) |
| **Code** | `CodeGeneratorConfig`의 출력 디렉터리 | `generate` | 외부(게임 프로젝트) |

---

## 설치 및 빌드

### 요구 사항

- **.NET 9.0 SDK / Runtime**
- **TableEditor(GUI)는 Windows 전용** (`net9.0-windows`, WPF)
- CLI(`TableAutomationTool`)는 Windows/macOS/Linux에서 동작

### 빌드

```bash
git clone https://github.com/kyl990312/ExcelToData.git
cd ExcelToData

# 전체 솔루션 빌드 (TableAutomationTool + TableEditor)
dotnet build TableAutomationTool.sln -c Release

# 백엔드 CLI만
dotnet build TableAutomationTool/TableAutomationTool.csproj -c Release
```

### 실행 폴더

모든 프로젝트가 **같은 폴더로 출력**됩니다. Config와 `Binary/EnumDB.bin`을 공유하기 위한
의도적인 설정입니다.

```
Output/Release/net9.0/
├── TableAutomationTool.exe   # 통합 CLI
├── TableEditor.exe           # GUI
├── Config/                   # 설정 6종 (아래 참조)
└── Binary/EnumDB.bin         # enum 단계 산출물
```

설정 파일은 **exe와 같은 폴더의 `Config/`** 에서 읽습니다. 어느 위치에서 실행하든 exe 위치를
기준으로 탐색합니다.

---

## 설정 파일

경로 규약의 단일 소스는 `TableAutomationTool/Shared/SystemPath.cs` 입니다.

| 파일 | 사용 앱 | 대응 클래스 |
|---|---|---|
| `Config/ExcelToDataConfig.json` | ExcelToData | `DataExtractConfig` |
| `Config/JsonConfig.json` | ExcelToData(JSON 단계) | `DataExtractConfig` |
| `Config/CodeGeneratorConfig.json` | CodeGenerator | `CodeGenerateConfig` |
| `Config/EnumGeneratorConfig.json` | EnumGenerator | `EnumGeneratorConfig` |
| `Config/GAuthConfig.json` | GAuth (Google 연동) | `UserCredentialConfig` |
| `Config/GitConfig.json` | `diff` 명령 | `GitConfig` |

설정 파일이 없으면 **첫 실행 시 기본값 템플릿이 자동 생성**됩니다. 경로를 실제 값으로 고친 뒤
다시 실행하십시오. GUI의 **설정 탭**에서도 5종(`GitConfig.json` 제외)을 편집·저장할 수 있습니다.

### ExcelToDataConfig.json

```json
{
  "IsLoadFromLocal": true,
  "IsLoadFromGSheet": false,
  "SrcDirectory": "C:/MyProject/Data/Excel",
  "DstDirectory": "C:/MyProject/Data/Binary",
  "SrcGDriveLink": "https://drive.google.com/drive/folders/<폴더ID>"
}
```

| 필드 | 설명 |
|---|---|
| `IsLoadFromLocal` | 로컬 `.xlsm`/`.xlsx` 추출 사용 여부 |
| `SrcDirectory` | 원본 Excel 폴더 (하위 폴더는 탐색하지 않음) |
| `DstDirectory` | `.bin` 출력 폴더 |
| `IsLoadFromGSheet` | Google Sheets 추출 사용 여부 |
| `SrcGDriveLink` | 테이블 GSheet들이 들어 있는 **Drive 폴더 링크** |

### JsonConfig.json

```json
{
  "SrcDirectory": "C:/MyProject/Data/Binary",
  "DstDirectory": "C:/MyProject/Data/Json"
}
```

> ⚠️ JSON 추출은 **`.bin`을 읽어 `.json`을 씁니다.** 따라서 `SrcDirectory`에는 Excel 폴더가
> 아니라 **`ExcelToDataConfig.DstDirectory`와 같은 바이너리 폴더**를 지정해야 합니다.
> 이 값이 어긋나면 JSON이 하나도 생성되지 않습니다.

### CodeGeneratorConfig.json

```json
{
  "BinaryDirectory": "C:/MyProject/Data/Binary",
  "ClassOutputDirectory": "C:/MyProject/Generated/Classes",
  "ManagerOutputDirectory": "C:/MyProject/Generated/Managers",
  "ClassNamespace": "MyProject.Data",
  "ManagerNamespace": "MyProject.Managers",
  "ReadFileExtension": "*.bin",
  "IsGenerateEnumCode": true,
  "EnumBinaryPath": "Binary/EnumDB.bin",
  "EnumOutputDirectory": "C:/MyProject/Generated/Enums",
  "CodeExtension": ".cs"
}
```

| 필드 | 설명 |
|---|---|
| `BinaryDirectory` | 입력 `.bin` 폴더 |
| `ClassOutputDirectory` | 생성 클래스 출력 폴더 |
| `ClassNamespace` | 생성 클래스 네임스페이스 |
| `IsGenerateEnumCode` | Enum 코드(.cs) 동시 생성 여부 |
| `EnumOutputDirectory` / `CodeExtension` | Enum 코드 출력 폴더 / 확장자 |

> **출력 언어(C# / C++) 선택은 설정 파일이 아니라 코드 상수입니다.**
> `CodeGenerator/ProjectDefine.cs`의 `CodeExtension`(기본 `.cs`)이 `cs`로 끝나면 C#,
> `cpp`로 끝나면 C++ 생성기가 선택됩니다. 설정 파일의 `CodeExtension`은
> **Enum 코드 파일 확장자**에만 사용됩니다.

### EnumGeneratorConfig.json

```json
{
  "IsLoadFromLocal": true,
  "EnumFilePath": "C:/MyProject/Data/Excel/SystemEnum.xlsx",
  "IsLoadFromGSheet": false,
  "SrcGDriveLink": "https://drive.google.com/drive/folders/<폴더ID>",
  "EnumSheetPatterns": [ "*Enum*", "*enum*" ]
}
```

| 필드 | 설명 |
|---|---|
| `IsLoadFromLocal` / `EnumFilePath` | 로컬 Enum Excel 파일 경로 |
| `IsLoadFromGSheet` / `SrcGDriveLink` | GSheet에서 Enum 시트를 읽을 때 사용 |
| `EnumSheetPatterns` | GSheet에서 Enum 탭을 식별하는 와일드카드 패턴 |

GSheet 설정이 켜져 있으면 GSheet가 우선합니다. 산출물은 항상 `Binary/EnumDB.bin` 입니다.

> 이 설정에는 출력 경로 항목이 없습니다. 예전 파일에 남아 있는 `DstDirectory`,
> `CodeExtension` 키는 현재 클래스에 존재하지 않아 **무시**됩니다. Enum 코드 출력 경로는
> `CodeGeneratorConfig.json`의 `EnumOutputDirectory`로 지정하십시오.

### GAuthConfig.json

```json
{
  "ApplicationName": "TableAutomationTool",
  "User": "user@example.com",
  "ClientSceret": "C:/path/to/client_secret.json"
}
```

> ⚠️ 클라이언트 시크릿 키 이름은 **`ClientSceret`** 입니다(철자가 이렇게 굳어져 있습니다).
> `ClientSecret`으로 적으면 값이 바인딩되지 않습니다.

### GitConfig.json

`diff` 명령 전용 설정입니다. 파일이 없어도 기본값(`git`)으로 동작합니다.

```json
{
  "RepositoryPath": "",
  "GitExecutablePath": "git"
}
```

| 필드 | 기본값 | 설명 |
|---|---|---|
| `RepositoryPath` | `""` | `diff` 검사 대상 폴더. 비우면 `ExcelToDataConfig.SrcDirectory`로 폴백합니다. 저장소 루트가 아니라 하위 폴더를 지정해도 됩니다 |
| `GitExecutablePath` | `"git"` | git 실행 파일 경로. 기본값이면 PATH에서 찾습니다 |

자세한 동작은 [diff — 변경 테이블 목록](#diff--변경-테이블-목록)을 보십시오.

---

## 테이블 작성 규칙

로컬 Excel과 GSheet 모두 동일한 규약을 사용합니다.

```
1행 | Id    | Name     | Level | Skills         | #Memo    ← 컬럼명
2행 | Int32 | String   | Int32 | String[]       | String   ← 데이터 타입
3행 | 1     | Warrior  | 10    | [Slash,Shield] | 전사
    | //    | 이 행은 주석이라 제외됩니다
4행 | 2     | Mage     | 8     | [Fireball]     | 마법사
    | EOF                                                  ← 데이터 끝
```

### 규칙

- **1행 = 컬럼명**, **2행 = 데이터 타입**, **3행부터 실제 데이터**
- `#`으로 시작하는 **컬럼**은 추출에서 제외됩니다 (메모·주석용)
- `//`로 시작하는 **행**은 추출에서 제외됩니다
- `EOF`를 만나면 그 아래는 읽지 않습니다
- 대상 확장자는 `*.xlsm`, `*.xlsx` 입니다

### 지원 타입

| 분류 | 타입 |
|---|---|
| 정수 | `Int16`, `Int32`, `Int64` |
| 실수 | `Single`, `Double` |
| 문자열 | `String` |
| 배열 | `Int32[]`, `String[]` 등 — 표기는 `[값1,값2,값3]` (공백 허용) |
| 열거형 | EnumGenerator에 등록된 사용자 정의 Enum |

### Enum 시트

```
| enumType   | member    | value |
| String     | String    | Int32 |
| ItemType   | Weapon    | 1     |
| ItemType   | Armor     | 2     |
| ItemRarity | Common    | 0     |
| EOF        |           |       |
```

GSheet에서는 탭 이름이 `EnumSheetPatterns`(기본 `*Enum*`, `*enum*`)에 걸리는 탭을 Enum 시트로
인식합니다.

### GSheet 탭 이름 규약

탭 이름이 `<파일명>_<탭명>` 형태이면 prefix를 제거한 **탭명**이 테이블명(= `.bin` 파일명)이
됩니다. `-each` 옵션의 매칭도 이 정규화 이후 이름을 기준으로 동작합니다.

---

## TableAutomationTool — 통합 CLI

모든 단계를 하나의 실행 파일로 처리하는 진입점입니다.

```bash
cd Output/Release/net9.0
./TableAutomationTool.exe <command> [options]
```

### 명령어

| 명령어 | 별칭 | 설명 |
|---|---|---|
| `all` | `pipeline` | 전체 파이프라인 (enum → extract → generate) |
| `extract` | `etd` | Excel/GSheet → `.bin` / `.json` |
| `generate` | `gen`, `cg` | `.bin` → 클래스 코드 |
| `enum` | — | `EnumDB.bin` 생성 |
| `init-config` | — | 설정 템플릿 6종 생성 |
| `test` | — | EnumDB 공유 메모리 검증 |
| `diff` | — | git 기준 변경된 테이블 목록 출력 ([아래 절](#diff--변경-테이블-목록)) |
| `diff all` \| `extract` \| `generate` | — | 목록 출력 후 Y/N 확인 → 변경분만 해당 단계 실행 |
| `help` | `-h`, `--help`, `?` | 도움말 |

### 옵션

| 옵션 | 설명 |
|---|---|
| `-each <파일명>` | 특정 파일/테이블만 처리 (확장자 제외) |
| `-f`, `--file <파일명>` | `-each`와 동일 |
| `-p`, `--path <폴더>` | (`diff` 전용) 검사 대상 폴더 오버라이드 |

### 예시

```bash
# 전체 파이프라인
./TableAutomationTool.exe all

# Item 테이블만 전 과정 처리 (Item.xlsm → Item.bin → Item.cs)
./TableAutomationTool.exe all -each Item

# Excel만 변환
./TableAutomationTool.exe extract
./TableAutomationTool.exe extract -each Character

# 코드만 생성
./TableAutomationTool.exe generate
./TableAutomationTool.exe gen -f Item

# Enum 정보만 갱신
./TableAutomationTool.exe enum

# 설정 템플릿 생성 / EnumDB 공유 메모리 검증
./TableAutomationTool.exe init-config
./TableAutomationTool.exe test
```

### 종료 코드

성공 `0`, 실패 `1`. `all`은 각 단계의 실패를 감지해 즉시 중단하므로 배치 스크립트에서
`if errorlevel 1`로 분기할 수 있습니다.
다만 설정 오류처럼 예외를 던지지 않는 실패는 `0`으로 끝납니다 —
자세한 내용은 [종료 코드](#종료-코드) 절을 참고하십시오.

### 단계별 상세

각 단계는 예전의 개별 exe와 동일한 커맨드 파이프라인을 그대로 실행합니다.

**`enum`**

| 항목 | 내용 |
|---|---|
| 인자 | `-each`는 **무시됨** |
| 설정 | `Config/EnumGeneratorConfig.json` |
| 입력 | 로컬 Enum xlsx **또는** GSheet의 Enum 탭 (`EnumSheetPatterns`) |
| 출력 | `Binary/EnumDB.bin` |
| 비고 | `SystemDefine.DevelopmentMode`가 `true`면 `EnumDB_BeforeSave.txt`·`EnumDB_AfterSave.txt` 덤프를 함께 남깁니다 |

**`extract` (`etd`)**

| 항목 | 내용 |
|---|---|
| 인자 | `-each <이름>` (선택) |
| 설정 | `Config/ExcelToDataConfig.json`(바이너리) · `Config/JsonConfig.json`(JSON) · `Config/GAuthConfig.json`(GSheet 인증) |
| 입력 | `*.xlsm`, `*.xlsx` 또는 GSheet 폴더. **JSON 단계는 `.bin`을 입력으로 받습니다** |
| 출력 | `.bin` (`ExcelToDataConfig.DstDirectory`), `.json` (`JsonConfig.DstDirectory`) |
| 선행 조건 | Enum 타입 컬럼이 있으면 `Binary/EnumDB.bin`이 먼저 있어야 합니다 |

한 번 실행하면 **바이너리 추출 → GSheet 바이너리 추출 → JSON 추출**을 차례로 수행합니다.
각 단계는 설정의 `IsLoadFromLocal` / `IsLoadFromGSheet` 값으로 켜고 끕니다.

**`generate` (`gen`, `cg`)**

| 항목 | 내용 |
|---|---|
| 인자 | `-each <이름>` (선택) |
| 설정 | `Config/CodeGeneratorConfig.json` |
| 입력 | `BinaryDirectory`의 `.bin` + `Binary/EnumDB.bin` |
| 출력 | 클래스 코드(`ClassOutputDirectory`), 매니저 코드(`ManagerOutputDirectory`), Enum 코드(`EnumOutputDirectory`) |
| 비고 | 출력 언어는 설정이 아니라 `TableAutomationTool/CodeGenerator/ProjectDefine.cs`의 `CodeExtension` 상수로 결정됩니다 |

**`test`**

`Binary/EnumDB.bin`을 읽어 EnumDB 공유 메모리 동작을 검증하는 개발용 명령입니다.
파이프라인에는 관여하지 않습니다.

---

## diff — 변경 테이블 목록

git으로 관리되는 테이블 폴더에서 **아직 커밋되지 않은 변경(스테이징 + 워킹트리 + 신규 파일)** 을
찾아 **테이블 이름 목록**을 출력합니다.

인자 없이 `diff`만 쓰면 **목록만 출력하고 끝납니다(프롬프트 없음)**. 뒤에 다음 단계 이름
(`all` / `extract` / `generate`)을 붙이면 목록을 보여 준 뒤 **Y/N 확인**을 거쳐 변경된
테이블만 그 단계로 넘깁니다. 다음 단계 이름과 별칭은 기존 서브커맨드와 같습니다.

```bash
./TableAutomationTool.exe diff
./TableAutomationTool.exe diff --path "C:\Work\Tables"
./TableAutomationTool.exe diff all         # 목록 → Y/N → enum + 변경분 추출 + 변경분 코드 생성
./TableAutomationTool.exe diff extract     # 목록 → Y/N → 변경분 추출만
./TableAutomationTool.exe diff generate    # 목록 → Y/N → 변경분 코드 생성만
```

### 대상 폴더 결정 순서

1. `--path <폴더>` (`-p`)
2. `Config/GitConfig.json`의 `RepositoryPath`
3. `Config/ExcelToDataConfig.json`의 `SrcDirectory`

셋 다 비어 있으면 오류(종료 코드 `1`)입니다. 하위 폴더를 지정해도 됩니다 —
저장소 루트는 `git rev-parse --show-toplevel`로 자동 판별하고, 지정한 폴더 아래만 검사합니다.

### Config/GitConfig.json

```json
{
  "RepositoryPath": "",
  "GitExecutablePath": "git"
}
```

| 필드 | 설명 |
|---|---|
| `RepositoryPath` | 검사 대상 폴더. 비우면 `ExcelToDataConfig.SrcDirectory`로 폴백 |
| `GitExecutablePath` | git 실행 파일 경로. 기본값 `git` (PATH에서 탐색) |

`init-config`로 생성되며, **이미 있으면 덮어쓰지 않습니다.**

### Y/N 확인과 비대화형 환경

`diff all` / `diff extract` / `diff generate`는 목록을 출력한 뒤 진행 여부를 묻습니다.

| 입력 | 동작 |
|---|---|
| `Y` / `y` | 변경된 테이블만 다음 단계로 넘겨 실행 |
| `N` / `n` | 아무 작업도 하지 않고 종료 (**종료 코드 `0` — 취소는 실패가 아닙니다**) |
| 그 외 | 최대 3회까지 다시 묻고, 그 뒤에는 취소 |

- **변경이 0건이면 묻지 않습니다.** "변경된 테이블이 없습니다" 안내 후 종료 코드 `0`.
- **비대화형 환경(stdin 리다이렉트)에서는 대기하지 않고 즉시 취소합니다.** CI·스크립트·에디터
  자동화에서 프로세스가 멈추지 않도록 `Console.IsInputRedirected`로 미리 감지합니다.
  자동 실행이 필요하면 프롬프트가 없는 `diff`의 출력을 파이프로 받아 `extract -each <이름>`을
  직접 호출하십시오(아래 예시).
- 인자 없는 `diff`는 프롬프트 자체가 없으므로 어떤 환경에서도 안전합니다.

### 출력 규약 (파이프 가능)

- **stdout** — 변경된 테이블 이름만 한 줄에 하나씩 (확장자 없음, 접두사 없음)
- **stderr** — 대상 폴더, 건수, 경고, 오류 등 나머지 전부

```powershell
# 변경된 테이블만 골라 추출
.\TableAutomationTool.exe diff | ForEach-Object { .\TableAutomationTool.exe extract -each $_ }
```

**실제 출력 예시**

```
> .\TableAutomationTool.exe diff
GameTestTables                                                     ← stdout
[2026-09-12 오후 7:03:37] [FileLoader] Convert config from: ...\Config/GitConfig.json        ← stderr
[2026-09-12 오후 7:03:37] [FileLoader] Convert config from: ...\Config/ExcelToDataConfig.json ← stderr
[diff] 대상 폴더: C:\Work\Tables (출처: ExcelToDataConfig.SrcDirectory)                       ← stderr
[diff] 변경된 테이블 1건                                                                      ← stderr
```

```
> .\TableAutomationTool.exe diff generate
GameTestTables
[diff] 대상 폴더: C:\Work\Tables (출처: ExcelToDataConfig.SrcDirectory)
[diff] 변경된 테이블 1건
[diff] 변경된 테이블 1건을 처리하시겠습니까? (Y/N): y
[diff] CodeGenerator: CharacterTable
[diff] CodeGenerator: DialogueTable
...
[diff] 완료
```

### 필터링 규칙

- 확장자는 `ProjectDefine.SearchPatterns`(`*.xlsm`, `*.xlsx`)를 따릅니다.
- Excel 임시 잠금 파일(`~$*.xlsx`)은 제외합니다.
- 삭제된 파일은 제외합니다(이름 변경은 **새 테이블 추가**로 취급).
- 같은 이름은 중복 없이 한 번만 출력합니다.

### 종료 코드

| 상황 | 종료 코드 |
|---|---|
| 목록 출력 성공 (변경 0건 포함) | `0` |
| 사용자가 `N`으로 취소 / 비대화형이라 취소 | `0` |
| 대상 폴더 미설정 / 폴더 없음 | `1` |
| git 저장소가 아님 / git 실행 파일 없음 / git 명령 실패 | `1` |
| 알 수 없는 옵션/하위 명령, `--path` 뒤 값 누락, 인자 과다 | `1` |

> git 실패를 "변경 0건"으로 뭉개지 않습니다. 기본 설정의 `SrcDirectory`가 git 저장소가 아니면
> 반드시 오류로 끝나므로, `GitConfig.RepositoryPath`를 채우거나 `--path`를 쓰십시오.

### 다음 단계로 넘어갈 때의 이름 변환

`diff`가 출력하는 목록은 **엑셀 파일 이름**(확장자 제외)입니다. 단계별로 필요한 이름이 다르므로
`diff`가 알아서 변환해 넘깁니다.

| 단계 | 넘기는 이름 | 이유 |
|---|---|---|
| `extract` | 엑셀 파일 이름 그대로 | `ExtractBinaryCommand`가 `SrcDirectory/<이름>.xlsx(.xlsm)`을 찾기 때문 |
| `generate` | **변경된 엑셀의 워크시트 이름들** | `.bin` 파일 이름은 파일명이 아니라 **워크시트 이름**이기 때문 |

`GameTestTables.xlsx` 한 개에 시트가 8개 있으면 `extract`는 1회, `generate`는 8회
(`CharacterTable` … `StageTable`) 호출됩니다. 워크시트 목록은 변경된 엑셀 파일을 직접 열어
읽습니다. 파일을 열 수 없으면 경고를 남기고 파일 이름을 그대로 사용합니다.

### 한계

- GSheet 소스의 변경은 git으로 알 수 없습니다(경로 출처가 `SrcDirectory`이고 GSheet 전용
  설정이면 경고를 출력합니다).
- 목록은 **확장자를 뺀 파일 이름** 기준으로 중복을 제거합니다. 서로 다른 폴더에 같은 이름의
  엑셀이 있으면(`Tables/Item.xlsm`, `Tables/Sub/Item.xlsx`) 한 건으로 합쳐집니다.
- `extract`는 `ExcelToDataConfig.SrcDirectory` 아래에서 파일을 찾습니다. diff 대상 폴더와
  `SrcDirectory`가 다르면 추출 대상을 찾지 못합니다.

---

## 실행 결과 예시

### 실행 로그 예시

```
[ExcelToData] Start Extract Data
[ExtractBinaryCommand] Processing all files in directory
[ExtractBinaryCommand] Extract StructuredBinary (C:/MyProject/Data/Excel/PlayerData.xlsm)
[ExtractBinaryCommand] Complete Extract StructuredBinary (3 files)
[ExtractJsonCommand] Complete Extract Json (3 files)
[ExcelToData] Quit Program
```

### 생성 결과 예시

`Generated/Classes/PlayerData.cs`

```csharp
// 자동 생성된 코드 - 수정하지 마세요

using System;
using System.Collections.Generic;

namespace MyProject.Data
{
    public class PlayerData
    {
        public PlayerData() { }

        public PlayerData(int id, string name, int level, string[] skills)
        {
            Id = id;
            Name = name;
            Level = level;
            Skills = skills;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
        public int Level { get; private set; }
        public string[] Skills { get; private set; }
    }
}
```

Enum 코드

```csharp
public enum ItemType
{
    Weapon = 1,
    Armor = 2,
    Accessory = 3
}
```

---

## TableEditor — GUI

추출 결과 확인과 파이프라인 실행을 한 창에서 처리하는 WPF 앱입니다. CLI와 **같은 `Config/`
폴더를 공유**하므로 설정을 따로 관리할 필요가 없습니다.
**테이블 편집 기능은 제공하지 않습니다** — 원본 수정은 Excel/GSheet에서 하고, 결과는 뷰어로 확인합니다.

```bash
dotnet build TableEditor/TableEditor.csproj -c Release
Output/Release/net9.0/TableEditor.exe
```

### 탭 구성

| 탭 | 기능 |
|---|---|
| **뷰어** | 추출된 `.bin`을 읽는 **읽기 전용 뷰어**. 좌측에 테이블 목록·검색, 우측에 타입 요약과 DataGrid, 하단에 `행 N개 · 컬럼 M개`. `새로고침` 버튼 |
| **데이터 추출** | 소스(로컬 xlsx / GSheet 폴더) 선택 → `목록 새로고침` → 체크 후 `전체 추출` / `선택 추출` |
| **코드 추출** | `.bin` 목록에서 선택해 `전체 생성` / `선택 생성`. `Enum 코드 함께 생성` 체크 시 EnumGenerator를 먼저 실행 |
| **설정** | 5종 Config를 항목별로 편집. `기본값 생성` · `검증` · `저장` |

### 동작 특성

- **뷰어 탭은 `ExcelToDataConfig.DstDirectory`(추출 결과 폴더)의 `*.bin`을 목록으로 보여줍니다.**
  원본 xlsx/GSheet가 아니라 **추출 산출물**을 보는 화면이므로, 표시되지 않는 테이블이 있다면
  먼저 데이터 추출을 실행해야 합니다.
- 배열 값은 `|`로 이어 붙인 문자열로, `null`은 빈 문자열로 표시됩니다.
- `선택 추출` / `선택 생성`은 항목마다 CLI의 `-each`와 동일한 경로로 실행됩니다.
- 파이프라인은 백그라운드로 실행되고 **동시 실행은 1개로 제한**됩니다. 진행 상황은 하단
  로그 패널(`로그` 토글)에 CLI와 동일한 로그로 출력됩니다.
- GSheet 추출은 개별 항목을 선택해도 **Drive 폴더 전체를 조회한 뒤** 대상만 추출합니다.
- 코드 추출 탭의 "출력 언어"는 현재 설정을 표시만 합니다. 실제 언어 선택은
  `CodeGenerator/ProjectDefine.cs` 상수로 결정됩니다.
- 엔진 쪽에는 GSheet 쓰기 API(`GAuthService.UpdateGSheetTable`)가 남아 있지만
  **GUI에서는 호출하지 않습니다.**

---

## Google Sheets 연동

### 1. 인증 준비

1. Google Cloud Console에서 **OAuth 클라이언트 ID(데스크톱 앱)** 를 만들고
   `client_secret.json`을 내려받습니다.
2. `Config/GAuthConfig.json`에 경로를 지정합니다(`ClientSceret` 철자 주의).
3. 첫 실행 시 브라우저에서 동의 절차를 한 번 거치면 이후에는 저장된 토큰을 사용합니다.

사용 스코프는 **Drive 읽기 전용 + Spreadsheets 읽기·쓰기** 입니다. GUI에서 GSheet를 저장하려면
쓰기 스코프가 필요하므로, 기존 토큰이 읽기 전용이라면 재인증이 필요할 수 있습니다.

### 2. 데이터 테이블을 GSheet에서 읽기

`Config/ExcelToDataConfig.json`

```json
{
  "IsLoadFromLocal": false,
  "IsLoadFromGSheet": true,
  "SrcGDriveLink": "https://drive.google.com/drive/folders/<폴더ID>",
  "DstDirectory": "C:/MyProject/Data/Binary"
}
```

지정한 **폴더 안의 모든 GSheet의 모든 탭**을 순회하며 `.bin`을 생성합니다.
`-each <이름>`을 주면 탭 이름 정규화 후 대소문자를 무시하고 매칭해 해당 테이블만 처리합니다.

### 3. Enum을 GSheet에서 읽기

`Config/EnumGeneratorConfig.json`에서 `IsLoadFromGSheet: true`와 `SrcGDriveLink`를 지정하면
`EnumSheetPatterns`에 걸리는 탭들을 Enum 시트로 읽습니다.

---

## 통합 워크플로우

### 통합 CLI 사용 (권장)

```bash
cd Output/Release/net9.0
./TableAutomationTool.exe all              # 전체 갱신
./TableAutomationTool.exe all -each Item   # 부분 갱신
```

### 배치 스크립트

**Windows (run_all.bat)**

```batch
@echo off
setlocal
cd /d "%~dp0"

echo Running full pipeline...
TableAutomationTool.exe all
if errorlevel 1 goto error

echo Pipeline completed successfully!
goto end

:error
echo Error occurred during pipeline execution!
exit /b 1

:end
endlocal
```

**Linux/macOS (run_all.sh)**

```bash
#!/bin/bash
set -e
cd "$(dirname "$0")"

echo "Running full pipeline..."
dotnet TableAutomationTool.dll all

echo "Pipeline completed successfully!"
```

### 단계별로 나누어 실행할 경우

```batch
@echo off
TableAutomationTool.exe enum
if errorlevel 1 goto error
TableAutomationTool.exe extract
if errorlevel 1 goto error
TableAutomationTool.exe generate
if errorlevel 1 goto error
echo Done!
goto end
:error
echo Failed!
exit /b 1
:end
```

### 변경된 테이블만 반영할 경우

```powershell
cd Output\Release\net9.0

# 1) 변경 목록만 확인 (프롬프트 없음, 파이프 가능)
.\TableAutomationTool.exe diff

# 2) 대화형 콘솔이라면 — Y/N 확인 후 변경분만 처리 (이름 변환까지 자동)
.\TableAutomationTool.exe diff all         # enum + 변경분 추출 + 변경분 코드 생성
.\TableAutomationTool.exe diff extract     # 변경분 추출만
.\TableAutomationTool.exe diff generate    # 변경분 코드 생성만

# 3) 스크립트·CI 처럼 비대화형이라면 — 목록을 파이프로 받아 직접 호출
.\TableAutomationTool.exe diff | ForEach-Object {
    .\TableAutomationTool.exe extract -each $_
}
.\TableAutomationTool.exe generate         # 코드 생성은 전체로 돌리는 편이 안전
```

> `diff`가 출력하는 이름은 **엑셀 파일 이름**입니다. `generate -each`는 **워크시트 이름**
> (= `.bin` 파일 이름)을 받으므로, 위 3)처럼 파이프로 받은 이름을 그대로 `generate -each`에
> 넘기면 안 됩니다. 이름 변환이 필요하면 `diff generate` / `diff all`을 쓰십시오 —
> 변경된 엑셀을 열어 워크시트 이름으로 바꿔서 넘겨 줍니다.

---

## AI 에이전트 스킬 작성 가이드

이 도구는 **CLI 진입점 하나(`TableAutomationTool.exe`)로 전 기능을 덮을 수 있으므로**
Claude Code 스킬이나 Codex 스킬로 감싸기 쉽습니다. 아래 내용을 그대로 스킬 본문에 옮기면 됩니다.

### 스킬이 지켜야 할 3가지 전제

1. **작업 디렉터리는 항상 exe 폴더** (`Output/<Configuration>/net9.0/`).
   설정과 `Binary/EnumDB.bin`을 exe 위치 기준으로 찾습니다.
2. **`TableAutomationTool.exe`만 호출한다.** 실행 파일은 이것과 GUI 둘뿐입니다.
3. **종료 코드만 믿지 않는다.** 설정 오류는 `0`으로 끝나므로 **출력 로그와 산출물 파일을 함께 확인**합니다.

### 의도 → 명령 매핑

| 사용자가 원하는 것 | 실행할 명령 |
|---|---|
| 테이블 전부 갱신 | `TableAutomationTool.exe all` |
| 테이블 하나만 갱신 | `TableAutomationTool.exe all -each <테이블명>` |
| Excel/GSheet 변경분만 데이터로 반영 | `TableAutomationTool.exe extract` |
| 코드만 다시 생성 | `TableAutomationTool.exe generate` |
| Enum을 추가/수정했을 때 | `TableAutomationTool.exe enum` 후 `TableAutomationTool.exe all` |
| 어떤 테이블이 바뀌었는지만 알고 싶을 때 | `TableAutomationTool.exe diff` (목록만 출력) |
| 변경된 테이블만 반영 (대화형 콘솔) | `TableAutomationTool.exe diff all` (Y/N 확인 후 실행) |
| 설정을 처음 만들 때 | 설정 없이 한 번 실행 → `Config/`의 템플릿을 채움 |

> `-each`의 인자는 **확장자를 뺀 파일명**입니다. GSheet는 `<파일명>_<탭명>`에서
> prefix를 제거한 **탭명**을 씁니다.
>
> `extract -each`는 **엑셀 파일 이름**을, `generate -each`는 **워크시트 이름(= `.bin` 이름)** 을
> 받습니다. 둘이 다를 수 있으므로 `diff`의 출력을 `generate -each`에 그대로 넘기지 마십시오
> (`diff generate`는 이 변환을 자동으로 합니다).

### 성공 판정 체크리스트

스킬이 실행 후 확인해야 할 항목입니다.

- 종료 코드가 `0`인가
- 로그에 `Wrong Src Directory`, `Wrong Dst Directory`, `IsUseLoadFromLocal is false.`,
  `파싱 실패` 같은 문구가 없는가
- `Complete Extract StructuredBinary (N files)`의 **N이 0이 아닌가**
- 산출물 파일(`.bin` / `.cs`)의 수정 시각이 방금으로 갱신됐는가

### 스킬 예시 (`.claude/skills/table-pipeline/SKILL.md`)

````markdown
---
name: table-pipeline
description: 게임 데이터 테이블(Excel/GSheet)을 바이너리와 C# 코드로 변환한다. 테이블 추가·수정 후 반영, 특정 테이블만 갱신, Enum 갱신 시 사용.
---

# 테이블 파이프라인 실행

## 실행 위치

모든 명령은 아래 폴더에서 실행한다.

```
C:\PROJECT\TableAutomarionTool\Output\Release\net9.0
```

## 명령

- 전체 갱신: `.\TableAutomationTool.exe all`
- 특정 테이블만: `.\TableAutomationTool.exe all -each <테이블명>` (확장자 제외)
- 데이터만: `.\TableAutomationTool.exe extract`
- 코드만: `.\TableAutomationTool.exe generate`
- Enum 정보만: `.\TableAutomationTool.exe enum`
- 변경된 테이블 목록: `.\TableAutomationTool.exe diff` (목록만 출력한다)

**실행 파일은 `TableAutomationTool.exe`(CLI)와 `TableEditor.exe`(GUI) 둘뿐이다.**

## 실행 후 검증

1. 종료 코드가 0인지 확인한다.
2. 로그에 `Wrong Src Directory` / `IsUseLoadFromLocal is false.`가 있으면
   `Config/`의 경로와 소스 플래그가 잘못된 것이다. 사용자에게 알린다.
3. `Complete Extract StructuredBinary (0 files)`이면 대상 파일을 찾지 못한 것이다.
   `-each` 이름 철자와 `SrcDirectory`를 확인한다.
4. 산출물 폴더의 파일 수정 시각이 갱신됐는지 확인한다.

## 주의

- Enum을 추가·수정했다면 `enum`을 먼저 실행해야 테이블의 Enum 컬럼이 올바르게 변환된다.
- `init-config`는 **이미 있는 설정 파일을 건드리지 않는다**(전부 건너뜀). 기본값으로 되돌리려면
  해당 파일을 지우고 다시 실행한다.
````

Codex 스킬도 형식만 다를 뿐 내용은 동일합니다. 위 본문을 그대로 옮겨 쓰십시오.

### 스킬에서 다루지 않는 것

다음은 사람이 직접 해야 하는 작업이라 스킬로 감싸지 않는 편이 안전합니다.

- `Config/*.json` 경로 수정 — 환경마다 다르고 잘못 쓰면 조용히 0건 처리됩니다.
- Google OAuth 최초 인증 — 브라우저 동의 절차가 필요합니다.
- `init-config` 실행 — 기존 파일은 건너뛰므로 안전하지만, 새로 만들어진 템플릿의 경로는 사람이 채워야 합니다.
- 출력 언어(C#/C++) 변경 — 설정이 아니라 소스 상수라 빌드가 필요합니다.

---

## 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| 실행했더니 아무 것도 안 하고 끝난다 | 설정 파일이 없어 템플릿만 생성된 첫 실행입니다. `Config/`의 경로를 채우고 다시 실행하십시오. |
| `Wrong Src Directory` 로그 | 설정의 `SrcDirectory`가 존재하지 않습니다. 절대 경로를 권장합니다. |
| `.bin`은 생기는데 `.json`이 안 생긴다 | `JsonConfig.json`의 `SrcDirectory`가 **바이너리 폴더**를 가리켜야 합니다. Excel 폴더로 되어 있으면 대상 파일이 0개가 됩니다. |
| `IsUseLoadFromLocal is false.` 로그 | 해당 소스가 설정에서 꺼져 있습니다. `IsLoadFromLocal` / `IsLoadFromGSheet`를 확인하십시오. |
| Enum 타입 컬럼이 변환되지 않는다 | `EnumDB.bin`이 없거나 오래됐습니다. `TableAutomationTool.exe enum`을 먼저 실행하십시오. |
| Enum `.cs`가 생성되지 않는다 | `CodeGeneratorConfig.json`의 `IsGenerateEnumCode`가 `true`인지, `EnumOutputDirectory`가 지정됐는지 확인하십시오. EnumGenerator는 코드 파일을 만들지 않습니다. |
| C++ 코드가 나오지 않는다 | 출력 언어는 `TableAutomationTool/CodeGenerator/ProjectDefine.cs`의 `CodeExtension` 상수로 결정됩니다. 설정 파일로는 바뀌지 않습니다. |
| Google 인증이 계속 실패한다 | `GAuthConfig.json`의 키 이름이 `ClientSceret`인지, 시크릿 파일 경로가 맞는지 확인하십시오. |
| `-each Item`인데 아무 것도 처리되지 않는다 | 확장자를 뺀 **파일명**(GSheet는 정규화된 탭명)과 정확히 일치해야 합니다. |
| Debug 빌드에서만 코드 생성 설정이 무시된다 | `Output/Debug/net9.0/Config/`에 `CodeGeneratotConfig.json`(오타) 파일이 남아 있으면 CodeGenerator가 설정을 찾지 못합니다. `CodeGeneratorConfig.json`으로 정리하십시오. |
| Enum 덤프 `.txt` 파일이 생긴다 | `SystemDefine.DevelopmentMode`가 `true`일 때의 정상 동작입니다. |
| `ExcelToData.exe` 등을 실행할 수 없다 | 개별 exe는 제거되었습니다. `TableAutomationTool.exe <서브커맨드>`로 바꾸십시오. |
| `init-config`를 실행했더니 설정이 초기화됐다 | 현재 버전에서는 초기화되지 않습니다. 6종 모두 **기존 파일이 있으면 건너뜁니다.** (예전 버전은 `CodeGeneratorConfig.json`·`EnumGeneratorConfig.json`을 덮어썼습니다) |
| `EnumGenerator`에 `-each`가 안 먹는다 | 의도된 동작입니다. Enum은 항상 전체를 다시 생성합니다. |
| `diff`가 "git 저장소가 아닙니다"로 끝난다 | 대상 폴더가 git 워크트리가 아닙니다. `Config/GitConfig.json`의 `RepositoryPath`를 채우거나 `--path`로 실제 저장소 폴더를 지정하십시오. |
| `diff` 결과를 파이프로 받으면 로그가 섞인다 | 섞이지 않습니다. 목록은 stdout, 나머지는 stderr로 나갑니다. 로그까지 받고 있다면 `2>&1`을 지우십시오. |
| `diff`는 나오는데 `generate -each`가 건너뛴다 | `.bin` 파일 이름은 **워크시트 이름**입니다. 엑셀 파일명과 시트명이 다르면 `generate -each <엑셀명>`은 매칭되지 않습니다. `diff generate` / `diff all`을 쓰면 워크시트 이름으로 자동 변환해 넘깁니다. |
| `Output/Debug/net9.0`에 `TableTool.exe`·`Tester.exe`가 남아 있다 | 통합 전 빌드 산출물입니다. 솔루션에서 프로젝트가 빠져 `dotnet clean`으로도 지워지지 않습니다. 폴더를 비우고 다시 빌드하십시오(`Config/`·`Binary/`는 백업). 배포에는 `Output/Release/net9.0`을 쓰십시오. |

---

## 관련 문서

| 문서 | 내용 |
|---|---|
| [INDIVIDUAL_FILE_PROCESSING.md](INDIVIDUAL_FILE_PROCESSING.md) | `-each` 개별 파일 처리 상세 |
| `Documents/TableAutomationTool-시스템구조.md` | 시스템 구조 문서 |
| `Documents/TableEditor-UIUX-기획서.html` | TableEditor UI/UX 기획서 |
| `.harness/knowledges/pre-code/**/Index.md` | 프로젝트별 코드 맵 |
