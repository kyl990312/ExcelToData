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
        base.OnClosing(e);
    }
}
