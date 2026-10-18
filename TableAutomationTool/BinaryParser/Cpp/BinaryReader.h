// 자동 생성된 코드 - 수정하지 마세요
// C++ Binary Reader for structured binary files

#ifndef BINARY_READER_H
#define BINARY_READER_H

#include <cstdint>
#include <string>
#include <vector>
#include <unordered_map>
#include <fstream>
#include <memory>
#include <variant>

namespace BinaryParser
{
    // 지원되는 데이터 타입
    using ColumnValue = std::variant<
        std::nullptr_t,
        int16_t,
        int32_t,
        int64_t,
        float,
        double,
        bool,
        std::string,
        std::vector<int16_t>,
        std::vector<int32_t>,
        std::vector<int64_t>,
        std::vector<float>,
        std::vector<double>,
        std::vector<std::string>
    >;

    // 행 데이터 타입
    using RowData = std::unordered_map<std::string, ColumnValue>;

    /**
     * @brief 칼럼 정보를 담는 구조체
     */
    struct ColumnInfo
    {
        std::string name;
        std::string type;
    };

    /**
     * @brief 파일 헤더 정보를 담는 구조체
     */
    struct FileInfo
    {
        int32_t columnCount = 0;
        int32_t rowCount = 0;
        std::vector<ColumnInfo> columns;
    };

    /**
     * @brief 구조화된 바이너리 파일을 읽어오는 클래스
     */
    class BinaryReader
    {
    public:
        BinaryReader() = default;
        ~BinaryReader() = default;

        // 복사/이동 생성자 및 할당 연산자
        BinaryReader(const BinaryReader&) = delete;
        BinaryReader& operator=(const BinaryReader&) = delete;
        BinaryReader(BinaryReader&&) = default;
        BinaryReader& operator=(BinaryReader&&) = default;

        /**
         * @brief 바이너리 파일에서 데이터를 읽어옵니다
         * @param filePath 읽을 파일 경로
         * @return 변환된 데이터 (vector<RowData> 형태)
         */
        std::vector<RowData> ReadFromFile(const std::string& filePath);

        /**
         * @brief 바이트 배열에서 데이터를 읽어옵니다
         * @param data 바이너리 데이터 포인터
         * @param size 데이터 크기
         * @return 변환된 데이터 (vector<RowData> 형태)
         */
        std::vector<RowData> ReadFromBytes(const uint8_t* data, size_t size);

        /**
         * @brief 마지막 읽기 작업의 파일 정보를 반환합니다
         */
        const FileInfo& GetLastFileInfo() const { return m_lastFileInfo; }

    private:
        /**
         * @brief 스트림에서 데이터를 읽어옵니다
         */
        std::vector<RowData> ReadDataFromStream(std::istream& stream);

        /**
         * @brief 헤더 정보를 읽어옵니다
         */
        FileInfo ReadHeader(std::istream& stream);

        /**
         * @brief 칼럼별로 데이터를 읽어옵니다
         */
        std::unordered_map<std::string, std::vector<ColumnValue>> ReadDataByColumns(
            const FileInfo& fileInfo, std::istream& stream);

        /**
         * @brief 칼럼 데이터를 행 딕셔너리로 변환합니다
         */
        std::vector<RowData> ConvertToRowDictionaries(
            const FileInfo& fileInfo,
            const std::unordered_map<std::string, std::vector<ColumnValue>>& columnData);

        /**
         * @brief 타입에 따라 값을 읽어옵니다
         */
        ColumnValue ReadValueByType(const std::string& type, std::istream& stream);

        /**
         * @brief 배열 값을 읽어옵니다
         */
        ColumnValue ReadArrayValue(const std::string& elementType, std::istream& stream);

        // Helper 함수들
        int16_t ReadInt16(std::istream& stream);
        int32_t ReadInt32(std::istream& stream);
        int64_t ReadInt64(std::istream& stream);
        float ReadFloat(std::istream& stream);
        double ReadDouble(std::istream& stream);
        bool ReadBool(std::istream& stream);
        std::string ReadString(std::istream& stream);

        FileInfo m_lastFileInfo;
    };

} // namespace BinaryParser

#endif // BINARY_READER_H
