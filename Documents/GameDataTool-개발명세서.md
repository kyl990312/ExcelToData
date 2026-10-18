# GameDataTool (Unity Editor) 개발 명세서

**작성일**: 2026-02-22  
**대상**: Unity Editor 전용 (Editor 폴더 내 배치)  
**연동 대상**: GameDataTool.exe (TableAutomationTool 빌드 산출물)

---

## 1. 개요

GameDataTool은 Unity Editor 내에서 TableAutomationTool의 핵심 기능(Binary 추출, 코드 생성, Enum 생성)을 GUI로 제어하는 에디터 윈도우이다. 외부 프로세스인 GameDataTool.exe를 통해 TableAutomationTool 파이프라인을 호출하며, Unity Editor의 윈도우 생명주기에 맞춰 자동 실행/종료된다.

---

## 2. 윈도우 생명주기

| 이벤트 | 동작 |
|--------|------|
| Unity GameDataTool 윈도우 Open | GameDataTool.exe 자동 실행 |
| Unity GameDataTool 윈도우 Close | GameDataTool.exe 자동 종료 |

GameDataTool.exe 프로세스는 Unity Editor 윈도우의 열림/닫힘과 1:1로 연동된다. 윈도우가 열리면 외부 프로세스를 시작하고, 윈도우가 닫히면 해당 프로세스를 Kill하여 리소스를 정리한다.

---

## 3. UI 구조

### 3.1 탭 구성

윈도우 상단에 2개의 탭이 위치한다. 윈도우 최초 오픈 시 **Extract Binary** 탭이 자동 선택된다.

```
┌──────────────────┬──────────────────┐
│  Extract Binary  │  Code Generator  │
└──────────────────┴──────────────────┘
```

---

### 3.2 Extract Binary 탭

Extract Binary 탭은 Excel 파일을 Binary로 변환하고, Enum을 생성하는 기능을 제공한다.

```
┌─────────────────────────────────────────────────────────────┐
│  [Extract Binary]  [Code Generator]                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [모두 선택/해제]                                              │
│                                                             │
│  ┌─────────────────────────────────┐  ┌──────────────────┐  │
│  │ Table List (From Table Folder)  │  │ Extract Binary   │  │
│  │                                 │  │     All          │  │
│  │  ☑ Character                    │  ├──────────────────┤  │
│  │  ☑ Weapon                       │  │ Extract Binary   │  │
│  │  ☐ Skill                        │  │    Selected      │  │
│  │                                 │  ├──────────────────┤  │
│  │                                 │  │ Enum Generate    │  │
│  └─────────────────────────────────┘  └──────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### 3.2.1 Table List 영역

- **데이터 소스**: Config에 지정된 Table Folder 경로에서 Excel 파일 목록을 자동 조회 (GetTableList)
- **표시 방식**: 파일명을 체크박스 리스트로 표시 (확장자 제외)
- **모두 선택/해제**: 토글 버튼으로 전체 체크박스를 일괄 선택 또는 해제

#### 3.2.2 버튼 영역

| 버튼 | 기능 | 설명 |
|------|------|------|
| **Extract Binary All** | 전체 추출 | Table Folder의 모든 Excel 파일을 Binary로 변환 |
| **Extract Binary Selected** | 선택 추출 | 체크박스에서 선택된 파일만 Binary로 변환 |
| **Enum Generate** | Enum 생성 | Enum 정의 Excel로부터 C# Enum 코드 및 EnumBinary 생성 |

---

### 3.3 Code Generator 탭

Code Generator 탭은 Binary 파일로부터 C# 클래스를 생성하는 기능을 제공한다.

```
┌─────────────────────────────────────────────────────────────┐
│  [Extract Binary]  [Code Generator]                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [모두 선택/해제]                                              │
│                                                             │
│  ┌─────────────────────────────────┐  ┌──────────────────┐  │
│  │ Table List (From Binary Folder) │  │ Code Generate    │  │
│  │                                 │  │     All          │  │
│  │  ☑ Character                    │  ├──────────────────┤  │
│  │  ☑ Weapon                       │  │ Code Generate    │  │
│  │  ☐ Skill                        │  │    Selected      │  │
│  │                                 │  │                  │  │
│  │                                 │  │                  │  │
│  └─────────────────────────────────┘  └──────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

#### 3.3.1 Table List 영역

- **데이터 소스**: Config에 지정된 Binary Folder 경로에서 Binary 파일 목록을 자동 조회 (GetBinaryList)
- **표시 방식**: 파일명을 체크박스 리스트로 표시 (확장자 제외)
- **모두 선택/해제**: 토글 버튼으로 전체 체크박스를 일괄 선택 또는 해제
- **탭 전환 시 자동 갱신**: Code Generator 탭 선택 시 GetBinaryList를 호출하여 리스트를 자동으로 갱신한다

#### 3.3.2 버튼 영역

| 버튼 | 기능 | 설명 |
|------|------|------|
| **Code Generate All** | 전체 생성 | Binary Folder의 모든 Binary 파일로부터 C# 클래스 생성 |
| **Code Generate Selected** | 선택 생성 | 체크박스에서 선택된 파일만 C# 클래스 생성 |

---

## 4. 기능 명세

### 4.1 GetTableList

| 항목 | 내용 |
|------|------|
| 호출 시점 | 윈도우 오픈 시, Extract Binary 탭 선택 시 |
| 데이터 소스 | Config의 Table Folder 경로 |
| 검색 대상 | Excel 파일 (*.xlsm, *.xlsx) |
| 반환 형식 | 파일명 리스트 (확장자 제외) |
| UI 반영 | 체크박스 리스트로 표시 |

### 4.2 GetBinaryList

| 항목 | 내용 |
|------|------|
| 호출 시점 | Code Generator 탭 선택 시 |
| 데이터 소스 | Config의 Binary Folder 경로 |
| 검색 대상 | Binary 파일 (*.bin) |
| 반환 형식 | 파일명 리스트 (확장자 제외) |
| UI 반영 | 체크박스 리스트로 표시 |

### 4.3 Extract Binary All

| 항목 | 내용 |
|------|------|
| 실행 방식 | GameDataTool.exe에 전체 추출 명령 전달 |
| 대상 | Table Folder 내 모든 Excel 파일 |
| 출력 | Binary Folder에 .bin 파일 생성 |
| 완료 후 | Unity AssetDatabase.Refresh() 호출 |

### 4.4 Extract Binary Selected

| 항목 | 내용 |
|------|------|
| 실행 방식 | GameDataTool.exe에 선택 추출 명령 전달 |
| 대상 | 체크박스에서 선택된 Excel 파일만 |
| 출력 | Binary Folder에 선택된 항목의 .bin 파일 생성 |
| 완료 후 | Unity AssetDatabase.Refresh() 호출 |

### 4.5 Enum Generate

| 항목 | 내용 |
|------|------|
| 실행 방식 | GameDataTool.exe에 Enum 생성 명령 전달 |
| 대상 | Config에 지정된 Enum Excel 파일 |
| 출력 | EnumBinary + C# Enum 코드 (.cs) |
| 완료 후 | Unity AssetDatabase.Refresh() 호출 |

### 4.6 Code Generate All

| 항목 | 내용 |
|------|------|
| 실행 방식 | GameDataTool.exe에 전체 코드 생성 명령 전달 |
| 대상 | Binary Folder 내 모든 .bin 파일 |
| 출력 | C# 클래스 파일 (.cs) |
| 완료 후 | Unity AssetDatabase.Refresh() 호출 |

### 4.7 Code Generate Selected

| 항목 | 내용 |
|------|------|
| 실행 방식 | GameDataTool.exe에 선택 코드 생성 명령 전달 |
| 대상 | 체크박스에서 선택된 Binary 파일만 |
| 출력 | 선택된 항목의 C# 클래스 파일 (.cs) |
| 완료 후 | Unity AssetDatabase.Refresh() 호출 |

---

## 5. GameDataTool.exe 연동

### 5.1 프로세스 관리

```
Unity Editor Window
    │
    ├── OnEnable()  → Process.Start("GameDataTool.exe")
    ├── 명령 실행    → GameDataTool.exe에 커맨드 전달
    └── OnDisable() → Process.Kill() / Process.Close()
```

- Unity Editor 윈도우의 OnEnable에서 GameDataTool.exe를 시작한다
- 각 버튼 클릭 시 GameDataTool.exe에 해당 명령을 전달한다
- Unity Editor 윈도우의 OnDisable에서 GameDataTool.exe 프로세스를 종료한다

### 5.2 명령 전달 방식

GameDataTool.exe와의 통신 방식은 다음 중 하나를 선택하여 구현한다.

| 방식 | 설명 | 장점 | 단점 |
|------|------|------|------|
| CLI 인자 | 실행 시 커맨드라인 인자로 명령 전달 | 구현 간단 | 매 명령마다 프로세스 재시작 필요 |
| 표준입출력 (stdin/stdout) | 프로세스 유지 상태에서 파이프 통신 | 프로세스 재사용 가능 | 프로토콜 정의 필요 |
| 파일 기반 | 명령 파일 작성 → exe가 감시 → 결과 파일 작성 | 단순 | 지연 발생 |

### 5.3 실행 결과 처리

- 명령 실행 중 Unity Editor에 Progress Bar를 표시한다
- 실행 완료 시 성공/실패 결과를 Unity Console에 로그 출력한다
- 실행 완료 후 AssetDatabase.Refresh()를 호출하여 생성된 파일을 Unity에 반영한다

---

## 6. 설정 의존성

GameDataTool Editor 윈도우가 참조하는 설정값은 다음과 같다.

| 설정 항목 | 용도 | 사용 위치 |
|-----------|------|-----------|
| Table Folder 경로 | Excel 파일 목록 조회 (GetTableList) | Extract Binary 탭 |
| Binary Folder 경로 | Binary 파일 목록 조회 (GetBinaryList) | Code Generator 탭 |
| GameDataTool.exe 경로 | 외부 프로세스 실행 경로 | 프로세스 관리 |
| Enum Excel 파일 경로 | Enum 생성 대상 지정 | Enum Generate 버튼 |

---

## 7. Unity Editor 스크립트 구조 (예상)

```
Assets/
└── Editor/
    └── GameDataTool/
        ├── GameDataToolWindow.cs       # EditorWindow 메인 클래스
        ├── GameDataToolConfig.cs       # 설정 관리 (경로, exe 위치 등)
        ├── GameDataToolProcess.cs      # GameDataTool.exe 프로세스 관리
        ├── Tabs/
        │   ├── ExtractBinaryTab.cs     # Extract Binary 탭 UI 및 로직
        │   └── CodeGeneratorTab.cs     # Code Generator 탭 UI 및 로직
        └── Utils/
            ├── FileListHelper.cs       # GetTableList, GetBinaryList 구현
            └── ProcessHelper.cs        # 외부 프로세스 실행/종료 유틸
```

---

## 8. 핵심 동작 흐름

### 8.1 윈도우 오픈 → Extract Binary 실행 흐름

```
[1] 사용자: Unity 메뉴에서 GameDataTool 윈도우 오픈
[2] OnEnable(): GameDataTool.exe 프로세스 시작
[3] 자동: Extract Binary 탭 활성화 + GetTableList() 호출
[4] UI: Table Folder의 Excel 파일 목록을 체크박스로 표시
[5] 사용자: 파일 선택 후 "Extract Binary Selected" 버튼 클릭
[6] 실행: GameDataTool.exe에 선택된 파일 목록과 함께 추출 명령 전달
[7] 대기: Progress Bar 표시
[8] 완료: 결과 로그 출력 + AssetDatabase.Refresh()
```

### 8.2 Code Generator 탭 전환 → 코드 생성 흐름

```
[1] 사용자: Code Generator 탭 클릭
[2] 자동: GetBinaryList() 호출 → Binary Folder 파일 목록 갱신
[3] UI: Binary 파일 목록을 체크박스로 표시
[4] 사용자: 파일 선택 후 "Code Generate Selected" 버튼 클릭
[5] 실행: GameDataTool.exe에 선택된 파일 목록과 함께 코드 생성 명령 전달
[6] 대기: Progress Bar 표시
[7] 완료: 결과 로그 출력 + AssetDatabase.Refresh()
```

---

## 9. 예외 처리

| 상황 | 처리 |
|------|------|
| GameDataTool.exe 경로 미설정 또는 파일 없음 | 에러 다이얼로그 표시, 설정 안내 |
| Table Folder / Binary Folder 경로 없음 | 빈 리스트 표시 + 경고 메시지 |
| 선택 없이 Selected 버튼 클릭 | 선택된 항목 없음 안내 메시지 |
| GameDataTool.exe 실행 중 오류 | 에러 로그를 Unity Console에 출력 |
| 프로세스 비정상 종료 | 프로세스 상태 체크 후 재시작 또는 에러 표시 |
| 윈도우 닫기 시 프로세스 종료 실패 | Process.Kill() 강제 종료 시도 + 로그 |
