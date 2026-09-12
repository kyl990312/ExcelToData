using TableAutomationTool.CodeGenerator;
using TableAutomationTool.EnumGenerator;
using TableAutomationTool.ExcelToData;

namespace TableEditor.Services;

/// <summary>
/// TableAutomationTool 엔진(Invoker)을 백그라운드로 실행한다.
/// 동시 실행은 1개로 제한하고, 상태/완료를 이벤트로 알린다.
/// </summary>
public class PipelineService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsBusy { get; private set; }

    public event Action<bool>? BusyChanged;
    public event Action<string>? Status;

    public Task ExtractAll()
        => RunAsync("데이터 추출(전체)", () => new ETDCommandInvoker(Array.Empty<string>()).ExecuteAll());

    public Task ExtractEach(IReadOnlyList<string> names)
        => RunAsync($"데이터 추출(선택 {names.Count}건)", () =>
        {
            foreach (var name in names)
            {
                UiLog.Write($"[TableEditor] extract -each {name}");
                new ETDCommandInvoker(new[] { "-each", name }).ExecuteAll();
            }
        });

    public Task CodeGenAll(bool withEnum)
        => RunAsync("코드 생성(전체)", () =>
        {
            if (withEnum)
                new EGCommandInvoker(Array.Empty<string>()).ExecuteAll();
            new CGCommandInvoker(Array.Empty<string>()).ExecuteAll();
        });

    public Task CodeGenEach(IReadOnlyList<string> names, bool withEnum)
        => RunAsync($"코드 생성(선택 {names.Count}건)", () =>
        {
            if (withEnum)
                new EGCommandInvoker(Array.Empty<string>()).ExecuteAll();
            foreach (var name in names)
            {
                UiLog.Write($"[TableEditor] generate -each {name}");
                new CGCommandInvoker(new[] { "-each", name }).ExecuteAll();
            }
        });

    private async Task RunAsync(string title, Action work)
    {
        if (!await _gate.WaitAsync(0))
        {
            Status?.Invoke("이미 다른 작업이 실행 중입니다.");
            return;
        }

        IsBusy = true;
        BusyChanged?.Invoke(true);
        Status?.Invoke($"{title} 실행 중…");
        try
        {
            await Task.Run(work);
            Status?.Invoke($"{title} 완료");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] {title} 실패: {ex}");
            Status?.Invoke($"{title} 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyChanged?.Invoke(false);
            _gate.Release();
        }
    }
}
