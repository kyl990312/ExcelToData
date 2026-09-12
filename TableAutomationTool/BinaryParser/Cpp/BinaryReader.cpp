// 자동 생성된 코드 - 수정하지 마세요
// C++ Binary Reader for structured binary files

#include "BinaryReader.h"
#include <cstring>
#include <sstream>
#include <stdexcept>

namespace BinaryParser
{
    std::vector<RowData> BinaryReader::ReadFromFile(const std::string& filePath)
    {
        std::ifstream file(filePath, std::ios::binary);
        if (!file.is_open())
        {
            // 파일을 찾을 수 없음
            return std::vector<RowData>();
        }

        return ReadDataFromStream(file);
    }

    std::vector<RowData> BinaryReader::ReadFromBytes(const uint8_t* data, size_t size)
    {
        std::string buffer(reinterpret_cast<const char*>(data), size);
        std::istringstream stream(buffer, std::ios::binary);

        return ReadDataFromStream(stream);
    }

    std::vector<RowData> BinaryReader::ReadDataFromStream(std::istream& stream)
    {
        // Header 정보 읽기
        m_lastFileInfo = ReadHeader(stream);

        if (m_lastFileInfo.columnCount == 0 || m_lastFileInfo.rowCount == 0)
        {
            return std::vector<RowData>();
        }

        // Data 정보 읽기 (칼럼별로 읽기)
        auto columnData = ReadDataByColumns(m_lastFileInfo, stream);

        // Dictionary 형태로 변환
        return ConvertToRowDictionaries(m_lastFileInfo, columnData);
    }

    FileInfo BinaryReader::ReadHeader(std::istream& stream)
    {
        FileInfo info;

        // 1. 칼럼 갯수
        info.columnCount = ReadInt32(stream);

        // 2. 데이터 갯수 (행 갯수)
        info.rowCount = ReadInt32(stream);

        // 3. 각 칼럼의 메타데이터
        info.columns.reserve(info.columnCount);
        for (int32_t i = 0; i < info.columnCount; ++i)
        {
            ColumnInfo columnInfo;

            // 칼럼 이름
            columnInfo.name = ReadString(stream);

            // 칼럼 타입
            columnInfo.type = ReadString(stream);

            info.columns.push_back(std::move(columnInfo));
        }

        return info;
    }

    std::unordered_map<std::string, std::vector<ColumnValue>> BinaryReader::ReadDataByColumns(
        const FileInfo& fileInfo, std::istream& stream)
    {
        std::unordered_map<std::string, std::vector<ColumnValue>> columnData;

        // 각 칼럼별로 데이터 읽기
        for (const auto& columnInfo : fileInfo.columns)
        {
            std::vector<ColumnValue> columnValues;
            columnValues.reserve(fileInfo.rowCount);

            for (int32_t rowIndex = 0; rowIndex < fileInfo.rowCount; ++rowIndex)
            {
                // null 체크
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    columnValues.push_back(nullptr);
                    continue;
                }

                // 타입별 데이터 읽기
                auto value = ReadValueByType(columnInfo.type, stream);
                columnValues.push_back(std::move(value));
            }

            columnData[columnInfo.name] = std::move(columnValues);
        }

        return columnData;
    }

    std::vector<RowData> BinaryReader::ConvertToRowDictionaries(
        const FileInfo& fileInfo,
        const std::unordered_map<std::string, std::vector<ColumnValue>>& columnData)
    {
        std::vector<RowData> result;
        result.reserve(fileInfo.rowCount);

        for (int32_t rowIndex = 0; rowIndex < fileInfo.rowCount; ++rowIndex)
        {
            RowData rowDict;

            for (const auto& columnInfo : fileInfo.columns)
            {
                auto it = columnData.find(columnInfo.name);
                if (it != columnData.end() && 
                    static_cast<int32_t>(it->second.size()) > rowIndex)
                {
                    rowDict[columnInfo.name] = it->second[rowIndex];
                }
                else
                {
                    rowDict[columnInfo.name] = nullptr;
                }
            }

            result.push_back(std::move(rowDict));
        }

        return result;
    }

    ColumnValue BinaryReader::ReadValueByType(const std::string& type, std::istream& stream)
    {
        // 타입 문자열 정규화
        std::string normalizedType = type;
        
        // AssemblyQualifiedName에서 타입명만 추출
        size_t commaPos = type.find(',');
        if (commaPos != std::string::npos)
        {
            normalizedType = type.substr(0, commaPos);
            // 앞뒤 공백 제거
            size_t start = normalizedType.find_first_not_of(" ");
            size_t end = normalizedType.find_last_not_of(" ");
            if (start != std::string::npos)
            {
                normalizedType = normalizedType.substr(start, end - start + 1);
            }
        }

        // 배열 타입 체크
        if (normalizedType.length() >= 2 && 
            normalizedType.substr(normalizedType.length() - 2) == "[]")
        {
            std::string elementType = normalizedType.substr(0, normalizedType.length() - 2);
            return ReadArrayValue(elementType, stream);
        }

        // 기본 타입 처리
        if (normalizedType == "System.Int16" || normalizedType == "Int16" || normalizedType == "short")
        {
            return ReadInt16(stream);
        }
        else if (normalizedType == "System.Int32" || normalizedType == "Int32" || normalizedType == "int")
        {
            return ReadInt32(stream);
        }
        else if (normalizedType == "System.Int64" || normalizedType == "Int64" || normalizedType == "long")
        {
            return ReadInt64(stream);
        }
        else if (normalizedType == "System.Single" || normalizedType == "Single" || normalizedType == "float")
        {
            return ReadFloat(stream);
        }
        else if (normalizedType == "System.Double" || normalizedType == "Double" || normalizedType == "double")
        {
            return ReadDouble(stream);
        }
        else if (normalizedType == "System.String" || normalizedType == "String" || normalizedType == "string")
        {
            return ReadString(stream);
        }
        else if (normalizedType == "System.Boolean" || normalizedType == "Boolean" || normalizedType == "bool")
        {
            return ReadBool(stream);
        }
        else
        {
            // Enum이나 기타 타입은 정수로 처리
            return ReadInt32(stream);
        }
    }

    ColumnValue BinaryReader::ReadArrayValue(const std::string& elementType, std::istream& stream)
    {
        // 배열 길이 읽기
        int32_t length = ReadInt32(stream);

        if (elementType == "System.Int16" || elementType == "Int16" || elementType == "short")
        {
            std::vector<int16_t> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0);
                }
                else
                {
                    array.push_back(ReadInt16(stream));
                }
            }
            return array;
        }
        else if (elementType == "System.Int32" || elementType == "Int32" || elementType == "int")
        {
            std::vector<int32_t> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0);
                }
                else
                {
                    array.push_back(ReadInt32(stream));
                }
            }
            return array;
        }
        else if (elementType == "System.Int64" || elementType == "Int64" || elementType == "long")
        {
            std::vector<int64_t> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0);
                }
                else
                {
                    array.push_back(ReadInt64(stream));
                }
            }
            return array;
        }
        else if (elementType == "System.Single" || elementType == "Single" || elementType == "float")
        {
            std::vector<float> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0.0f);
                }
                else
                {
                    array.push_back(ReadFloat(stream));
                }
            }
            return array;
        }
        else if (elementType == "System.Double" || elementType == "Double" || elementType == "double")
        {
            std::vector<double> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0.0);
                }
                else
                {
                    array.push_back(ReadDouble(stream));
                }
            }
            return array;
        }
        else if (elementType == "System.String" || elementType == "String" || elementType == "string")
        {
            std::vector<std::string> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back("");
                }
                else
                {
                    array.push_back(ReadString(stream));
                }
            }
            return array;
        }
        else
        {
            // 기타 타입은 int32 배열로 처리
            std::vector<int32_t> array;
            array.reserve(length);
            for (int32_t i = 0; i < length; ++i)
            {
                bool isNull = ReadBool(stream);
                if (isNull)
                {
                    array.push_back(0);
                }
                else
                {
                    array.push_back(ReadInt32(stream));
                }
            }
            return array;
        }
    }

    int16_t BinaryReader::ReadInt16(std::istream& stream)
    {
        int16_t value = 0;
        stream.read(reinterpret_cast<char*>(&value), sizeof(value));
        return value;
    }

    int32_t BinaryReader::ReadInt32(std::istream& stream)
    {
        int32_t value = 0;
        stream.read(reinterpret_cast<char*>(&value), sizeof(value));
        return value;
    }

    int64_t BinaryReader::ReadInt64(std::istream& stream)
    {
        int64_t value = 0;
        stream.read(reinterpret_cast<char*>(&value), sizeof(value));
        return value;
    }

    float BinaryReader::ReadFloat(std::istream& stream)
    {
        float value = 0.0f;
        stream.read(reinterpret_cast<char*>(&value), sizeof(value));
        return value;
    }

    double BinaryReader::ReadDouble(std::istream& stream)
    {
        double value = 0.0;
        stream.read(reinterpret_cast<char*>(&value), sizeof(value));
        return value;
    }

    bool BinaryReader::ReadBool(std::istream& stream)
    {
        char value = 0;
        stream.read(&value, sizeof(value));
        return value != 0;
    }

    std::string BinaryReader::ReadString(std::istream& stream)
    {
        // .NET BinaryWriter의 문자열 형식: 7bit 인코딩된 길이 + UTF-8 문자열
        int32_t length = 0;
        int shift = 0;
        
        // 7bit 인코딩된 길이 읽기
        while (true)
        {
            char byte = 0;
            stream.read(&byte, 1);
            
            length |= (static_cast<int32_t>(byte & 0x7F) << shift);
            shift += 7;
            
            if ((byte & 0x80) == 0)
            {
                break;
            }
            
            if (shift >= 35)
            {
                // 잘못된 형식
                return "";
            }
        }

        if (length <= 0)
        {
            return "";
        }

        std::string result(length, '\0');
        stream.read(&result[0], length);
        
        return result;
    }

} // namespace BinaryParser
