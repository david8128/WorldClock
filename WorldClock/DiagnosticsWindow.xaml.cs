using System.Windows;
using System.Windows.Input;
using WorldClock.Services;
using WorldClock.ViewModels;

namespace WorldClock;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Closing the window also turns off the setting so it doesn't reopen on next launch
        ThemeService.Instance.ShowDiagnostics = false;
        Close();
    }
}
