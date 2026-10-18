using System.IO;
using System.Windows;
using TableEditor.Services;

namespace TableEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // CLI와 동일하게 실행 파일 위치를 기준으로 상대 경로(Config/, Binary/ 등)를 해석한다
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        // 엔진 Logger는 Console에 쓰므로, Console 출력을 GUI 로그 패널로 리다이렉트한다
        Console.SetOut(new UiLogWriter());

        base.OnStartup(e);
    }
}
