using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.ViewModels;

/// <summary>One row on the Startup Apps list, with its own Enable/Disable toggle.</summary>
public sealed partial class StartupAppItemViewModel : ObservableObject
{
    private readonly Func<StartupAppInfo, bool, Task<ActionResult>> _setEnabled;

    public StartupAppInfo Model { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _lastResultMessage;

    public StartupAppItemViewModel(StartupAppInfo model, Func<StartupAppInfo, bool, Task<ActionResult>> setEnabled)
    {
        Model = model;
        _isEnabled = model.IsCurrentlyEnabled;
        _setEnabled = setEnabled;
    }

    public string Name => Model.Name;
    public string CommandLine => Model.CommandLine;
    public bool RequiresAdmin => Model.RequiresAdminToDisable;
    public string Location => Model.Location.ToString();

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var desired = !IsEnabled;
            var result = await _setEnabled(Model, desired).ConfigureAwait(true);
            LastResultMessage = result.Message;
            if (result.Success)
            {
                IsEnabled = desired;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
