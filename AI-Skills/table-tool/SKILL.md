---
name: table-tool
description: 게임 데이터 테이블(xlsx·GSheet·.bin)의 목록·스키마·행을 조회·검색하고, GSheet 행을 수정·추가·삭제하고, GSheet 에 컬럼·탭(시트)·스프레드시트 파일을 새로 추가하고, 추출·코드생성을 실행한다. 테이블 데이터를 확인·검증·수정하거나, 테이블 구조를 바꾸거나 새 테이블을 만들거나, 추출/코드생성을 돌려야 할 때 사용.
disable-model-invocation: false
---

# /table-tool

TableAutomationTool CLI 의 `ai` 서브커맨드. stdout 에 **JSON 한 줄**만 내고, 로그는 stderr 로 보낸다.

## 실행
- exe: `<TableAutomationTool 폴더>/Output/Release/net9.0/TableAutomationTool.exe` (프로젝트마다 아래 `EXE` 를 맞춘다)
- **Bash 툴**로 호출하고 `2>/dev/null` 로 로그를 버린다. 어디서 호출해도 된다(내부에서 exe 폴더로 이동).
- exe 가 없거나 오래됐으면 TableAutomationTool 폴더에서 `dotnet build TableAutomationTool.sln -c Release`.
  TableEditor 가 켜져 있으면 dll 이 잠겨 실패하므로 사용자에게 닫아 달라고 한다.

```bash
EXE=/c/PROJECT/TableAutomarionTool/Output/Release/net9.0/TableAutomationTool.exe
"$EXE" ai help 2>/dev/null          # 툴·옵션·타입 규칙·오류 코드 전체 목록
```

## 툴
### 조회
| 툴 | 용도 |
|---|---|
| `ai tables [--source local\|gsheet\|bin] [--filter t]` | 테이블 목록. 원본이면 `extracted`(.bin 유무). gsheet 는 `origin` = `파일/탭` |
| `ai schema <table> [--source ...]` | 컬럼 이름·타입·배열·enum 멤버, 행 수 |
| `ai rows <table> [--where "Col=V"]... [--search t] [--columns A,B] [--offset N] [--limit N]` | 행 조회. `= != ~ > < >= <=`, 여러 `--where` 는 AND, `--limit` 기본 50(0=전부) |
| `ai diff [--path dir]` | git 기준 변경된 워크북·테이블(로컬 xlsx 만) |

### 실행
| 툴 | 용도 |
|---|---|
| `ai run <extract\|generate\|enum\|all> [--table name]...` | 엔진 실행. 단계별 `ok`·`changedFiles`·`errors` |

### GSheet 행 쓰기
| 툴 | 용도 |
|---|---|
| `ai update <table> (--row N\|--where "Col=V")... --set "Col=V"... [--all] [--dry-run]` | 셀 값 변경. 같은 값인 셀은 건너뜀(`unchanged`) |
| `ai insert <table> --set "Col=V"... [--copy N] [--at N] [--dry-run]` | 행 추가. `--at` = 새 행의 `_row`(기본 끝), `--copy N` = 행 N 복사 후 `--set` 덮어쓰기 |
| `ai delete <table> (--row N\|--where "Col=V")... [--all] [--dry-run]` | 행 삭제. 지운 값은 `changes` 에 남음 |

### GSheet 구조 쓰기
| 툴 | 용도 |
|---|---|
| `ai rename-column <table> --column "Old" --name New [--dry-run]` | 컬럼 이름(이름 헤더 셀) 변경. 타입·데이터 그대로. `--column` 은 앞뒤 공백까지 정확히 먼저 찾음 |
| `ai add-column <table> --name Col --type T [--after Col] [--value V] [--dry-run]` | 컬럼 추가. 시트 열을 끼워 이름·타입 헤더를 쓰고 모든 데이터 행을 `--value` 로 채움. 기본 위치는 마지막 컬럼 오른쪽 |
| `ai add-sheet <spreadsheet> --sheet Tab --column "Name:T"... [--dry-run]` | 기존 스프레드시트 파일에 테이블 탭 추가(1행 이름, 2행 타입만) |
| `ai add-file <spreadsheet> --sheet Tab --column "Name:T"... [--dry-run]` | GSheet 폴더에 새 스프레드시트 파일을 만들고 첫 탭을 테이블 탭으로 만듦 |

- `--source` 생략 시 설정(ExcelToDataConfig)을 따른다.
- 원본(local/gsheet) 행 값은 추출기가 .bin 에 쓰는 규칙대로 타입 변환된 값이다. 각 행의 `_row` 는 0부터 센 데이터 행 번호.

## GSheet 쓰기 공통
- 대상은 항상 GSheet 다(`--source` 없음). 로컬 xlsx·.bin·코드는 바뀌지 않는다. 반영은 `ai run extract`·`generate` 가 따로 한다.
- **먼저 `--dry-run`** 으로 결과(`changes`, 새 테이블이면 `table`·`origin`)를 확인하고, **같은 인자로** 적용한다.
- 결과: `applied`(썼는지), `verified`(다시 읽은 값이 기대와 같은지).
  `verify_failed` 는 **이미 쓴 뒤** 다시 읽은 값이 다르다는 뜻이다(셀 표시 형식·동시 편집). `mismatches` 를 사용자에게 그대로 알린다.

## 행 쓰기 (update·insert·delete)
- 대상 행: `--where "Id=1001"` 처럼 **키로 지정**한다. `--row`(`_row`)는 이전 조회 뒤 행이 밀렸을 수 있으므로 `--where` 와 함께 쓰면 가드가 된다(교집합).
  `--where` 만으로 여러 행이 맞으면 `--all` 이 있어야 한다(`multiple_matches`).
- 값: 숫자는 `.` 소수점, enum 은 멤버 이름(대소문자 무시) 또는 정수, 배열은 `"[a,b]"`. 빈 값은 String·배열만 된다.
  `--set` 값은 첫 `=` 뒤 전체다(문자열은 앞뒤 공백도 그대로 쓴다).
  `insert` 는 `--copy` 없이 쓰면 문자열·배열이 아닌 컬럼을 모두 `--set` 해야 한다(`missing_value`).
- 되돌릴 때는 before 값으로 `update`, 지운 행은 `values` 로 `insert` 한다.

## 구조 쓰기 (rename-column·add-column·add-sheet·add-file)
- **타입**: `Int16 Int32 Int64 Single Double Boolean String DateTime`(별칭 `short int long float bool` 은 바꿔 씀),
  배열은 `Int32[]` 처럼 `[]`(Boolean·DateTime 배열 불가), 그 밖은 **EnumDB 에 있는 enum 이름**.
  새 enum 이 필요하면 enum 소스에 먼저 추가하고 `ai run enum` 을 돌려야 한다(사용자 요청 시).
- **이름**: 컬럼·테이블 이름은 식별자(영문·숫자·`_`, 숫자로 시작 불가). 생성 코드의 프로퍼티·클래스 이름이 된다.
  기존 컬럼과 대소문자만 달라도 중복으로 거절한다.
- **열 제한**: 리더는 A:Z(26열)만 읽는다. 넘기면 `column_limit`.
- `add-column`
  - 데이터 행이 있으면 String·배열이 아닌 타입은 `--value` 필수(`missing_value`). 모든 행에 같은 값이 들어간다.
    행마다 다른 값은 추가한 뒤 `ai update` 로 바꾼다.
  - `--after` 컬럼 바로 오른쪽에 끼운다. 그 뒤의 열('#' 메모 열 포함)은 한 칸씩 밀린다. A열 앞에는 끼울 수 없다.
- `add-sheet`·`add-file`
  - `<spreadsheet>` 는 GSheet 폴더의 **파일 이름**이다(`ai tables --source gsheet` 의 `origin` 앞부분).
  - **테이블 이름 = `<파일>_<탭>` 에서 파일 이름의 첫 `_` 앞 토막을 뗀 것.** 파일 이름에 `_` 가 없으면 탭 이름 그대로다
    (예: 파일 `Item_Tables` + 탭 `Weapon` → `Tables_Weapon`). dry-run 의 `data.table` 로 확인한다.
  - 같은 테이블 이름·같은 파일 이름·같은 탭 이름이 이미 있으면 `already_exists`.
  - 새 탭에는 헤더 2행만 생긴다. 보통 첫 컬럼이 키(`Id:Int32`)다. 행은 이후 `ai insert` 로 넣는다.
  - `add-file` 은 드라이브 쓰기 권한이 필요하다. 탭 쓰기가 실패하면 만든 파일은 휴지통으로 옮긴다.
- 구조를 바꾼 뒤에는 `ai run extract`·`generate`(또는 `all`)를 돌려야 .bin·코드에 반영된다. **사용자가 요청할 때만** 실행한다.

### 예: 새 테이블 만들기
```bash
"$EXE" ai add-file ItemTables --sheet Item --column "Id:Int32" --column "Name:String" --column "Type:EItemType" --dry-run 2>/dev/null
# data.table(= Item) 확인 후 --dry-run 을 빼고 같은 인자로 적용
"$EXE" ai insert Item --set "Id=1" --set "Name=Sword" --set "Type=Weapon" --dry-run 2>/dev/null
```

## 결과 읽기
- `ok:false` 면 `error.code`·`error.hint` 를 보고 대응한다. 종료 코드도 1.
- `warnings` 는 데이터 검사 결과다. 사용자에게 그대로 전한다.
  - 타입 변환 실패: 빈 숫자 셀 등. 추출하면 .bin 이 깨진다.
  - enum 값 오류: 클라이언트 `Enum.Parse(ignoreCase)` 가 실패할 값.

## 규칙
- 조회(`tables`·`schema`·`rows`·`diff`)는 자유롭게 쓴다.
- `run` 은 Client 파일(.bin·json·Info 코드·DataManager)을 쓴다. **사용자가 요청했을 때만** 실행하고, 끝나면 `changedFiles` 를 보고한다.
- 쓰기 툴(행·구조)은 공유 GSheet 원본을 바꾼다. **사용자가 수정을 요청했을 때만** 쓰고, 먼저 `--dry-run` 으로 확인한 뒤 같은 인자로 적용한다.
  끝나면 바꾼 내용(`changes` 또는 `table`·`url`)과 `verified` 를 보고한다.
- `add-file` 은 공유 폴더에 파일을 만든다. 파일 이름·탭 이름·컬럼을 사용자에게 확인받은 뒤 적용한다.
- 구조 변경은 되돌리는 툴이 없다(컬럼·탭·파일 삭제 없음). 잘못 만들었으면 사용자에게 시트에서 직접 지워 달라고 한다.

## 오류 대응
- `gsheet_permission_denied`: 로그인 계정에 시트(새 파일이면 폴더) 편집 권한이 없다. 사용자에게 알린다.
- `gsheet_scope_insufficient`: 토큰이 드라이브 쓰기 권한 추가 전에 받은 것이다. 사용자에게 TableEditor 설정 탭의 [Google 인증]을 다시 요청한다(새 동의 화면이 뜬다).
- `ambiguous_table`: 같은 테이블 이름의 탭이 여러 스프레드시트에 있어 쓰지 않았다. 사용자에게 중복 정리를 요청한다.
- `ambiguous_spreadsheet`: 같은 이름의 스프레드시트 파일이 여러 개다. 사용자에게 정리를 요청한다.
- `spreadsheet_not_found`: hint 의 파일 목록에서 이름을 다시 고르거나, 새 파일이면 `add-file` 을 쓴다.
- `already_exists`: 다른 이름을 쓰거나, 기존 테이블에 컬럼을 더하는 것이면 `add-column` 을 쓴다.
- `column_limit`: A:Z 를 넘는다. 사용자에게 '#' 메모 열·빈 열 정리를 요청한다.
- `enum_unavailable`: enum 정의를 EnumDB 에서 찾지 못해 쓰지 않았다. EnumDB 재생성(`ai run enum`)이 필요한지 사용자에게 알린다.
- `gsheet_api_error` 의 힌트가 요청 한도면(분당 읽기 횟수) 1분쯤 기다렸다가 다시 시도한다. 쓰기 툴은 한 번에 여러 번 읽으므로 연달아 부르지 않는다.
- `gsheet_auth_required`·`gsheet_auth_expired`: 브라우저 로그인이 필요한 상태다. 직접 해결하지 말고 사용자에게 TableEditor 설정 탭의 [Google 인증]을 요청한다.
- `timeout`: 무작정 `--timeout` 을 늘리지 말고 원인(인증 대기 등)을 사용자에게 알린다.
