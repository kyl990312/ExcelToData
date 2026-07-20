using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TableEditor.Core;

/// <summary>
/// INotifyPropertyChanged 기본 구현
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// 범용 ICommand 구현 (CommandManager 기반 CanExecute 갱신)
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// 체크박스 목록 아이템 (데이터/코드 추출 탭 공용)
/// </summary>
public sealed class SelectableItem : ViewModelBase
{
    private bool _isChecked;

    public SelectableItem(string name) => Name = name;

    public string Name { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }
}
