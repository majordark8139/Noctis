namespace Noctis.Services;

/// <summary>
/// Thread-safe runtime snapshot of the experimental switches under Settings → Beta Features.
/// Every flag defaults to <c>false</c> so testers can opt in to individual changes cleanly.
/// </summary>
public static class BetaFeaturesConfig
{
    private static volatile bool _preloadNextVideo;
    private static volatile bool _shareVideoStream;
    private static volatile bool _smoothVideoSync;
    private static volatile bool _seekBackgroundVideoWithSong;
    private static volatile bool _gentleHddFileCaching;
    private static volatile bool _smartAnimatedCoverThreads;
    private static volatile bool _lowOverheadTrackSwitch;

    public static bool PreloadNextVideo => _preloadNextVideo;
    public static bool ShareVideoStream => _shareVideoStream;
    public static bool SmoothVideoSync => _smoothVideoSync;
    public static bool SeekBackgroundVideoWithSong => _seekBackgroundVideoWithSong;
    public static bool GentleHddFileCaching => _gentleHddFileCaching;
    public static bool SmartAnimatedCoverThreads => _smartAnimatedCoverThreads;
    public static bool LowOverheadTrackSwitch => _lowOverheadTrackSwitch;

    /// <summary>Raised when any Beta Features toggle changes.</summary>
    public static event EventHandler? Changed;

    public static void Apply(
        bool preloadNextVideo,
        bool shareVideoStream,
        bool smoothVideoSync,
        bool seekBackgroundVideoWithSong,
        bool gentleHddFileCaching,
        bool smartAnimatedCoverThreads,
        bool lowOverheadTrackSwitch)
    {
        var changed =
            _preloadNextVideo != preloadNextVideo ||
            _shareVideoStream != shareVideoStream ||
            _smoothVideoSync != smoothVideoSync ||
            _seekBackgroundVideoWithSong != seekBackgroundVideoWithSong ||
            _gentleHddFileCaching != gentleHddFileCaching ||
            _smartAnimatedCoverThreads != smartAnimatedCoverThreads ||
            _lowOverheadTrackSwitch != lowOverheadTrackSwitch;

        _preloadNextVideo = preloadNextVideo;
        _shareVideoStream = shareVideoStream;
        _smoothVideoSync = smoothVideoSync;
        _seekBackgroundVideoWithSong = seekBackgroundVideoWithSong;
        _gentleHddFileCaching = gentleHddFileCaching;
        _smartAnimatedCoverThreads = smartAnimatedCoverThreads;
        _lowOverheadTrackSwitch = lowOverheadTrackSwitch;

        if (changed)
            Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Reset() =>
        Apply(false, false, false, false, false, false, false);

    /// <summary>Capped decoder thread count when low-memory/smooth video options are enabled.</summary>
    public static int RecommendedVideoDecoderThreads() =>
        Math.Clamp(Environment.ProcessorCount / 2, 1, 2);
}
