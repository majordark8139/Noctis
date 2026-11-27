using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;
using Noctis.Helpers;
using Noctis.Services;

namespace Noctis.Controls;

/// <summary>
/// Full-bleed looping video / GIF background (lyrics page). Same mechanism as
/// <see cref="AnimatedCoverImage"/> — LibVLC decodes frames into a <see cref="WriteableBitmap"/>
/// shown by a plain <c>Image</c>, so the clip composes under the lyrics, clips to the
/// page and never spawns a native video window — but tuned for a backdrop:
/// <list type="bullet">
/// <item>The decode buffer keeps the clip's aspect ratio and is capped at
/// <see cref="MaxLongSide"/> px on the long side (a 4K clip decodes at 960px — behind a
/// blur-strength scrim nothing above that is visible, and it keeps the per-frame copy
/// to ~2 MB instead of ~33 MB).</item>
/// <item>Frames are coalesced: at most one UI invalidation is in flight, so a 60 fps
/// clip on a busy UI thread drops frames instead of queueing them.</item>
/// <item>Decoding stops whenever the control is detached, <see cref="IsActive"/> is
/// false, or the hosting window is minimized — a hidden backdrop costs nothing.</item>
/// </list>
/// Software-decoded (required for the callback path); GIF plays through LibVLC's
/// image/avformat demuxers like any video.
/// </summary>
public sealed class VideoBackdrop : Control, IVideoFeedConsumer
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<VideoBackdrop, string?>(nameof(Source));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VideoBackdrop, bool>(nameof(IsActive), defaultValue: true);

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Holds the clip on its current frame (Settings: pause the video with playback).
    /// Combined with the window-minimized park; the decoder resumes where it stopped.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<VideoBackdrop, bool>(nameof(IsPaused));

    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <summary>
    /// Music-video mode: the clip follows this playback position instead of looping
    /// freely. Null (default) keeps the free-running backdrop behaviour. The clip is
    /// re-seeked whenever it drifts more than <see cref="SyncToleranceMs"/> from it.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> SyncPositionProperty =
        AvaloniaProperty.Register<VideoBackdrop, TimeSpan?>(nameof(SyncPosition));
    public TimeSpan? SyncPosition
    {
        get => GetValue(SyncPositionProperty);
        set => SetValue(SyncPositionProperty, value);
    }

    /// <summary>UniformToFill (backdrop) or Uniform (music video inside the artwork slot).</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<VideoBackdrop, Stretch>(nameof(Stretch), defaultValue: Stretch.UniformToFill);
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Corner radius of the painted frame (music video: flat or rounded).</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<VideoBackdrop, double>(nameof(CornerRadius));
    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>False once the current source turned out to have no video stream (or could
    /// not be read): the page keeps the cover up instead of an empty frame.</summary>
    public static readonly DirectProperty<VideoBackdrop, bool> HasVideoStreamProperty =
        AvaloniaProperty.RegisterDirect<VideoBackdrop, bool>(nameof(HasVideoStream), o => o.HasVideoStream);
    private bool _hasVideoStream = true;
    public bool HasVideoStream
    {
        get => _hasVideoStream;
        private set => SetAndRaise(HasVideoStreamProperty, ref _hasVideoStream, value);
    }

    /// <summary>Music-video mode: the clip's playback speed, following the song's (Playback
    /// speed 0.75x-2x). Without it a sped-up song drifts past the tolerance every tick and the
    /// clip stutters from re-seek to re-seek.</summary>
    public static readonly StyledProperty<double> RateProperty =
        AvaloniaProperty.Register<VideoBackdrop, double>(nameof(Rate), defaultValue: 1.0);
    public double Rate
    {
        get => GetValue(RateProperty);
        set => SetValue(RateProperty, value);
    }

    /// <summary>Long-side cap of the decode buffer for this instance (default
    /// <see cref="MaxLongSide"/>); the full-size music video view raises it so the clip
    /// is not upscaled from 960 px. Read when a clip starts.</summary>
    public static readonly StyledProperty<int> MaxDecodeSideProperty =
        AvaloniaProperty.Register<VideoBackdrop, int>(nameof(MaxDecodeSide), defaultValue: MaxLongSide);
    public int MaxDecodeSide
    {
        get => GetValue(MaxDecodeSideProperty);
        set => SetValue(MaxDecodeSideProperty, value);
    }

    public const int SyncToleranceMs = 350;
    public const int SmoothSyncToleranceMs = 80;

    /// <summary>When true (Beta: Seek Background Video With Song), a clip shorter than the track loops cleanly
    /// in phase with song position while seeking forward/back, and a longer clip stops when the track changes.</summary>
    public static readonly StyledProperty<bool> SyncLoopWithTrackProperty =
        AvaloniaProperty.Register<VideoBackdrop, bool>(nameof(SyncLoopWithTrack));
    public bool SyncLoopWithTrack
    {
        get => GetValue(SyncLoopWithTrackProperty);
        set => SetValue(SyncLoopWithTrackProperty, value);
    }

    /// <summary>Bumped on each new track start so loop-synced background videos seek back to 00:00 even
    /// when two consecutive tracks use the same background clip.</summary>
    public static readonly StyledProperty<int> TrackEpochProperty =
        AvaloniaProperty.Register<VideoBackdrop, int>(nameof(TrackEpoch));
    public int TrackEpoch
    {
        get => GetValue(TrackEpochProperty);
        set => SetValue(TrackEpochProperty, value);
    }

    /// <summary>What a sync request does to the clip.</summary>
    internal enum SyncAction { None, Seek, Restart }

    internal readonly record struct SyncDecision(SyncAction Action, long TargetMs, double RateMultiplier);

    internal static long EffectiveTargetMs(long targetMs, long lengthMs, bool loopWithTrack)
    {
        var clamped = Math.Max(0, targetMs);
        return loopWithTrack && lengthMs > 0 ? clamped % lengthMs : clamped;
    }

    /// <summary>
    /// A sync request against the clip's state: seek when it drifted past the tolerance (or
    /// when forced); restart when it has ENDED — VLC ignores a seek after the end, so a song
    /// replayed or sought back would otherwise leave the clip frozen — unless the song is
    /// already past the clip's end (<paramref name="lengthMs"/>; 0 = unknown, never restart).
    /// </summary>
    internal static SyncAction PlanSync(bool ended, long timeMs, long targetMs, long lengthMs, int toleranceMs, bool force)
        => PlanSync(ended, timeMs, targetMs, lengthMs, toleranceMs, force, loopWithTrack: false);

    internal static SyncAction PlanSync(
        bool ended, long timeMs, long targetMs, long lengthMs, int toleranceMs, bool force, bool loopWithTrack)
    {
        var effective = EffectiveTargetMs(targetMs, lengthMs, loopWithTrack);
        if (ended)
        {
            if (loopWithTrack && lengthMs > 0) return SyncAction.Restart;
            return lengthMs > 0 && effective < lengthMs - toleranceMs ? SyncAction.Restart : SyncAction.None;
        }
        if (force) return SyncAction.Seek;

        var diff = Math.Abs(timeMs - effective);
        if (loopWithTrack && lengthMs > 0)
            diff = Math.Min(diff, lengthMs - diff);

        return diff > toleranceMs ? SyncAction.Seek : SyncAction.None;
    }

    internal static SyncDecision EvaluateSync(
        bool ended, long timeMs, long targetMs, long lengthMs,
        bool force, bool loopWithTrack, bool smoothSync, bool isPaused = false)
    {
        var effective = EffectiveTargetMs(targetMs, lengthMs, loopWithTrack);
        if (ended)
        {
            var restart = (loopWithTrack && lengthMs > 0) ||
                          (lengthMs > 0 && effective < lengthMs - (smoothSync ? SmoothSyncToleranceMs : SyncToleranceMs));
            return new SyncDecision(restart ? SyncAction.Restart : SyncAction.None, effective, 1.0);
        }

        if (!smoothSync || isPaused)
        {
            var tolerance = smoothSync ? SmoothSyncToleranceMs : SyncToleranceMs;
            var action = PlanSync(false, timeMs, targetMs, lengthMs, tolerance, force, loopWithTrack);
            return new SyncDecision(action, effective, 1.0);
        }

        if (force)
            return new SyncDecision(SyncAction.Seek, effective, 1.0);

        var signedDiff = effective - timeMs;
        if (loopWithTrack && lengthMs > 0)
        {
            if (signedDiff > lengthMs / 2) signedDiff -= lengthMs;
            else if (signedDiff < -lengthMs / 2) signedDiff += lengthMs;
        }

        var absDiff = Math.Abs(signedDiff);
        if (absDiff > 250)
            return new SyncDecision(SyncAction.Seek, effective, 1.0);
        if (absDiff > 60)
            return new SyncDecision(SyncAction.None, effective, signedDiff > 0 ? 1.03 : 0.97);

        return new SyncDecision(SyncAction.None, effective, 1.0);
    }

    /// <summary>Long-side cap for the decode buffer.</summary>
    public const int MaxLongSide = 960;

    /// <summary>
    /// Decode-buffer size for a clip of <paramref name="width"/>×<paramref name="height"/>:
    /// the same aspect ratio, long side capped at <paramref name="maxLongSide"/> (default
    /// <see cref="MaxLongSide"/>), both sides even (VLC's chroma planes want even dimensions)
    /// and at least 2 px. Unknown dimensions (0) fall back to a 16:9 buffer at the cap.
    /// </summary>
    public static (int Width, int Height) FitBuffer(int width, int height, int maxLongSide = MaxLongSide)
    {
        if (maxLongSide < 2) maxLongSide = MaxLongSide;
        if (width <= 0 || height <= 0) { width = maxLongSide; height = maxLongSide * 9 / 16; }
        var scale = Math.Min(1.0, (double)maxLongSide / Math.Max(width, height));
        var w = Math.Max(2, (int)Math.Round(width * scale)) & ~1;
        var h = Math.Max(2, (int)Math.Round(height * scale)) & ~1;
        return (Math.Max(2, w), Math.Max(2, h));
    }

    private VideoDecodeSession? _session;
    private VideoFeed? _feed;
    private WriteableBitmap? _bridgeFrame;
    private string? _activeSource;
    private int _generation;
    private WriteableBitmap? _current;   // frame currently painted (owned by the session, feed, or _bridgeFrame)
    private bool _currentDrawn;          // Render has picked _current up since it was set
    private readonly HostWindowWatch _hostWindow;
    private bool _betaSubscribed;

    public VideoBackdrop()
    {
        ClipToBounds = true;
        // Created before the handlers below so it has found the window by the time they run.
        _hostWindow = new HostWindowWatch(this, OnHostMinimizedChanged);
        AttachedToVisualTree += (_, _) =>
        {
            SubscribeBeta();
            Refresh();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            UnsubscribeBeta();
            Teardown(allowLinger: true);
        };
    }

    private void SubscribeBeta()
    {
        if (_betaSubscribed) return;
        _betaSubscribed = true;
        BetaFeaturesConfig.Changed += OnBetaConfigChanged;
    }

    private void UnsubscribeBeta()
    {
        if (!_betaSubscribed) return;
        _betaSubscribed = false;
        BetaFeaturesConfig.Changed -= OnBetaConfigChanged;
    }

    private void OnBetaConfigChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(Refresh);

    // Park the decoder while the window can't be seen; resume where it left off.
    private void OnHostMinimizedChanged()
    {
        var paused = _hostWindow.IsMinimized || IsPaused;
        _session?.SetPaused(paused);
        _feed?.UpdatePaused(paused);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == IsActiveProperty || change.Property == SyncLoopWithTrackProperty)
        {
            var sourceUnchanged = change.Property != SourceProperty;
            Refresh(allowLingerOnDeactivate: sourceUnchanged);
        }
        else if (change.Property == IsPausedProperty)
        {
            var paused = IsPaused || _hostWindow.IsMinimized;
            _session?.SetPaused(paused);
            _feed?.UpdatePaused(paused);
        }
        else if (change.Property == TrackEpochProperty)
        {
            if (SyncLoopWithTrack && SyncPosition is { } pos)
                SyncActiveTarget((long)pos.TotalMilliseconds, force: true);
        }
        else if (change.Property == SyncPositionProperty)
        {
            if (SyncPosition is { } pos)
                SyncActiveTarget((long)pos.TotalMilliseconds, force: false);
            else if (change.OldValue != null)
                Refresh(allowLingerOnDeactivate: true); // transitioned from synced to free loop
        }
        else if (change.Property == RateProperty)
        {
            _session?.SetRate(Rate);
            _feed?.UpdateRate(Rate);
        }
        else if (change.Property == StretchProperty || change.Property == CornerRadiusProperty)
            InvalidateVisual();
    }

    private void SyncActiveTarget(long targetMs, bool force)
    {
        var loopWithTrack = SyncLoopWithTrack;
        var smooth = BetaFeaturesConfig.SmoothVideoSync;
        _session?.SyncTo(targetMs, force, loopWithTrack, smooth, Rate);
        _feed?.SyncTo(targetMs, force, loopWithTrack, smooth, Rate);
    }

    void IVideoFeedConsumer.OnFeedFrame(WriteableBitmap frame, bool hasVideoStream)
    {
        if (!ReferenceEquals(frame, _bridgeFrame))
        {
            var oldBridge = _bridgeFrame;
            _bridgeFrame = null;
            if (oldBridge != null)
                Dispatcher.UIThread.Post(oldBridge.Dispose);
        }
        _current = frame;
        _currentDrawn = false;
        HasVideoStream = hasVideoStream;
        InvalidateVisual();
    }

    void IVideoFeedConsumer.OnFeedNoVideoStream()
    {
        HasVideoStream = false;
    }

    public override void Render(DrawingContext context)
    {
        var frame = _current;
        if (frame == null) return;
        _currentDrawn = true;
        // UniformToFill: scale the frame to cover the control, centred, clipped by bounds.
        var bounds = Bounds;
        var src = frame.Size;
        if (src.Width <= 0 || src.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
        var scale = Stretch == Stretch.Uniform
            ? Math.Min(bounds.Width / src.Width, bounds.Height / src.Height)
            : Math.Max(bounds.Width / src.Width, bounds.Height / src.Height);
        var w = src.Width * scale;
        var h = src.Height * scale;
        var dest = new Rect((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
        var radius = CornerRadius;
        if (radius > 0)
        {
            var clipRect = Stretch == Stretch.Uniform ? dest : new Rect(bounds.Size);
            using (context.PushClip(new RoundedRect(clipRect, radius)))
                context.DrawImage(frame, new Rect(src), dest);
        }
        else
        {
            context.DrawImage(frame, new Rect(src), dest);
        }
    }

    private void Refresh(bool allowLingerOnDeactivate = false)
    {
        var source = Source;
        var active = IsActive && !string.IsNullOrEmpty(source) && File.Exists(source)
                     && this.IsAttachedToVisualTree();
        var sameSource = !string.IsNullOrEmpty(source) && string.Equals(source, _activeSource, StringComparison.OrdinalIgnoreCase);
        Teardown(allowLinger: allowLingerOnDeactivate && sameSource);
        if (!active || string.IsNullOrEmpty(source))
        {
            _activeSource = null;
            return;
        }
        _activeSource = source;

        var startPaused = _hostWindow.IsMinimized || IsPaused;
        var syncPos = SyncPosition;
        var synced = syncPos != null;
        var loopWithTrack = SyncLoopWithTrack;
        var loopRepeat = !synced || loopWithTrack;
        var rate = Rate;
        var maxSide = MaxDecodeSide;

        // Route through VideoFeed when stream sharing is active or a matching preloaded standby is ready.
        if (BetaFeaturesConfig.ShareVideoStream ||
            (BetaFeaturesConfig.PreloadNextVideo && VideoFeed.HasPreloaded(source)))
        {
            _feed = VideoFeed.Acquire(
                source,
                this,
                maxSide,
                loopRepeat,
                startPaused,
                rate,
                syncPos,
                loopWithTrack,
                BetaFeaturesConfig.SmoothVideoSync);
            return;
        }

        var generation = _generation;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            VideoDecodeSession? session = null;
            var hasVideo = false;
            try
            {
                // Probe the clip's dimensions first so the buffer keeps its aspect ratio.
                using var probe = new Media(SharedLibVlc.Instance, source, FromType.FromPath);
                probe.Parse(MediaParseOptions.ParseLocal, timeout: 3000).GetAwaiter().GetResult();
                int vw = 0, vh = 0;
                foreach (var track in probe.Tracks)
                {
                    if (track.TrackType != TrackType.Video) continue;
                    hasVideo = true;
                    vw = (int)track.Data.Video.Width;
                    vh = (int)track.Data.Video.Height;
                    break;
                }
                var (bw, bh) = FitBuffer(vw, vh, maxSide);
                session = new VideoDecodeSession(
                    bw,
                    bh,
                    probe.Duration,
                    bitmap =>
                    {
                        _current = bitmap;
                        _currentDrawn = false;
                        HasVideoStream = true;
                        InvalidateVisual();
                    },
                    () => ReferenceEquals(_session, session),
                    () => _current == null || _currentDrawn);
            }
            catch
            {
                // LibVLC unavailable or unreadable clip — leave the artwork backdrop
                Dispatcher.UIThread.Post(() => { if (generation == _generation) HasVideoStream = false; });
                return;
            }

            try
            {
                var options = new List<string> { ":no-audio" };
                if (loopRepeat) options.Add(":input-repeat=65535");
                if (BetaFeaturesConfig.SmartAnimatedCoverThreads || BetaFeaturesConfig.SmoothVideoSync)
                    options.Add($":avcodec-threads={BetaFeaturesConfig.RecommendedVideoDecoderThreads()}");
                using var media = new Media(SharedLibVlc.Instance, source, FromType.FromPath, options.ToArray());
                session.Player.Play(media);
                if (synced) session.SetRate(rate);
                if (startPaused) session.SetPaused(true);
                DebugLogger.Info(DebugLogger.Category.Playback, "Backdrop.Play", $"src={Path.GetFileName(source)}");
            }
            catch
            {
                session.ShutDown();
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (generation != _generation)
                {
                    session.ShutDown(); // restarted or torn down while starting up
                    return;
                }
                _session = session;
                HasVideoStream = hasVideo;
                // Line the clip up with the song as it is NOW. The source switches while the
                // track changes, before the song's position resets, so a position captured at
                // start-up could still be the previous song's.
                if (SyncPosition is { } now)
                {
                    if (Rate != rate) session.SetRate(Rate); // speed moved while starting up
                    session.SyncTo((long)now.TotalMilliseconds, force: true, SyncLoopWithTrack, BetaFeaturesConfig.SmoothVideoSync, Rate);
                }
            });
        });
    }

    private void Teardown(bool allowLinger = false)
    {
        _generation++;
        var session = _session;
        var feed = _feed;
        var bridge = _bridgeFrame;
        _session = null;
        _feed = null;
        _bridgeFrame = null;
        _current = null;
        session?.ShutDown();
        feed?.Release(this, linger: allowLinger);
        if (bridge != null)
            Dispatcher.UIThread.Post(bridge.Dispose);
        InvalidateVisual();
    }
}
