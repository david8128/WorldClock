using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WorldClock.Services;
using WorldClock.ViewModels;

namespace WorldClock;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow() : this(null!) { }

    public DiagnosticsWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        // Closing the window also turns off the setting so it doesn't reopen on next launch
        ThemeService.Instance.ShowDiagnostics = false;
        Close();
    }
}
