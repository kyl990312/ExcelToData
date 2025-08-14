# BinaryParser 라이브러리

Dictionary 형태의 데이터를 고성능 구조화된 바이너리 형식으로 저장/로드하는 라이브러리입니다.

## 주요 특징

- **고성능**: 칼럼별 저장 방식으로 빠른 읽기/쓰기
- **효율적**: 타입 정보 보존으로 메모리 효율적 처리
- **호환성**: JSON 변환 지원
- **확장성**: 다양한 데이터 타입 지원

## 바이너리 파일 구조

```
Header:
├── 칼럼 갯수 (int32)
├── 데이터 갯수 (int32)  
└── 각 칼럼 메타데이터
    ├── 칼럼 이름 (string)
    └── 칼럼 타입 (string)

Data (칼럼별 저장):
├── 칼럼1의 모든 데이터
├── 칼럼2의 모든 데이터
└── ...
```

## 기본 사용법

```csharp
using BinaryParser;

// 데이터 생성
var data = new List<Dictionary<string, object>>
{
    new() { ["ID"] = 1, ["Name"] = "User1", ["Score"] = 95.5 },
    new() { ["ID"] = 2, ["Name"] = "User2", ["Score"] = 87.3 }
};

// 저장
BinaryHelper.SaveToFile(data, "data.sbin");

// 로드
var loadedData = BinaryHelper.LoadFromFile("data.sbin");

// JSON 변환
BinaryHelper.ConvertToJson("data.sbin", "data.json");
```

## 지원 데이터 타입

- 기본 타입: `int`, `long`, `float`, `double`, `string`, `bool`
- 배열 타입: `int[]`, `string[]` 등
- Enum 타입
- Null 값 지원

## 예제

`Examples/UsageExample.cs` 파일에서 다양한 사용법을 확인할 수 있습니다.
