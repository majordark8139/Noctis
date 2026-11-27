using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Noctis.Services;

namespace Noctis.Controls;

/// <summary>Consumer of a shared/preloaded <see cref="VideoFeed"/>.</summary>
internal interface IVideoFeedConsumer
{
    void OnFeedFrame(WriteableBitmap frame, bool hasVideoStream);
    void OnFeedNoVideoStream();
}

/// <summary>
/// Shared, pre-warmable video decoder pool for <see cref="VideoBackdrop"/> controls.
/// Mirrors <see cref="AnimatedCoverFeed"/> so multiple placements of the same clip
/// (Cover, Behind Lyrics, Full-Screen) share decoded frames without reopening from disk,
/// and next-track videos can be pre-probed and decoded up to frame #1 paused ahead of handoff.
/// </summary>
internal sealed class VideoFeed
{
    private static readonly TimeSpan LingerAfterLastLease = TimeSpan.FromMilliseconds(300);
    private const int MaxPreloadedEntries = 2;

    private sealed record PreloadedEntry(VideoFeed Feed, long StartMs);

    private static readonly List<VideoFeed> s_activeFeeds = new();
    private static readonly List<PreloadedEntry> s_preloaded = new();

    private readonly List<IVideoFeedConsumer> _consumers = new();
    private CancellationTokenSource? _lingerCts;
    private VideoDecodeSession? _session;
    private WriteableBitmap? _currentFrame;
    private bool _hasVideoStream = true;
    private bool _closed;
    private volatile bool _isStandbyPreload;

    public string Source { get; }
    public int MaxDecodeSide { get; }
    public bool LoopRepeat { get; }

    internal static int ActiveFeedCount => s_activeFeeds.Count;
    internal static string? PreloadedSource => s_preloaded.Count > 0 ? s_preloaded[^1].Feed.Source : null;

    private VideoFeed(string source, int maxDecodeSide, bool loopRepeat, bool isStandbyPreload)
    {
        Source = source;
        MaxDecodeSide = maxDecodeSide;
        LoopRepeat = loopRepeat;
        _isStandbyPreload = isStandbyPreload;
    }

    /// <summary>
    /// Pre-probes and starts a standby paused feed for <paramref name="source"/> holding its first
    /// decoded frame ready at <paramref name="startPositionMs"/> before the track transitions.
    /// UI thread only.
    /// </summary>
    public static void Preload(string? source, int maxDecodeSide, long startPositionMs, bool loopRepeat)
    {
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            return;

        if (maxDecodeSide < 2) maxDecodeSide = VideoBackdrop.MaxLongSide;
        startPositionMs = Math.Max(0, startPositionMs);

        for (var i = s_preloaded.Count - 1; i >= 0; i--)
        {
            var entry = s_preloaded[i];
            if (entry.Feed._closed)
            {
                s_preloaded.RemoveAt(i);
                continue;
            }
            if (string.Equals(entry.Feed.Source, source, StringComparison.OrdinalIgnoreCase) &&
                entry.Feed.MaxDecodeSide >= maxDecodeSide &&
                Math.Abs(entry.StartMs - startPositionMs) <= 250)
            {
                return;
            }
            if (string.Equals(entry.Feed.Source, source, StringComparison.OrdinalIgnoreCase))
            {
                s_preloaded.RemoveAt(i);
                entry.Feed.Close();
            }
        }

        if (FindReusableActiveFeed(source, maxDecodeSide) != null)
            return;

        while (s_preloaded.Count >= MaxPreloadedEntries)
        {
            var oldest = s_preloaded[0].Feed;
            s_preloaded.RemoveAt(0);
            oldest.Close();
        }

        var feed = new VideoFeed(source, maxDecodeSide, loopRepeat, isStandbyPreload: true);
        s_preloaded.Add(new PreloadedEntry(feed, startPositionMs));
        feed.StartSession(startPaused: true, rate: 1.0, initialSyncMs: startPositionMs, loopWithTrack: loopRepeat);
    }

    /// <summary>Cancels and disposes any staged next-track videos that have not been claimed.</summary>
    public static void CancelPreload()
    {
        if (s_preloaded.Count == 0) return;
        var snapshot = s_preloaded.ToArray();
        s_preloaded.Clear();
        foreach (var entry in snapshot)
            entry.Feed.Close();
    }

    /// <summary>Drops staged preloads except those matching the active track's paths.</summary>
    public static void CancelPreloadExcept(string? keepPath1, string? keepPath2)
    {
        for (var i = s_preloaded.Count - 1; i >= 0; i--)
        {
            var feed = s_preloaded[i].Feed;
            var keep1 = !string.IsNullOrEmpty(keepPath1) && string.Equals(feed.Source, keepPath1, StringComparison.OrdinalIgnoreCase);
            var keep2 = !string.IsNullOrEmpty(keepPath2) && string.Equals(feed.Source, keepPath2, StringComparison.OrdinalIgnoreCase);
            if (!keep1 && !keep2)
            {
                s_preloaded.RemoveAt(i);
                feed.Close();
            }
        }
    }

    internal static bool HasPreloaded(string? source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        for (var i = 0; i < s_preloaded.Count; i++)
        {
            var entry = s_preloaded[i];
            if (!entry.Feed._closed && string.Equals(entry.Feed.Source, source, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Acquires or promotes a <see cref="VideoFeed"/> for <paramref name="consumer"/>.
    /// UI thread only.
    /// </summary>
    public static VideoFeed Acquire(
        string source,
        IVideoFeedConsumer consumer,
        int maxDecodeSide,
        bool loopRepeat,
        bool startPaused,
        double rate,
        TimeSpan? syncPosition,
        bool loopWithTrack,
        bool smoothSync)
    {
        if (maxDecodeSide < 2) maxDecodeSide = VideoBackdrop.MaxLongSide;

        var feed = BetaFeaturesConfig.ShareVideoStream ? FindReusableActiveFeed(source, maxDecodeSide) : null;

        WriteableBitmap? bridgeFrame = null;
        if (feed == null)
        {
            for (var i = s_preloaded.Count - 1; i >= 0; i--)
            {
                var preloaded = s_preloaded[i].Feed;
                if (preloaded._closed)
                {
                    s_preloaded.RemoveAt(i);
                    continue;
                }
                if (!string.Equals(preloaded.Source, source, StringComparison.OrdinalIgnoreCase))
                    continue;

                s_preloaded.RemoveAt(i);
                if (preloaded.MaxDecodeSide >= maxDecodeSide || !BetaFeaturesConfig.ShareVideoStream)
                {
                    preloaded._isStandbyPreload = false;
                    preloaded.CancelLinger();
                    s_activeFeeds.Add(preloaded);
                    feed = preloaded;
                    DebugLogger.Info(DebugLogger.Category.Playback, "VideoFeed.Promote", $"src={Path.GetFileName(source)}, max={preloaded.MaxDecodeSide}");
                }
                else
                {
                    if (preloaded._currentFrame is { } pf)
                        bridgeFrame = CloneBitmap(pf);
                    preloaded.Close();
                }
                break;
            }
        }

        if (feed == null && BetaFeaturesConfig.ShareVideoStream)
        {
            var existingActive = FindAnyActiveFeed(source);
            if (existingActive?._currentFrame is { } srcFrame)
                bridgeFrame = CloneBitmap(srcFrame);
        }

        if (feed == null)
        {
            feed = new VideoFeed(source, maxDecodeSide, loopRepeat, isStandbyPreload: false);
            s_activeFeeds.Add(feed);
            var startMs = syncPosition is { } p ? (long)p.TotalMilliseconds : 0;
            feed.StartSession(startPaused, rate, startMs, loopWithTrack);
        }

        if (!feed._consumers.Contains(consumer))
            feed._consumers.Add(consumer);
        feed.CancelLinger();

        if (feed._currentFrame != null)
            consumer.OnFeedFrame(feed._currentFrame, feed._hasVideoStream);
        else if (bridgeFrame != null)
            consumer.OnFeedFrame(bridgeFrame, true);
        else if (!feed._hasVideoStream)
            consumer.OnFeedNoVideoStream();

        feed.UpdatePaused(startPaused);
        if (syncPosition is { } pos)
        {
            feed.UpdateRate(rate);
            feed.SyncTo((long)pos.TotalMilliseconds, force: true, loopWithTrack, smoothSync, rate);
        }

        return feed;
    }

    public void Release(IVideoFeedConsumer consumer, bool linger)
    {
        _consumers.Remove(consumer);
        if (_consumers.Count > 0 || _closed)
            return;

        if (!linger || !BetaFeaturesConfig.ShareVideoStream)
        {
            Close();
            return;
        }

        CancelLinger();
        var cts = _lingerCts = new CancellationTokenSource();
        var token = cts.Token;
        DispatcherTimer.RunOnce(() =>
        {
            if (!token.IsCancellationRequested && _consumers.Count == 0 && !_closed)
                Close();
        }, LingerAfterLastLease);
    }

    public void UpdatePaused(bool paused)
    {
        if (!_isStandbyPreload)
        {
            if (!paused) _session?.ArmStandbyPark(false);
            _session?.SetPaused(paused);
        }
    }

    public void UpdateRate(double rate) => _session?.SetRate(rate);

    public void SyncTo(long targetMs, bool force, bool loopWithTrack, bool smoothSync, double baseRate)
    {
        if (!_isStandbyPreload)
            _session?.SyncTo(targetMs, force, loopWithTrack, smoothSync, baseRate);
    }

    private static VideoFeed? FindReusableActiveFeed(string source, int minDecodeSide)
    {
        for (var i = s_activeFeeds.Count - 1; i >= 0; i--)
        {
            var f = s_activeFeeds[i];
            if (!f._closed &&
                f.MaxDecodeSide >= minDecodeSide &&
                string.Equals(f.Source, source, StringComparison.OrdinalIgnoreCase))
            {
                return f;
            }
        }
        return null;
    }

    private static VideoFeed? FindAnyActiveFeed(string source)
    {
        for (var i = s_activeFeeds.Count - 1; i >= 0; i--)
        {
            var f = s_activeFeeds[i];
            if (!f._closed && string.Equals(f.Source, source, StringComparison.OrdinalIgnoreCase))
                return f;
        }
        return null;
    }

    private static WriteableBitmap? CloneBitmap(WriteableBitmap source)
    {
        try
        {
            var size = source.PixelSize;
            var copy = new WriteableBitmap(size, source.Dpi, source.Format ?? PixelFormat.Bgra8888, source.AlphaFormat ?? AlphaFormat.Opaque);
            using var srcLock = source.Lock();
            using var dstLock = copy.Lock();
            var bytesPerRow = Math.Min(srcLock.RowBytes, dstLock.RowBytes);
            var rowScratch = ArrayPool<byte>.Shared.Rent(bytesPerRow);
            try
            {
                for (var y = 0; y < size.Height; y++)
                {
                    Marshal.Copy(srcLock.Address + y * srcLock.RowBytes, rowScratch, 0, bytesPerRow);
                    Marshal.Copy(rowScratch, 0, dstLock.Address + y * dstLock.RowBytes, bytesPerRow);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rowScratch);
            }
            return copy;
        }
        catch
        {
            return null;
        }
    }

    private void CancelLinger()
    {
        _lingerCts?.Cancel();
        _lingerCts?.Dispose();
        _lingerCts = null;
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        CancelLinger();
        s_activeFeeds.Remove(this);
        for (var i = s_preloaded.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(s_preloaded[i].Feed, this))
                s_preloaded.RemoveAt(i);
        }
        var session = _session;
        _session = null;
        _currentFrame = null;
        session?.ShutDown();
    }

    private void StartSession(bool startPaused, double rate, long initialSyncMs, bool loopWithTrack)
    {
        var source = Source;
        var maxSide = MaxDecodeSide;
        var loopRepeat = LoopRepeat;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            VideoDecodeSession? session = null;
            var hasVideo = false;
            try
            {
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
                var (bw, bh) = VideoBackdrop.FitBuffer(vw, vh, maxSide);
                session = new VideoDecodeSession(
                    bw,
                    bh,
                    probe.Duration,
                    bitmap => OnSessionFrame(session!, bitmap),
                    () => !_closed && ReferenceEquals(_session, session));
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_closed) return;
                    _hasVideoStream = false;
                    foreach (var c in _consumers.ToArray())
                        c.OnFeedNoVideoStream();
                });
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_closed)
                {
                    session.ShutDown();
                    return;
                }

                _session = session;
                _hasVideoStream = hasVideo;
                if (!hasVideo)
                {
                    foreach (var c in _consumers.ToArray())
                        c.OnFeedNoVideoStream();
                }

                var startArgMs = VideoBackdrop.EffectiveTargetMs(initialSyncMs, session.LengthMs, loopWithTrack);
                var standby = _isStandbyPreload;
                ThreadPool.QueueUserWorkItem(__ =>
                {
                    try
                    {
                        var options = new List<string> { ":no-audio" };
                        if (loopRepeat)
                            options.Add(":input-repeat=65535");
                        if (BetaFeaturesConfig.SmartAnimatedCoverThreads || BetaFeaturesConfig.SmoothVideoSync)
                            options.Add($":avcodec-threads={BetaFeaturesConfig.RecommendedVideoDecoderThreads()}");
                        if (startArgMs > 250)
                            options.Add(string.Create(CultureInfo.InvariantCulture, $":start-time={startArgMs / 1000.0:0.###}"));

                        using var media = new Media(SharedLibVlc.Instance, source, FromType.FromPath, options.ToArray());
                        session.ArmStandbyPark(standby);
                        session.Player.Play(media);
                        if (!standby)
                        {
                            session.SetRate(rate);
                            if (startPaused) session.SetPaused(true);
                        }
                    }
                    catch
                    {
                        Dispatcher.UIThread.Post(Close);
                    }
                });
            });
        });
    }

    private void OnSessionFrame(VideoDecodeSession session, WriteableBitmap bitmap)
    {
        if (_closed || !ReferenceEquals(_session, session))
            return;

        _currentFrame = bitmap;
        foreach (var c in _consumers.ToArray())
            c.OnFeedFrame(bitmap, _hasVideoStream);
    }
}
