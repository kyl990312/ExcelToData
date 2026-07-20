using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TableEditor.ViewModels;

namespace TableEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(Dispatcher);
    }

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
        => LogBox.ScrollToEnd();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Edit.HasChanges)
        {
            var result = MessageBox.Show(
                "저장하지 않은 변경이 있습니다. 저장 없이 종료할까요?",
                "Table Editor",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
