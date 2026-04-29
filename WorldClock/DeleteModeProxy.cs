using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorldClock.Services;

namespace WorldClock;

/// <summary>
/// Thin singleton proxy that mirrors <see cref="ThemeService.DeleteMode"/>
/// so XAML DataTriggers can bind to it without a DataContext collision.
/// </summary>
public sealed class DeleteModeProxy : INotifyPropertyChanged
{
    public static DeleteModeProxy Instance { get; } = new();

    private DeleteModeProxy()
    {
        ThemeService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeService.DeleteMode))
                OnPropertyChanged(nameof(DeleteMode));
        };
    }

    public bool DeleteMode => ThemeService.Instance.DeleteMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
