using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Noctis.Services;

namespace Noctis.Controls;

/// <summary>
/// Single LibVLC software video decode session writing RV32 frames into rotating
/// <see cref="WriteableBitmap"/> instances. Shared by <see cref="VideoBackdrop"/> and
/// <see cref="VideoFeed"/> so playback, standby preloading, sync convergence, and buffer
/// pooling live in one place.
/// </summary>
internal sealed class VideoDecodeSession
{
    public readonly MediaPlayer Player;

    private readonly Action<WriteableBitmap> _onFrame;
    private readonly Func<bool> _isOwnerAlive;
    private readonly Func<bool> _canEmitFrame;
    private readonly Action? _onFirstStandbyFrame;

    private readonly int _stride;
    private readonly int _bufferBytes;
    private readonly IntPtr _buffer;
    private readonly byte[] _scratch;
    private readonly bool _pooledScratch;
    private readonly WriteableBitmap[] _bitmaps = new WriteableBitmap[3];
    private int _nextBitmap;
    private volatile bool _framePending;
    private volatile bool _dead;
    private volatile bool _paused;
    private int _standbyParkArmed;
    private long _lengthMs;

    private long _syncSerial;
    private long _syncTargetMs;
    private int _syncForce;
    private int _syncLoopWithTrack;
    private int _syncSmooth;
    private long _syncBaseRateBits = BitConverter.DoubleToInt64Bits(1.0);
    private int _syncWorkerActive;

    // Delegates must stay alive for the player's lifetime (VLC holds raw function pointers).
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;

    public long LengthMs => Volatile.Read(ref _lengthMs);

    public VideoDecodeSession(
        int width,
        int height,
        long lengthMs,
        Action<WriteableBitmap> onFrame,
        Func<bool> isOwnerAlive,
        Func<bool>? canEmitFrame = null,
        Action? onFirstStandbyFrame = null)
    {
        _lengthMs = lengthMs;
        _onFrame = onFrame;
        _isOwnerAlive = isOwnerAlive;
        _canEmitFrame = canEmitFrame ?? (static () => true);
        _onFirstStandbyFrame = onFirstStandbyFrame;

        _stride = width * 4;
        _bufferBytes = _stride * height;
        _buffer = Marshal.AllocHGlobal(_bufferBytes);
        _pooledScratch = BetaFeaturesConfig.LowOverheadTrackSwitch;
        _scratch = _pooledScratch ? ArrayPool<byte>.Shared.Rent(_bufferBytes) : new byte[_bufferBytes];

        for (var i = 0; i < _bitmaps.Length; i++)
            _bitmaps[i] = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Opaque);

        _lockCb = OnLock;
        _displayCb = OnDisplay;
        Player = new MediaPlayer(SharedLibVlc.Instance) { EnableHardwareDecoding = false, Mute = true };
        Player.SetVideoFormat("RV32", (uint)width, (uint)height, (uint)_stride);
        Player.SetVideoCallbacks(_lockCb, null, _displayCb);
    }

    public void ArmStandbyPark(bool armed) => Interlocked.Exchange(ref _standbyParkArmed, armed ? 1 : 0);

    public bool CompleteStandbyPark() => Interlocked.Exchange(ref _standbyParkArmed, 0) == 1;

    public void SetPaused(bool paused)
    {
        if (_dead) return;
        _paused = paused;
        ThreadPool.QueueUserWorkItem(_ => { try { Player.SetPause(paused); } catch { } });
    }

    public void SetRate(double rate)
    {
        if (_dead || double.IsNaN(rate) || rate <= 0) return;
        var r = (float)Math.Clamp(rate, 0.25, 4.0);
        ThreadPool.QueueUserWorkItem(_ => { try { Player.SetRate(r); } catch { } });
    }

    public void SyncTo(long targetMs, bool force = false, bool loopWithTrack = false, bool smoothSync = false, double baseRate = 1.0)
    {
        if (_dead) return;
        Interlocked.Exchange(ref _syncTargetMs, targetMs);
        if (force) Interlocked.Exchange(ref _syncForce, 1);
        Interlocked.Exchange(ref _syncLoopWithTrack, loopWithTrack ? 1 : 0);
        Interlocked.Exchange(ref _syncSmooth, smoothSync ? 1 : 0);
        Interlocked.Exchange(ref _syncBaseRateBits, BitConverter.DoubleToInt64Bits(baseRate));
        Interlocked.Increment(ref _syncSerial);

        if (Interlocked.CompareExchange(ref _syncWorkerActive, 1, 0) != 0) return;
        ThreadPool.QueueUserWorkItem(_ => DrainSyncQueue());
    }

    private void DrainSyncQueue()
    {
        try
        {
            while (!_dead)
            {
                var serial = Volatile.Read(ref _syncSerial);
                var reqTarget = Interlocked.Read(ref _syncTargetMs);
                var reqForce = Interlocked.Exchange(ref _syncForce, 0) == 1;
                var reqLoop = Volatile.Read(ref _syncLoopWithTrack) == 1;
                var reqSmooth = Volatile.Read(ref _syncSmooth) == 1;
                var reqRate = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _syncBaseRateBits));

                var length = Player.Length;
                if (length > 0) Volatile.Write(ref _lengthMs, length);

                var decision = VideoBackdrop.EvaluateSync(
                    Player.State == VLCState.Ended,
                    Player.Time,
                    reqTarget,
                    LengthMs,
                    reqForce,
                    reqLoop,
                    reqSmooth,
                    _paused);

                if (reqSmooth)
                {
                    var desiredRate = (float)Math.Clamp(reqRate * decision.RateMultiplier, 0.25, 4.0);
                    if (Math.Abs(Player.Rate - desiredRate) > 0.005f)
                        Player.SetRate(desiredRate);
                }

                switch (decision.Action)
                {
                    case VideoBackdrop.SyncAction.Seek:
                        Player.Time = decision.TargetMs;
                        break;
                    case VideoBackdrop.SyncAction.Restart:
                        Player.Stop();
                        Player.Play();
                        Player.Time = decision.TargetMs;
                        if (_paused) Player.SetPause(true);
                        break;
                }

                if (Volatile.Read(ref _syncSerial) == serial)
                    break;
            }
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _syncWorkerActive, 0);
            if (!_dead && Volatile.Read(ref _syncForce) == 1 && Interlocked.CompareExchange(ref _syncWorkerActive, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(_ => DrainSyncQueue());
        }
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _buffer);
        return _buffer;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        if (_framePending || _dead) return;
        _framePending = true;
        Marshal.Copy(_buffer, _scratch, 0, _bufferBytes);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_dead || !_isOwnerAlive() || !_canEmitFrame()) return;
                var bitmap = _bitmaps[_nextBitmap];
                _nextBitmap = (_nextBitmap + 1) % _bitmaps.Length;
                using (var fb = bitmap.Lock())
                    Marshal.Copy(_scratch, 0, fb.Address, _bufferBytes);
                _onFrame(bitmap);
                if (CompleteStandbyPark())
                {
                    _onFirstStandbyFrame?.Invoke();
                    SetPaused(true);
                }
            }
            finally { _framePending = false; }
        }, DispatcherPriority.Render);
    }

    public void ShutDown()
    {
        if (_dead) return;
        _dead = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { Player.Stop(); } catch { }
#pragma warning disable CS8625
            try { Player.SetVideoCallbacks(null, null, null); } catch { }
#pragma warning restore CS8625
            try { Player.Dispose(); } catch { }
            Marshal.FreeHGlobal(_buffer);
            if (_pooledScratch)
                ArrayPool<byte>.Shared.Return(_scratch);
            Dispatcher.UIThread.Post(() => { foreach (var b in _bitmaps) b.Dispose(); });
        });
    }
}
