namespace TableAutomationTool.EnumGenerator;

/// <summary>
/// EnumGenerator 설정.
///
/// Enum 은 툴이 만들지 않는다. 프로그래머가 클라이언트에 직접 작성한 C# enum 소스 파일을
/// SystemEnumFiles 에 등록하면, 툴이 그 파일을 읽어 EnumDB(Binary/EnumDB.bin)를 만든다.
/// EnumDB 는 데이터 추출(enum 셀 검증·숫자→멤버명 변환)과 코드 생성(enum 네임스페이스 using)에서 쓴다.
/// </summary>
public class EnumGeneratorConfig
{
    /// <summary>
    /// enum 이 정의된 C# 소스 파일 경로 목록.
    /// 상대 경로는 다른 설정과 같이 실행 파일 폴더 기준이다.
    /// 파일 이름(확장자 제외)이 EnumDB 의 시트 이름이 된다.
    /// </summary>
    public string[] SystemEnumFiles { get; set; } = [];

    public static EnumGeneratorConfig GetDefault()
    {
        return new EnumGeneratorConfig()
        {
            SystemEnumFiles = [],
        };
    }
}
