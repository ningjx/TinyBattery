using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.State;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Core.Monitoring;

internal sealed class DeviceSession : IAsyncDisposable
{
    private readonly IDeviceTransport _transport;
    private readonly IProtocolRegistry _protocols;
    private readonly IBatteryStateStore _state;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private readonly Dictionary<string, ProtocolReading> _lastReadings =
        new(StringComparer.OrdinalIgnoreCase);

    private Task? _loop;
    private bool _placeholder;
    private int _failureCount;

    public DeviceSession(
        IDeviceTransport transport,
        DeviceCandidate candidate,
        DeviceDefinition definition,
        IProtocolRegistry protocols,
        IBatteryStateStore state,
        ILogger logger)
    {
        _transport = transport;
        Candidate = candidate;
        Definition = definition;
        _protocols = protocols;
        _state = state;
        _logger = logger;
    }

    public DeviceCandidate Candidate { get; }

    public DeviceDefinition Definition { get; }

    public string StableId => Candidate.StableId;

    public string DefinitionId => Definition.Id;

    public void Start()
    {
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void RequestRefresh()
    {
        try
        {
            if (_refreshSignal.CurrentCount == 0)
            {
                _refreshSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_placeholder)
        {
            _state.Remove(Candidate.StableId);
            _placeholder = false;
        }

        foreach (var logicalId in _lastReadings.Keys)
        {
            _state.Remove(logicalId);
        }

        _lastReadings.Clear();
        _cts.Dispose();
        _refreshSignal.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<ProtocolReading> readings;
                await using (var connection = await _transport.OpenAsync(Candidate, ct))
                {
                    var protocol = _protocols.Resolve(Definition.Protocol.Kind);
                    readings = await protocol.ReadAsync(
                        new ProtocolContext(Candidate, connection, Definition),
                        ct);
                }

                ApplyReadings(readings);
                _failureCount = 0;
                DrainRefresh();
                await DelayAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ApplyFailure(exception);
                _failureCount = Math.Min(_failureCount + 1, 8);
                var backoffSeconds = Math.Min(
                    30,
                    5 * Math.Pow(2, _failureCount - 1));
                _logger.LogWarning(
                    exception,
                    "Battery read failed for '{Device}' ({Definition}); retrying in {Backoff}s.",
                    Definition.DisplayName,
                    Definition.Id,
                    backoffSeconds);

                try
                {
                    await _refreshSignal.WaitAsync(
                        TimeSpan.FromSeconds(backoffSeconds),
                        ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private void ApplyReadings(IReadOnlyList<ProtocolReading> readings)
    {
        if (_placeholder)
        {
            _state.Remove(Candidate.StableId);
            _placeholder = false;
        }

        foreach (var reading in readings)
        {
            _lastReadings[reading.LogicalDeviceId] = reading;
            _state.Update(new DeviceSnapshot(
                reading.LogicalDeviceId,
                reading.DisplayName,
                DeviceConnectionState.Connected,
                reading.Battery,
                reading.Battery.Timestamp,
                null,
                IsStale: false));
        }

        _logger.LogInformation(
            "Battery update for '{Device}': {Readings}.",
            Definition.DisplayName,
            string.Join(
                ", ",
                readings.Select(reading =>
                    $"{reading.Battery.Percent?.ToString() ?? "?"}%"
                    + (reading.Battery.VoltageMv is { } voltage
                        ? $" ({voltage:0} mV)"
                        : string.Empty))));
    }

    private void ApplyFailure(Exception exception)
    {
        var state = Classify(exception);
        if (_lastReadings.Count == 0)
        {
            _placeholder = true;
            _state.Update(new DeviceSnapshot(
                Candidate.StableId,
                Definition.DisplayName,
                state,
                Battery: null,
                LastSuccess: null,
                LastError: exception.Message,
                IsStale: false));
            return;
        }

        foreach (var (logicalId, reading) in _lastReadings)
        {
            _state.Update(new DeviceSnapshot(
                logicalId,
                reading.DisplayName,
                state,
                reading.Battery,
                reading.Battery.Timestamp,
                exception.Message,
                IsStale: true));
        }
    }

    private async Task DelayAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(5, Definition.Polling.IntervalSeconds));
        var jitterSeconds = Math.Max(0, Definition.Polling.JitterSeconds);
        var jitter = jitterSeconds == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Random.Shared.Next(0, jitterSeconds + 1));
        await _refreshSignal.WaitAsync(interval + jitter, ct);
    }

    private void DrainRefresh()
    {
        try
        {
            while (_refreshSignal.Wait(0))
            {
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static DeviceConnectionState Classify(Exception exception) => exception switch
    {
        UnauthorizedAccessException => DeviceConnectionState.AccessDenied,
        IOException => DeviceConnectionState.Unreachable,
        ProtocolException => DeviceConnectionState.Asleep,
        _ => DeviceConnectionState.Faulted,
    };
}
