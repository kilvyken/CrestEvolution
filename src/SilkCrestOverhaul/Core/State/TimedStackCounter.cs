using System;
using SilkCrestOverhaul.Core.Time;

namespace SilkCrestOverhaul.Core.State;

public sealed class TimedStackCounter
{
    private readonly IClock _clock;
    private readonly int _maxStacks;
    private readonly double _durationSeconds;
    private double _expiresAt;

    public TimedStackCounter(int maxStacks, double durationSeconds, IClock clock)
    {
        if (maxStacks <= 0) throw new ArgumentOutOfRangeException(nameof(maxStacks));
        if (durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _maxStacks = maxStacks;
        _durationSeconds = durationSeconds;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public int Count { get; private set; }
    public bool IsActive => Count > 0 && RemainingSeconds > 0;
    public double RemainingSeconds => Count == 0 ? 0 : Math.Max(0, _expiresAt - _clock.Now);

    public event EventHandler<StackChangedEventArgs>? Changed;
    public event EventHandler<ThresholdReachedEventArgs>? ThresholdReached;
    public event EventHandler? Expired;

    public int Add(int amount = 1, bool refreshDuration = true, params int[] thresholds)
    {
        if (amount <= 0) return Count;
        Tick();
        var previous = Count;
        Count = Math.Min(_maxStacks, Count + amount);
        if (refreshDuration && Count > 0) _expiresAt = _clock.Now + _durationSeconds;
        RaiseThresholds(previous, Count, thresholds);
        RaiseChanged(previous);
        return Count;
    }

    public void Set(int value, bool refreshDuration = true, params int[] thresholds)
    {
        Tick();
        var previous = Count;
        Count = Math.Max(0, Math.Min(_maxStacks, value));
        if (refreshDuration && Count > 0) _expiresAt = _clock.Now + _durationSeconds;
        if (Count == 0) _expiresAt = 0;
        RaiseThresholds(previous, Count, thresholds);
        RaiseChanged(previous);
    }

    public void Refresh()
    {
        Tick();
        if (Count == 0) return;
        _expiresAt = _clock.Now + _durationSeconds;
        RaiseChanged(Count);
    }

    public void Clear()
    {
        if (Count == 0) return;
        var previous = Count;
        Count = 0;
        _expiresAt = 0;
        RaiseChanged(previous);
    }

    public void Tick()
    {
        if (Count == 0 || _clock.Now < _expiresAt) return;
        var previous = Count;
        Count = 0;
        _expiresAt = 0;
        RaiseChanged(previous);
        Expired?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseThresholds(int previous, int current, int[] thresholds)
    {
        if (current <= previous) return;
        foreach (var threshold in thresholds)
        {
            if (previous < threshold && current >= threshold)
                ThresholdReached?.Invoke(this, new ThresholdReachedEventArgs(threshold));
        }
    }

    private void RaiseChanged(int previous) =>
        Changed?.Invoke(this, new StackChangedEventArgs(previous, Count, RemainingSeconds));
}

public sealed record StackChangedEventArgs(int Previous, int Current, double RemainingSeconds);
public sealed record ThresholdReachedEventArgs(int Threshold);
