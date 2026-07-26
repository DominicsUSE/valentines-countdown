using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.ViewModels;

/// <summary>One row on the Background Apps list. Closing is not reversible - the user reopens the app themselves.</summary>
public sealed partial class BackgroundAppItemViewModel : ObservableObject
{
    private readonly Func<BackgroundAppInfo, Task<ActionResult>> _close;

    public BackgroundAppInfo Model { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isClosed;

    [ObservableProperty]
    private string? _lastResultMessage;

    public BackgroundAppItemViewModel(BackgroundAppInfo model, Func<BackgroundAppInfo, Task<ActionResult>> close)
    {
        Model = model;
        _close = close;
    }

    public string ProcessName => Model.ProcessName;
    public string WindowTitle => string.IsNullOrWhiteSpace(Model.WindowTitle) ? "(no window)" : Model.WindowTitle;
    public string CpuDisplay => Model.CpuPercent.IsAvailable ? $"{Model.CpuPercent.Value:0.#}%" : "Unavailable";
    public string MemoryDisplay => Model.MemoryMegabytes.IsAvailable ? $"{Model.MemoryMegabytes.Value:0} MB" : "Unavailable";

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (IsBusy || IsClosed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _close(Model).ConfigureAwait(true);
            LastResultMessage = result.Message;
            IsClosed = result.Success;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
