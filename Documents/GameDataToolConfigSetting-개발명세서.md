# GameDataToolConfigSetting Window 개발 명세서

**작성일**: 2026-02-22  
**대상**: Unity Editor 전용 (Editor 폴더 내 배치)  
**관련 문서**: GameDataTool-개발명세서.md

---

## 1. 개요

GameDataToolConfigSetting Window는 TableAutomationTool의 각 실행 파일(EnumGenerator, ExcelToData, CodeGenerator)이 사용하는 **Config 파일을 Unity Editor GUI에서 생성 및 관리**하기 위한 에디터 윈도우이다.

각 프로그램의 SrcPath, DstPath 등 경로 정보를 입력받아 Config 파일을 생성하고, GameDataTool Path 정보를 Unity Asset으로 저장하여 GameDataTool 윈도우에서 참조할 수 있도록 한다.

---

## 2. 역할 및 목적

| 항목 | 내용 |
|------|------|
| **Config 로드** | 윈도우 오픈 시 지정된 경로에서 GameDataToolConfig(ScriptableObject) 로드 |
| **Config 신규 생성** | GameDataToolConfig가 없을 경우 신규 생성 후 윈도우 Refresh |
| **Config 생성** | 각 Tool의 Config JSON 파일을 GUI에서 생성 |
| **경로 관리** | SrcPath, DstPath 등 Tool별 경로를 입력·관리 |
| **Asset 저장** | GameDataTool Path 정보를 Unity Asset(ScriptableObject)에 저장 |
| **일관성 보장** | GameDataTool 윈도우가 사용하는 경로 정보의 단일 진입점 역할 |

---

## 3. 윈도우 오픈 동작

### 3.1 GameDataToolConfig 로드 흐름

윈도우 오픈(OnEnable) 시 다음 순서로 GameDataToolConfig를 로드한다.

```
[1] 지정된 Asset 경로에서 GameDataToolConfig(ScriptableObject) 로드 시도

[2-A] 로드 성공
        → 로드된 Config 데이터를 InputField에 반영
        → 윈도우 정상 표시

[2-B] 로드 실패 (Config 없음)
        → GameDataToolConfig ScriptableObject 신규 생성
        → 지정된 Asset 경로에 저장
        → 윈도우 Refresh (Repaint)
        → 빈 InputField 상태로 윈도우 표시
```

### 3.2 GameDataToolConfig Asset 경로

```
Assets/
└── Editor/
    └── GameDataTool/
        └── GameDataToolConfig.asset    ← 로드/생성 대상
```

---

## 4. UI 구조

### 4.1 전체 레이아웃

```
┌──────────────────────────────────────────────────────────┐
│  GameDataToolSetting Window                              │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  GameDataToolConfig  (ScriptableObject 인스펙터)    │  │
│  │    ToolRootPath  [ InputField                     ] │  │
│  │    ...                                             │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  GameDataToolPath Field                                  │
│  ┌────────────────────────────────────────────────────┐  │
│  │  EnumGenerate Config                               │  │
│  │    DstPath  [ InputField                         ] │  │
│  │    SrcPath  [ InputField                         ] │  │
│  │    ...                                             │  │
│  ├────────────────────────────────────────────────────┤  │
│  │  ExcelToData Config                                │  │
│  │    DstPath  [ InputField                         ] │  │
│  │    SrcPath  [ InputField                         ] │  │
│  │    ...                                             │  │
│  ├────────────────────────────────────────────────────┤  │
│  │  CodeGenerator Config                              │  │
│  │    DstPath  [ InputField                         ] │  │
│  │    SrcPath  [ InputField                         ] │  │
│  │    ...                                             │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  [ Generate ]                                            │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### 4.2 GameDataToolConfig 섹션 (최상단)

윈도우 **가장 상단**에 위치하며, GameDataToolConfig ScriptableObject의 필드를 직접 풀어서 표시한다. Unity의 `SerializedObject` / `EditorGUILayout` 방식으로 각 필드를 개별 렌더링한다.

| UI 요소 | 타입 | 설명 |
|---------|------|------|
| 섹션 헤더 | Label | "GameDataToolConfig" 표시 |
| 각 필드 | TextField / ObjectField 등 | ScriptableObject의 직렬화 필드를 항목별로 표시 |

GameDataToolConfig의 필드를 `ScriptableObject` Inspector처럼 통째로 내장하는 것이 아니라, **각 필드를 개별 GUI 요소로 풀어서 표시**한다.

### 4.3 GameDataToolPath Field

각 Tool의 Config 설정 섹션을 묶는 컨테이너 영역이다. Tool별로 독립된 Config 설정 블록을 포함한다.

### 4.4 Tool Config 섹션 (공통 구조)

각 Tool(EnumGenerator, ExcelToData, CodeGenerator)마다 동일한 구조의 Config 섹션이 존재한다.

| UI 요소 | 타입 | 설명 |
|---------|------|------|
| 섹션 헤더 | Label | Tool 이름 표시 (예: "EnumGenerate Config") |
| DstPath InputField | TextField | 출력 경로 입력 |
| SrcPath InputField | TextField | 입력 경로 입력 |
| 추가 항목 (...) | TextField | Tool별 추가 Config 항목 |

### 4.5 Generate 버튼

윈도우 하단에 위치하며, 입력된 모든 설정을 기반으로 Config 파일 생성 및 Asset 저장을 실행한다.

---

## 5. Tool별 Config 항목

### 5.1 EnumGenerate Config

`TableAutomationTool.exe enum` 단계가 사용하는 Config 항목에 대응한다.

| 항목 | UI 레이블 | 설명 |
|------|-----------|------|
| SrcPath | SrcPath InputField | Enum 정의 Excel 파일 경로 |
| DstPath | DstPath InputField | 생성된 Enum C# 파일 출력 경로 |
| (추가 항목) | ... | EnumGeneratorConfig의 추가 필드 |

### 5.2 ExcelToData Config

`TableAutomationTool.exe extract` 단계가 사용하는 Config 항목에 대응한다.

| 항목 | UI 레이블 | 설명 |
|------|-----------|------|
| SrcPath | SrcPath InputField | Excel 파일(Table) 소스 경로 |
| DstPath | DstPath InputField | 변환된 Binary 파일 출력 경로 |
| (추가 항목) | ... | DataExtractConfig의 추가 필드 |

### 5.3 CodeGenerator Config

`TableAutomationTool.exe generate` 단계가 사용하는 Config 항목에 대응한다.

| 항목 | UI 레이블 | 설명 |
|------|-----------|------|
| SrcPath | SrcPath InputField | Binary 파일 소스 경로 |
| DstPath | DstPath InputField | 생성된 C# 클래스 파일 출력 경로 |
| (추가 항목) | ... | CodeGenerateConfig의 추가 필드 |

---

## 6. Generate 버튼 동작

`Generate` 버튼 클릭 시 다음 순서로 처리한다.

```
[1] 입력값 유효성 검사
      - 필수 경로 항목(SrcPath, DstPath)이 비어있지 않은지 확인
      - 경로 형식 유효성 확인

[2] 각 Tool의 Config 파일 생성/갱신
      ┌─ Config 파일이 이미 존재하는 경우
      │     → InputField 값을 기반으로 Config JSON 파일 갱신 (덮어쓰기)
      │
      └─ Config 파일이 존재하지 않는 경우
            → 각 Tool의 init-config 명령어를 실행하여 기본 Config 파일 생성
            → 생성된 Config 파일에 InputField 값을 반영하여 갱신
      
      대상: 통합 CLI 1개 (개별 exe 3종은 제거되었다)
        - TableAutomationTool.exe init-config

[3] GameDataTool Path 정보를 Unity Asset에 저장
      - GameDataToolConfig ScriptableObject 갱신
      - 각 Tool의 실행 파일 경로, Config 경로를 Asset에 반영
      - AssetDatabase.SaveAssets() 호출

[4] 완료 메시지 출력
      - Unity Console에 생성 결과 로그 출력
      - 성공/실패 여부 표시
```

### 6.1 init-config 명령어 실행

Config 파일이 존재하지 않는 Tool에 대해서만 init-config를 실행한다.

| Tool | 실행 명령 | 생성되는 Config |
|------|-----------|----------------|
| TableAutomationTool (통합 CLI) | `TableAutomationTool.exe init-config` | `ExcelToDataConfig.json`, `JsonConfig.json`, `GAuthConfig.json`, `CodeGeneratorConfig.json`, `EnumGeneratorConfig.json`, `GitConfig.json` |

> 인자는 `--init-config` 가 아니라 **`init-config`** 이다(더블대시 없음).
> 한 번의 호출로 6종을 모두 만들고, **이미 있는 파일은 건너뛴다.**
> 개별 exe(`EnumGenerator.exe` · `ExcelToData.exe` · `CodeGenerator.exe`)는 제거되었으므로
> 더 이상 호출할 수 없다.

init-config 실행 후 생성된 기본 Config에 InputField에 입력된 값을 덮어써서 최종 Config를 확정한다.

---

## 7. GameDataToolConfig ScriptableObject

윈도우 최상단에 표시되며, Generate 실행 결과로 갱신되는 Unity Asset이다. GameDataTool 윈도우 및 기타 Editor 스크립트에서 이 Asset을 참조하여 경로 정보를 얻는다.

### Asset 저장 경로

```
Assets/
└── Editor/
    └── GameDataTool/
        └── GameDataToolConfig.asset    ← ScriptableObject Asset
```

### Asset 구조 (예상)

```csharp
[CreateAssetMenu]
public class GameDataToolConfig : ScriptableObject
{
    // 각 Tool 실행 파일 경로
    public string EnumGeneratorExePath;
    public string ExcelToDataExePath;
    public string CodeGeneratorExePath;

    // 각 Tool Config 파일 경로
    public string EnumGeneratorConfigPath;
    public string ExcelToDataConfigPath;
    public string CodeGeneratorConfigPath;

    // 공통 경로
    public string TableFolderPath;   // Excel 원본 경로
    public string BinaryFolderPath;  // Binary 출력 경로
    public string CodeOutputPath;    // C# 코드 출력 경로
}
```

---

## 8. Config 파일 생성 대상

Config JSON 파일은 **통합 CLI 실행 파일과 같은 폴더의 `Config/`** 한 곳에 모인다.
(경로 규약의 단일 소스는 `TableAutomationTool/Shared/SystemPath.cs` 이다)

| 단계 | Config 파일명 | 기본 저장 위치 |
|------|--------------|---------------|
| enum | `EnumGeneratorConfig.json` | {TableAutomationTool.exe 경로}/Config/ |
| extract (bin) | `ExcelToDataConfig.json` | {TableAutomationTool.exe 경로}/Config/ |
| extract (json) | `JsonConfig.json` | {TableAutomationTool.exe 경로}/Config/ |
| generate | `CodeGeneratorConfig.json` | {TableAutomationTool.exe 경로}/Config/ |
| GSheet 인증 | `GAuthConfig.json` | {TableAutomationTool.exe 경로}/Config/ |
| diff | `GitConfig.json` | {TableAutomationTool.exe 경로}/Config/ |

---

## 9. 연관 윈도우

| 윈도우 | 관계 |
|--------|------|
| GameDataTool Window | ConfigSetting에서 저장한 GameDataToolConfig Asset을 참조하여 동작 |

GameDataToolConfigSetting Window는 **사전 설정 단계**에 해당하며, 설정이 완료된 이후 GameDataTool Window를 정상적으로 사용할 수 있다.

---

## 10. Unity Editor 스크립트 구조 (예상)

```
Assets/
└── Editor/
    └── GameDataTool/
        ├── GameDataToolSettingWindow.cs    # EditorWindow 메인 클래스 (ConfigSetting)
        ├── GameDataToolConfig.cs           # ScriptableObject 정의
        └── Config/
            ├── EnumGenerateConfigData.cs   # EnumGenerate Config UI 데이터 모델
            ├── ExcelToDataConfigData.cs    # ExcelToData Config UI 데이터 모델
            └── CodeGeneratorConfigData.cs  # CodeGenerator Config UI 데이터 모델
```

---

## 11. 예외 처리

| 상황 | 처리 |
|------|------|
| 윈도우 오픈 시 GameDataToolConfig 없음 | 신규 생성 후 윈도우 Refresh |
| 필수 경로 미입력 상태에서 Generate 실행 | 에러 다이얼로그 표시, Generate 중단 |
| 존재하지 않는 경로 입력 | 경고 다이얼로그 표시 (사용자 확인 후 진행 또는 중단) |
| init-config 명령 실행 실패 | Unity Console에 에러 로그 출력, 해당 Tool Config 생성 건너뜀 |
| Config 파일 생성 실패 (권한 등) | Unity Console에 에러 로그 출력 |
| Asset 저장 실패 | Unity Console에 에러 로그 출력 |
