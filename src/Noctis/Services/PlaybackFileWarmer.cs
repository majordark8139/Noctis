using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Noctis.Services;

/// <summary>
/// Reads the playing song's file once, in the background, so the operating system holds it in
/// its file cache. A seek into a part of the file that was never read otherwise waits on the
/// disk: the demuxer probes several places to find the frame, and on a hard disk each probe is
/// a head move. Measured 11-19 on a hard-disk library (official 1.7.0, 54 timeline clicks in one
/// song): audio back in 5 ms when the spot was already read, 100-722 ms in 8 of them; a cold
/// file seeked from the test harness went silent for 315 ms. One sequential read of the file
/// costs the disk less than a second and makes every later seek in that song a memory read.
/// </summary>
internal sealed class PlaybackFileWarmer : IDisposable
{
    /// <summary>
    /// The read starts with the song. It was 1.5 s (the song's own first read not made to share
    /// the disk), but a seek in that first second still waited on the disk. Harness 11-20, cold
    /// hard-disk FLACs, 60-107 MB, seek 0.2-2 s after Play: 1.5 s delay 4-343 ms (median 84, n=15),
    /// read at once 3-25 ms (median 5, n=15); start of playback (Play to first audio) median 356
    /// against 336 ms (n=18 each, no cost measured).
    /// </summary>
    internal const int StartDelayMs = 0;

    /// <summary>Longer files (audiobooks, DSD albums in one file) are left alone: reading
    /// gigabytes to save a seek would push other things out of the cache.</summary>
    internal const long MaxBytes = 768L * 1024 * 1024;
    internal const long GentleLowRamMaxBytes = 192L * 1024 * 1024;
    internal const long LowRamThresholdBytes = 6L * 1024 * 1024 * 1024;
    internal const int GentleDefaultDelayMs = 1000;
    internal const int GentleChunkPauseMs = 20;

    private const int ChunkBytes = 1 << 20;
    private const int ThreadModeBackgroundBegin = 0x00010000;
    private const int ThreadModeBackgroundEnd = 0x00020000;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _skipDelay; // cancelled by StartNow: the pass reads at once
    private string? _path;
    private readonly int _startDelayMs;
    private readonly bool? _gentleOverride;

    internal PlaybackFileWarmer(int startDelayMs = StartDelayMs, bool? gentleOverride = null)
    {
        _startDelayMs = startDelayMs;
        _gentleOverride = gentleOverride;
    }

    private bool IsGentleMode => _gentleOverride ?? BetaFeaturesConfig.GentleHddFileCaching;

    internal static long EffectiveMaxBytes(bool gentle, long totalAvailableMemoryBytes)
    {
        if (!gentle || totalAvailableMemoryBytes <= 0) return MaxBytes;
        return totalAvailableMemoryBytes <= LowRamThresholdBytes ? GentleLowRamMaxBytes : MaxBytes;
    }

    internal static int EffectiveStartDelayMs(int configuredDelayMs, bool gentle) =>
        gentle && configuredDelayMs <= 0 ? GentleDefaultDelayMs : configuredDelayMs;

    /// <summary>The read of the last file given to <see cref="Warm"/>; completed when nothing runs. For tests.</summary>
    internal Task Current { get; private set; } = Task.CompletedTask;

    /// <summary>Bytes read by the last finished or running pass. For tests.</summary>
    internal long BytesRead => Interlocked.Read(ref _bytesRead);
    private long _bytesRead;

    /// <summary>
    /// Starts reading <paramref name="path"/> (a local file) after a short delay and drops the
    /// read of any other file. The same path again changes nothing, so a restart or a seek that
    /// reopens the song does not read it twice. Null or a path that is not a file stops it.
    /// </summary>
    internal void Warm(string? path)
    {
        lock (_gate)
        {
            if (path != null && SamePath(path, _path)) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _skipDelay = null;
            _path = path;
            if (string.IsNullOrEmpty(path)) { Current = Task.CompletedTask; return; }
            var cts = _cts = new CancellationTokenSource();
            var skip = _skipDelay = new CancellationTokenSource();
            // Tokens taken here: a Warm right after this one disposes the source before the pass runs.
            var token = cts.Token;
            var skipToken = skip.Token;
            Current = Task.Run(() => ReadThrough(path, token, skipToken));
        }
    }

    /// <summary>Whether <paramref name="path"/> is the file this warmer reads or has read.</summary>
    internal bool IsWarming(string path)
    {
        lock (_gate) return SamePath(path, _path);
    }

    private static bool SamePath(string a, string? b)
    {
        if (b == null) return false;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>
    /// A seek in the song being warmed: start its read now instead of after the start delay.
    /// A far seek right after a cold start otherwise waits on the disk (harness 11-19, hard
    /// disk: 70-354 ms at 0.7-1.3 s into the song against 6-8 ms once the file was read).
    /// Nothing waiting (already read, or no file): no effect.
    /// </summary>
    internal void StartNow()
    {
        lock (_gate)
        {
            try { _skipDelay?.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task ReadThrough(string path, CancellationToken ct, CancellationToken skipDelay)
    {
        var gentle = IsGentleMode;
        var delayMs = EffectiveStartDelayMs(_startDelayMs, gentle);
        try
        {
            Interlocked.Exchange(ref _bytesRead, 0);
            if (delayMs > 0)
            {
                try
                {
                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct, skipDelay);
                    await Task.Delay(delayMs, wait.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* StartNow */ }
            }
            ct.ThrowIfCancellationRequested();

            var info = new FileInfo(path);
            var maxBytes = EffectiveMaxBytes(gentle, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
            if (!info.Exists || info.Length > maxBytes) return;

            if (gentle)
            {
                // Dedicated non-pooled thread so Windows THREAD_MODE_BACKGROUND_BEGIN/END
                // stays strictly scoped to this read loop and never leaks onto the ThreadPool.
                await Task.Factory.StartNew(
                    () => ReadThroughGentleSync(path, skipDelay, ct),
                    ct,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).ConfigureAwait(false);
                return;
            }

            var started = Stopwatch.GetTimestamp();
            // Shared for everything: tag edits, moves and deletes of the playing file go on working.
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 1,
                FileOptions.SequentialScan | FileOptions.Asynchronous);
            var pooled = BetaFeaturesConfig.LowOverheadTrackSwitch
                ? ArrayPool<byte>.Shared.Rent(ChunkBytes)
                : null;
            try
            {
                var buffer = pooled ?? new byte[ChunkBytes];
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, ChunkBytes), ct).ConfigureAwait(false)) > 0)
                    Interlocked.Add(ref _bytesRead, read);
            }
            finally
            {
                if (pooled != null)
                    ArrayPool<byte>.Shared.Return(pooled);
            }

            DebugLogger.Info(DebugLogger.Category.Playback, "FileWarm.Done",
                $"file={Path.GetFileName(path)}, mb={BytesRead / (1024 * 1024)}, ms={Noctis.Helpers.Stopwatch.GetElapsedTime(started).TotalMilliseconds:0}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A file that cannot be read this way plays as before; nothing to report.
        }
    }

    private void ReadThroughGentleSync(string path, CancellationToken skipDelay, CancellationToken ct)
    {
        var enteredBackgroundIo = false;
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                try { enteredBackgroundIo = SetThreadPriority(GetCurrentThread(), ThreadModeBackgroundBegin); }
                catch { enteredBackgroundIo = false; }
            }

            var started = Stopwatch.GetTimestamp();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 1,
                FileOptions.SequentialScan);
            using var pauseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, skipDelay);

            int read;
            while (!ct.IsCancellationRequested && (read = stream.Read(buffer, 0, ChunkBytes)) > 0)
            {
                Interlocked.Add(ref _bytesRead, read);
                if (!skipDelay.IsCancellationRequested)
                    pauseTokenSource.Token.WaitHandle.WaitOne(GentleChunkPauseMs);
            }

            DebugLogger.Info(DebugLogger.Category.Playback, "FileWarm.Done",
                $"file={Path.GetFileName(path)}, mb={BytesRead / (1024 * 1024)}, ms={Noctis.Helpers.Stopwatch.GetElapsedTime(started).TotalMilliseconds:0}, gentle=1");
        }
        finally
        {
            if (enteredBackgroundIo && OperatingSystem.IsWindows())
            {
                try { SetThreadPriority(GetCurrentThread(), ThreadModeBackgroundEnd); }
                catch { }
            }
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _skipDelay = null;
            _path = null;
        }
    }
}
