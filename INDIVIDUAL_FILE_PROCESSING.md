# 개별 파일 처리 기능 가이드

## 개요

ExcelToData와 CodeGenerator에 개별 파일별로 동작할 수 있는 기능이 추가되었습니다.
커맨드 라인 인자 `-each` 옵션을 사용하여 특정 파일만 선택적으로 처리할 수 있습니다.

## 사용법

### 기본 문법

```bash
# ExcelToData - 특정 Excel 파일만 처리
ExcelToData.exe -each <파일명>

# CodeGenerator - 특정 Binary 파일만 처리
CodeGenerator.exe -each <파일명>
```

**중요**: 파일명에 확장자를 제외하고 입력합니다.

### 예시

```bash
# Item.xlsm 파일만 바이너리로 변환
ExcelToData.exe -each Item

# Item.bin 파일만 C# 클래스로 변환
CodeGenerator.exe -each Item
```

## 실전 시나리오

### 시나리오 1: 특정 테이블만 수정하고 재생성

```bash
# 1. Item 테이블만 수정했을 때

# 2. Item.xlsm만 Binary로 변환
cd Output/Release/net9.0
./ExcelToData.exe -each Item

# 3. Item.bin만 C# 클래스로 생성
./CodeGenerator.exe -each Item
```

### 시나리오 2: 전체 파이프라인 실행

```bash
# 인자 없이 실행하면 모든 파일 처리
./ExcelToData.exe
./CodeGenerator.exe
```

### 시나리오 3: 배치 스크립트로 여러 파일 처리

**Windows (process_tables.bat)**
```batch
@echo off
echo Processing specific tables...

echo Converting Character table...
ExcelToData.exe -each Character
if errorlevel 1 goto error

echo Converting Item table...
ExcelToData.exe -each Item
if errorlevel 1 goto error

echo Generating Character class...
CodeGenerator.exe -each Character
if errorlevel 1 goto error

echo Generating Item class...
CodeGenerator.exe -each Item
if errorlevel 1 goto error

echo Completed!
goto end

:error
echo Error occurred!

:end
pause
```

**Linux/macOS (process_tables.sh)**
```bash
#!/bin/bash
echo "Processing specific tables..."

echo "Converting Character table..."
./ExcelToData.exe -each Character || exit 1

echo "Converting Item table..."
./ExcelToData.exe -each Item || exit 1

echo "Generating Character class..."
./CodeGenerator.exe -each Character || exit 1

echo "Generating Item class..."
./CodeGenerator.exe -each Item || exit 1

echo "Completed!"
```

## 동작 원리

### ExcelToData

1. `-each Item` 인자를 받으면:
   - `Item.xlsm` 또는 `Item.xlsx` 파일을 찾습니다
   - 해당 파일만 Binary로 변환합니다
   - 출력: `Item.bin`

2. 인자 없이 실행하면:
   - Config의 `SrcDirectory`에 있는 모든 Excel 파일 처리

### CodeGenerator

1. `-each Item` 인자를 받으면:
   - `Item.bin` 파일을 찾습니다
   - 해당 파일만 C# 클래스로 생성합니다
   - 출력: `Item.cs`

2. 인자 없이 실행하면:
   - Config의 `BinaryDirectory`에 있는 모든 Binary 파일 처리

## 장점

### ⚡ 성능 향상
- 수정된 파일만 처리하여 빌드 시간 대폭 단축
- 대규모 프로젝트에서 개발 효율성 극대화

### 🎯 정확성
- 특정 테이블만 테스트하고 싶을 때 유용
- 다른 파일에 영향 없이 안전하게 작업 가능

### 🔧 디버깅 용이
- 문제가 있는 특정 파일만 재처리 가능
- 로그 출력이 간결하여 문제 파악 쉬움

### 💻 자동화 친화적
- 배치 스크립트나 CI/CD 파이프라인에 쉽게 통합
- 파일별로 순차 처리 또는 병렬 처리 가능

## 로그 출력 예시

### 개별 파일 처리
```
[ExcelToData] Start Extract Data
[ExcelToData] Target file specified: Item
[ExtractBinaryCommand] Processing specific file: Item
[ExtractBinaryCommand] Extract StructuredBinary (C:\Data\Table\Item.xlsm)
[ExtractBinaryCommand] Complete Extract StructuredBinary (1 files)
[ExcelToData] Quit Program
```

### 전체 파일 처리
```
[ExcelToData] Start Extract Data
[ExtractBinaryCommand] Processing all files in directory
[ExtractBinaryCommand] Extract StructuredBinary (C:\Data\Table\Item.xlsm)
[ExtractBinaryCommand] Extract StructuredBinary (C:\Data\Table\Character.xlsm)
[ExtractBinaryCommand] Extract StructuredBinary (C:\Data\Table\Skill.xlsm)
[ExtractBinaryCommand] Complete Extract StructuredBinary (3 files)
[ExcelToData] Quit Program
```

## 문제 해결

### 파일을 찾을 수 없다는 오류

**증상**: "Target file not found" 경고 메시지

**원인**:
1. 파일명이 잘못되었거나
2. Config의 디렉토리 설정이 잘못됨

**해결**:
```bash
# 파일 존재 확인
dir "C:\Data\Table\Item.xlsm"

# Config 파일 확인
notepad Config/StructuredBinaryConfig.json
# SrcDirectory 경로가 올바른지 확인
```

### 확장자를 포함하여 입력한 경우

**잘못된 예시**:
```bash
ExcelToData.exe -each Item.xlsm  # ❌ 잘못됨
```

**올바른 예시**:
```bash
ExcelToData.exe -each Item        # ✅ 올바름
```

### 공백이 포함된 파일명

```bash
# 따옴표로 감싸기
ExcelToData.exe -each "Item Master"
CodeGenerator.exe -each "Item Master"
```

## 기술 세부사항

### 구현 파일
- `ExcelToData/Program.cs:8-12` - 커맨드 라인 인자 파싱
- `CodeGenerator/Program.cs:8-12` - 커맨드 라인 인자 파싱
- `Common/Command/CommandInvoker.cs:6` - targetFileName 필드
- `ExcelToData/Commands/DataExtractCommands.cs:59-75` - 개별 파일 처리 로직
- `CodeGenerator/Commands/CodeGenerateCommands.cs:54-70` - 개별 파일 처리 로직

### 파일 검색 로직

ExcelToData는 다음 확장자를 순서대로 검색합니다:
1. `{파일명}.xlsm`
2. `{파일명}.xlsx`

CodeGenerator는 다음 파일을 찾습니다:
1. `{파일명}.bin`

## 사용 팁

### 💡 Tip 1: 개발 워크플로우 최적화
```bash
# 개발 중에는 -each 사용
ExcelToData.exe -each Item
CodeGenerator.exe -each Item

# 최종 빌드 전에는 전체 실행
ExcelToData.exe
CodeGenerator.exe
```

### 💡 Tip 2: PowerShell 함수로 간소화
```powershell
# PowerShell 프로파일에 추가
function Process-Table {
    param([string]$TableName)

    Write-Host "Processing $TableName..."
    & ExcelToData.exe -each $TableName
    & CodeGenerator.exe -each $TableName
    Write-Host "Done!"
}

# 사용
Process-Table "Item"
```

### 💡 Tip 3: Git Hook과 연동
```bash
# .git/hooks/pre-commit
#!/bin/bash

# 변경된 Excel 파일만 처리
git diff --cached --name-only | grep "\.xlsm$" | while read file; do
    filename=$(basename "$file" .xlsm)
    ./ExcelToData.exe -each "$filename"
    ./CodeGenerator.exe -each "$filename"
done
```

## 추가 정보

- 관련 파일:
  - `ExcelToData/Program.cs` - 커맨드 라인 파싱
  - `CodeGenerator/Program.cs` - 커맨드 라인 파싱
  - `Common/Command/CommandInvoker.cs` - 베이스 인보커
  - `ExcelToData/Commands/ETDCommandInvoker.cs` - ExcelToData 인보커
  - `CodeGenerator/Commands/CGCommandInvoker.cs` - CodeGenerator 인보커
