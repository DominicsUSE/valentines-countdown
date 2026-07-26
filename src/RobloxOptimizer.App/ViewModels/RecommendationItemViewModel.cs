using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.App.ViewModels;

/// <summary>One recommendation card: problem, change, benefit, side effects, admin requirement, and Apply/Undo.</summary>
public sealed partial class RecommendationItemViewModel : ObservableObject
{
    private readonly Func<Recommendation, Task<ActionResult>> _apply;
    private readonly Func<Recommendation, Task<ActionResult>> _undo;

    public Recommendation Model { get; }

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _lastResultMessage;

    public RecommendationItemViewModel(Recommendation model, Func<Recommendation, Task<ActionResult>> apply, Func<Recommendation, Task<ActionResult>> undo)
    {
        Model = model;
        _apply = apply;
        _undo = undo;
    }

    public string Title => Model.Title;
    public string DetectedProblem => Model.DetectedProblem;
    public string ProposedChange => Model.ProposedChange;
    public string ExpectedBenefit => Model.ExpectedBenefit;
    public string PossibleSideEffects => Model.PossibleSideEffects;
    public bool RequiresAdmin => Model.RequiresAdmin;
    public bool IsAutomatable => Model.IsAutomatable;

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (IsApplied || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apply(Model).ConfigureAwait(true);
            LastResultMessage = result.Message;
            IsApplied = result.Success;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!IsApplied || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _undo(Model).ConfigureAwait(true);
            LastResultMessage = result.Message;
            if (result.Success)
            {
                IsApplied = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
