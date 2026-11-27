using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Noctis.Models;
using Noctis.Services;

namespace Noctis.ViewModels;

/// <summary>
/// Settings → Beta Features: grouped opt-in experiments (all off by default)
/// so individual fixes can be tested side-by-side against stock behavior.
/// </summary>
public partial class SettingsViewModel
{
    [ObservableProperty] private bool _betaPreloadNextVideo;
    [ObservableProperty] private bool _betaShareVideoStream;
    [ObservableProperty] private bool _betaSmoothVideoSync;
    [ObservableProperty] private bool _betaSeekBackgroundVideoWithSong;
    [ObservableProperty] private bool _betaGentleHddFileCaching;
    [ObservableProperty] private bool _betaSmartAnimatedCoverThreads;
    [ObservableProperty] private bool _betaLowOverheadTrackSwitch;

    partial void OnBetaPreloadNextVideoChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaShareVideoStreamChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaSmoothVideoSyncChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaSeekBackgroundVideoWithSongChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaGentleHddFileCachingChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaSmartAnimatedCoverThreadsChanged(bool value) => OnBetaSwitchChanged();
    partial void OnBetaLowOverheadTrackSwitchChanged(bool value) => OnBetaSwitchChanged();

    private void OnBetaSwitchChanged()
    {
        ApplyBetaSettings();
        if (_settingsLoaded) _ = SaveAsync();
    }

    private void LoadBetaSettings(AppSettings source)
    {
        BetaPreloadNextVideo = source.BetaPreloadNextVideo;
        BetaShareVideoStream = source.BetaShareVideoStream;
        BetaSmoothVideoSync = source.BetaSmoothVideoSync;
        BetaSeekBackgroundVideoWithSong = source.BetaSeekBackgroundVideoWithSong;
        BetaGentleHddFileCaching = source.BetaGentleHddFileCaching;
        BetaSmartAnimatedCoverThreads = source.BetaSmartAnimatedCoverThreads;
        BetaLowOverheadTrackSwitch = source.BetaLowOverheadTrackSwitch;
    }

    private void SyncBetaSettings(AppSettings target)
    {
        target.BetaPreloadNextVideo = BetaPreloadNextVideo;
        target.BetaShareVideoStream = BetaShareVideoStream;
        target.BetaSmoothVideoSync = BetaSmoothVideoSync;
        target.BetaSeekBackgroundVideoWithSong = BetaSeekBackgroundVideoWithSong;
        target.BetaGentleHddFileCaching = BetaGentleHddFileCaching;
        target.BetaSmartAnimatedCoverThreads = BetaSmartAnimatedCoverThreads;
        target.BetaLowOverheadTrackSwitch = BetaLowOverheadTrackSwitch;
    }

    private void ResetBetaSettings(AppSettings defaults) => LoadBetaSettings(defaults);

    private void ApplyBetaSettings()
    {
        BetaFeaturesConfig.Apply(
            BetaPreloadNextVideo,
            BetaShareVideoStream,
            BetaSmoothVideoSync,
            BetaSeekBackgroundVideoWithSong,
            BetaGentleHddFileCaching,
            BetaSmartAnimatedCoverThreads,
            BetaLowOverheadTrackSwitch);

        if (_player != null)
        {
            _player.BetaPreloadNextVideo = BetaPreloadNextVideo;
            _player.BetaShareVideoStream = BetaShareVideoStream;
            _player.BetaSmoothVideoSync = BetaSmoothVideoSync;
            _player.BetaSeekBackgroundVideoWithSong = BetaSeekBackgroundVideoWithSong;
            _player.BetaLowOverheadTrackSwitch = BetaLowOverheadTrackSwitch;
        }
    }

    [RelayCommand]
    private void ResetBetaFeatures()
    {
        BetaPreloadNextVideo = false;
        BetaShareVideoStream = false;
        BetaSmoothVideoSync = false;
        BetaSeekBackgroundVideoWithSong = false;
        BetaGentleHddFileCaching = false;
        BetaSmartAnimatedCoverThreads = false;
        BetaLowOverheadTrackSwitch = false;
    }
}
