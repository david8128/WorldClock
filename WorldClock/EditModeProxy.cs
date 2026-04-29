using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorldClock.Services;

namespace WorldClock;

/// <summary>
/// Thin singleton proxy that mirrors <see cref="ThemeService.EditMode"/>
/// so XAML DataTriggers inside DataTemplates can bind to it without a
/// DataContext collision (the DataContext is a <see cref="Models.ClockLocation"/>).
/// </summary>
public sealed class EditModeProxy : INotifyPropertyChanged
{
    public static EditModeProxy Instance { get; } = new();

    private EditModeProxy()
    {
        ThemeService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeService.EditMode))
                OnPropertyChanged(nameof(EditMode));
        };
    }

    public bool EditMode => ThemeService.Instance.EditMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
