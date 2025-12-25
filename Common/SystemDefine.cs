namespace Common;

/// <summary>
/// 시스템 전역 설정
/// </summary>
public static class SystemDefine
{
    /// <summary>
    /// 개발 모드 활성화 여부
    /// true인 경우 테스트/검증용 커맨드가 실행됨
    /// </summary>
    public static bool DevelopmentMode { get; set; } = true;
}