using Noctis.Controls;
using Noctis.Models;
using Noctis.Services;
using Noctis.ViewModels;
using Xunit;

namespace Noctis.Tests;

public class BetaFeaturesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NoctisBetaTests", Guid.NewGuid().ToString("N"));

    public BetaFeaturesTests()
    {
        Directory.CreateDirectory(_root);
        BetaFeaturesConfig.Reset();
    }

    public void Dispose()
    {
        BetaFeaturesConfig.Reset();
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void BetaSettings_AllDefaultToOff_AndResetCleanly()
    {
        var defaults = new AppSettings();
        Assert.False(defaults.BetaPreloadNextVideo);
        Assert.False(defaults.BetaShareVideoStream);
        Assert.False(defaults.BetaSmoothVideoSync);
        Assert.False(defaults.BetaSeekBackgroundVideoWithSong);
        Assert.False(defaults.BetaGentleHddFileCaching);
        Assert.False(defaults.BetaSmartAnimatedCoverThreads);
        Assert.False(defaults.BetaLowOverheadTrackSwitch);

        var vm = new SettingsViewModel(new PersistenceService(_root), new FakeLibraryService(), new NoOpPlayHistory());
        vm.BetaPreloadNextVideo = true;
        vm.BetaShareVideoStream = true;
        vm.BetaSmoothVideoSync = true;
        vm.BetaSeekBackgroundVideoWithSong = true;
        vm.BetaGentleHddFileCaching = true;
        vm.BetaSmartAnimatedCoverThreads = true;
        vm.BetaLowOverheadTrackSwitch = true;

        Assert.True(BetaFeaturesConfig.PreloadNextVideo);
        Assert.True(BetaFeaturesConfig.SeekBackgroundVideoWithSong);

        vm.ResetBetaFeaturesCommand.Execute(null);

        Assert.False(vm.BetaPreloadNextVideo);
        Assert.False(vm.BetaShareVideoStream);
        Assert.False(vm.BetaSmoothVideoSync);
        Assert.False(vm.BetaSeekBackgroundVideoWithSong);
        Assert.False(vm.BetaGentleHddFileCaching);
        Assert.False(vm.BetaSmartAnimatedCoverThreads);
        Assert.False(vm.BetaLowOverheadTrackSwitch);
        Assert.False(BetaFeaturesConfig.PreloadNextVideo);
        Assert.False(BetaFeaturesConfig.SeekBackgroundVideoWithSong);
    }

    [Theory]
    // Shorter video (20s) under a longer song (90s): modulo loops cleanly across seam & seeks.
    [InlineData(false, 5000, 5000, 20000, false, VideoBackdrop.SyncAction.None, 5000, 1.0)]
    [InlineData(false, 19950, 20050, 20000, false, VideoBackdrop.SyncAction.None, 50, 1.0)]
    [InlineData(false, 1000, 75000, 20000, false, VideoBackdrop.SyncAction.Seek, 15000, 1.0)]
    [InlineData(true, 20000, 25000, 20000, false, VideoBackdrop.SyncAction.Restart, 5000, 1.0)]
    // Longer video (120s) tracks 1:1 while song runs and resets cleanly when trackEpoch forces 00:00.
    [InlineData(false, 45100, 45000, 120000, false, VideoBackdrop.SyncAction.None, 45000, 0.97)]
    [InlineData(false, 45000, 45150, 120000, false, VideoBackdrop.SyncAction.None, 45150, 1.03)]
    [InlineData(false, 60000, 0, 120000, true, VideoBackdrop.SyncAction.Seek, 0, 1.0)]
    public void EvaluateSync_HandlesLoopWrappingAndSmoothRateNudges(
        bool ended, long timeMs, long targetMs, long lengthMs, bool force,
        VideoBackdrop.SyncAction expectedAction, long expectedTargetMs, double expectedRate)
    {
        var decision = VideoBackdrop.EvaluateSync(
            ended, timeMs, targetMs, lengthMs, force, loopWithTrack: true, smoothSync: true);

        Assert.Equal(expectedAction, decision.Action);
        Assert.Equal(expectedTargetMs, decision.TargetMs);
        Assert.Equal(expectedRate, decision.RateMultiplier, precision: 3);
    }

    [Fact]
    public void PlaybackFileWarmer_GentlePolicy_ProtectsLowRamAndRespectsStartDelay()
    {
        Assert.Equal(PlaybackFileWarmer.GentleLowRamMaxBytes,
            PlaybackFileWarmer.EffectiveMaxBytes(gentle: true, 4L * 1024 * 1024 * 1024));
        Assert.Equal(PlaybackFileWarmer.MaxBytes,
            PlaybackFileWarmer.EffectiveMaxBytes(gentle: true, 16L * 1024 * 1024 * 1024));
        Assert.Equal(PlaybackFileWarmer.MaxBytes,
            PlaybackFileWarmer.EffectiveMaxBytes(gentle: false, 4L * 1024 * 1024 * 1024));

        Assert.Equal(PlaybackFileWarmer.GentleDefaultDelayMs,
            PlaybackFileWarmer.EffectiveStartDelayMs(0, gentle: true));
        Assert.Equal(0,
            PlaybackFileWarmer.EffectiveStartDelayMs(0, gentle: false));
    }

    [Fact]
    public async Task PlaybackFileWarmer_GentleMode_ReadsFullSmallFileWithoutErrors()
    {
        var path = Path.Combine(_root, "sample.flac");
        File.WriteAllBytes(path, new byte[2_200_000]);

        using var warmer = new PlaybackFileWarmer(startDelayMs: 0, gentleOverride: true);
        warmer.Warm(path);
        warmer.StartNow();
        await warmer.Current.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2_200_000, warmer.BytesRead);
    }

    private sealed class NoOpPlayHistory : IPlayHistoryService
    {
        public IReadOnlyList<PlayHistoryEvent> Events => Array.Empty<PlayHistoryEvent>();
        public Task PreloadAsync() => Task.CompletedTask;
        public void RecordPlay(Track track) { }
        public void RecordSkip(Track track) { }
        public Task FlushAsync() => Task.CompletedTask;
    }
}
