# BinaryParser 라이브러리

Dictionary 형태의 데이터를 고성능 구조화된 바이너리 형식으로 저장/로드하는 라이브러리입니다.

## 주요 특징

- **고성능**: 칼럼별 저장 방식으로 빠른 읽기/쓰기
- **효율적**: 타입 정보 보존으로 메모리 효율적 처리
- **크로스 플랫폼**: C#과 C++ 모두 지원
- **확장성**: 다양한 데이터 타입 및 배열 지원

## 바이너리 파일 구조

```
Header:
├── 칼럼 갯수 (int32)
├── 데이터 갯수 (int32)  
└── 각 칼럼 메타데이터
    ├── 칼럼 이름 (string, 7bit 인코딩 길이 + UTF-8)
    └── 칼럼 타입 (string, 7bit 인코딩 길이 + UTF-8)

Data (칼럼별 저장):
├── 칼럼1의 모든 데이터
│   ├── Row1: null 플래그(bool) + 값
│   ├── Row2: null 플래그(bool) + 값
│   └── ...
├── 칼럼2의 모든 데이터
└── ...
```

## 파일 구성

| 파일 | 설명 |
|------|------|
| `BinaryWriter.cs` | Dictionary 데이터를 바이너리로 저장 |
| `BinaryReader.cs` | 바이너리를 Dictionary로 로드 |
| `BinarySchema.cs` | 바이너리 파일의 스키마(메타데이터)만 추출 |
| `Cpp/BinaryReader.h` | C++ 헤더 파일 |
| `Cpp/BinaryReader.cpp` | C++ 구현 파일 |

## C# 사용법

### 바이너리 파일 쓰기

```csharp
using BinaryParser;

// 칼럼 타입 정의
var columnTypes = new Dictionary<string, string>
{
    ["ID"] = "Int32",
    ["Name"] = "String",
    ["Score"] = "Double",
    ["Tags"] = "String[]"
};

// 데이터 생성
var data = new List<Dictionary<string, object>>
{
    new() { ["ID"] = 1, ["Name"] = "User1", ["Score"] = 95.5, ["Tags"] = new[] { "vip", "active" } },
    new() { ["ID"] = 2, ["Name"] = "User2", ["Score"] = 87.3, ["Tags"] = new[] { "new" } }
};

// 저장
var writer = new BinaryWriter();
writer.WriteToFile(columnTypes, data, "data.bin");

// 또는 바이트 배열로 변환
byte[] bytes = writer.WriteToBytes(columnTypes, data);
```

### 바이너리 파일 읽기

```csharp
using BinaryParser;

var reader = new BinaryReader();

// 파일에서 읽기
var data = reader.ReadFromFile("data.bin");

// 또는 바이트 배열에서 읽기
var data = reader.ReadFromBytes(bytes);

// 데이터 접근
foreach (var row in data)
{
    var id = (int)row["ID"];
    var name = (string)row["Name"];
    var score = (double)row["Score"];
    var tags = (string[])row["Tags"];
}
```

### 스키마만 읽기

```csharp
using BinaryParser;

// 바이너리 파일의 스키마(칼럼 정보)만 추출
var schema = BinarySchema.ReadFromBytes(bytes);

// 스키마 정보 접근
Console.WriteLine($"칼럼 수: {schema.ColumnCount}");
Console.WriteLine($"행 수: {schema.RowCount}");

foreach (var columnName in schema.GetColumnNames())
{
    var columnType = schema.GetColumnType(columnName);
    Console.WriteLine($"  {columnName}: {columnType}");
}
```

## C++ 사용법

### 헤더 파일 포함

```cpp
#include "BinaryReader.h"
```

### 파일에서 읽기

```cpp
BinaryParser::BinaryReader reader;
auto data = reader.ReadFromFile("data.bin");

// 파일 정보 확인
const auto& fileInfo = reader.GetLastFileInfo();
std::cout << "Columns: " << fileInfo.columnCount << std::endl;
std::cout << "Rows: " << fileInfo.rowCount << std::endl;

// 데이터 접근
for (const auto& row : data)
{
    // std::variant에서 값 추출
    auto id = std::get<int32_t>(row.at("ID"));
    auto name = std::get<std::string>(row.at("Name"));
    auto score = std::get<double>(row.at("Score"));
    
    std::cout << id << ", " << name << ", " << score << std::endl;
}
```

### 바이트 배열에서 읽기

```cpp
const uint8_t* binaryData = /* ... */;
size_t dataSize = /* ... */;

BinaryParser::BinaryReader reader;
auto data = reader.ReadFromBytes(binaryData, dataSize);
```

### C++ 지원 타입

```cpp
// ColumnValue는 다음 타입들의 std::variant입니다:
using ColumnValue = std::variant<
    std::nullptr_t,           // null
    int16_t,                  // short
    int32_t,                  // int
    int64_t,                  // long
    float,                    // float
    double,                   // double
    bool,                     // bool
    std::string,              // string
    std::vector<int16_t>,     // short[]
    std::vector<int32_t>,     // int[]
    std::vector<int64_t>,     // long[]
    std::vector<float>,       // float[]
    std::vector<double>,      // double[]
    std::vector<std::string>  // string[]
>;
```

## 지원 데이터 타입

| C# 타입 | 저장 형식 | C++ 타입 |
|---------|-----------|----------|
| `short` / `Int16` | 2 bytes | `int16_t` |
| `int` / `Int32` | 4 bytes | `int32_t` |
| `long` / `Int64` | 8 bytes | `int64_t` |
| `float` / `Single` | 4 bytes | `float` |
| `double` / `Double` | 8 bytes | `double` |
| `bool` / `Boolean` | 1 byte | `bool` |
| `string` / `String` | 7bit 길이 + UTF-8 | `std::string` |
| `DateTime` | 8 bytes (Ticks) | `int64_t` |
| `T[]` (배열) | 길이(int32) + 요소들 | `std::vector<T>` |
| Enum | 4 bytes (int32) | `int32_t` |
| null | 1 byte (true) | `std::nullptr_t` |

## 빌드 요구사항

### C#
- .NET 9.0 이상

### C++
- C++17 이상
- STL 지원 (variant, string, vector, unordered_map)
