using System.Linq;
using RobloxOptimizer.Backup;
using RobloxOptimizer.Core;
using Xunit;

namespace RobloxOptimizer.Tests;

/// <summary>
/// Exercises the real <see cref="BackupService"/> against its real, documented
/// storage location (<see cref="AppPaths.BackupFilePath"/>) since the service
/// intentionally has no injectable path (see docs/ARCHITECTURE.md - the backup
/// file's fixed, predictable location is part of what makes it trustworthy).
/// Each test cleans up after itself so runs don't interfere with each other
/// or with a real installation's backup file.
/// </summary>
[Collection("BackupFile")]
public class BackupServiceTests : IDisposable
{
    private readonly BackupService _sut = new();

    public BackupServiceTests()
    {
        if (File.Exists(AppPaths.BackupFilePath))
        {
            File.Delete(AppPaths.BackupFilePath);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmptySnapshot_WhenNoFileExists()
    {
        var snapshot = await _sut.LoadAsync();

        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public async Task RecordEntryAsync_ThenGetEntryAsync_RoundTripsThePreviousValue()
    {
        await _sut.RecordEntryAsync("test-action", "state", "original-value");

        var entry = await _sut.GetEntryAsync("test-action", "state");

        Assert.NotNull(entry);
        Assert.Equal("original-value", entry!.PreviousValue);
    }

    [Fact]
    public async Task RecordEntryAsync_Twice_ReplacesRatherThanDuplicates()
    {
        await _sut.RecordEntryAsync("test-action", "state", "first");
        await _sut.RecordEntryAsync("test-action", "state", "second");

        var snapshot = await _sut.LoadAsync();

        var matching = snapshot.Entries.Where(e => e.ActionId == "test-action" && e.Key == "state").ToList();
        Assert.Single(matching);
        Assert.Equal("second", matching[0].PreviousValue);
    }

    [Fact]
    public async Task RemoveEntryAsync_DeletesTheEntry()
    {
        await _sut.RecordEntryAsync("test-action", "state", "value");

        await _sut.RemoveEntryAsync("test-action", "state");

        var entry = await _sut.GetEntryAsync("test-action", "state");
        Assert.Null(entry);
    }

    [Fact]
    public async Task LoadAsync_SurvivesACorruptedBackupFile()
    {
        AppPaths.EnsureRootFolderExists();
        await File.WriteAllTextAsync(AppPaths.BackupFilePath, "{ this is not valid json");

        var snapshot = await _sut.LoadAsync();

        Assert.Empty(snapshot.Entries);
    }

    public void Dispose()
    {
        if (File.Exists(AppPaths.BackupFilePath))
        {
            File.Delete(AppPaths.BackupFilePath);
        }
    }
}
