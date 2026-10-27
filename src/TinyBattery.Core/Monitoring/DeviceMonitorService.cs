using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Monitoring;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.State;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Core.Monitoring;

public sealed class DeviceMonitorService : IHostedService, IDeviceRefreshCoordinator
{
    private readonly IReadOnlyList<IDeviceTransport> _transports;
    private readonly IDeviceMatcher _matcher;
    private readonly IProtocolRegistry _protocols;
    private readonly IBatteryStateStore _state;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DeviceMonitorService> _logger;
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public DeviceMonitorService(
        IEnumerable<IDeviceTransport> transports,
        IDeviceMatcher matcher,
        IProtocolRegistry protocols,
        IBatteryStateStore state,
        ILoggerFactory loggerFactory,
        ILogger<DeviceMonitorService> logger)
    {
        _transports = transports.ToArray();
        _matcher = matcher;
        _protocols = protocols;
        _state = state;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public void RequestRefresh()
    {
        foreach (var session in _sessions.Values)
        {
            session.RequestRefresh();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }

        _sessions.Clear();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncSessionsAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Device discovery failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncSessionsAsync(CancellationToken ct)
    {
        var discovered = await DiscoverAsync(ct);
        var matched = new Dictionary<string, (IDeviceTransport Transport, DeviceCandidate Candidate, DeviceDefinition Definition)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (transport, candidate) in discovered)
        {
            var match = _matcher.Match(candidate);
            if (match is null)
            {
                continue;
            }

            if (matched.TryGetValue(candidate.StableId, out var existing)
                && existing.Definition.Priority >= match.Definition.Priority)
            {
                continue;
            }

            matched[candidate.StableId] = (transport, candidate, match.Definition);
        }

        foreach (var (stableId, session) in _sessions.ToArray())
        {
            if (matched.TryGetValue(stableId, out var replacement)
                && string.Equals(
                    replacement.Definition.Id,
                    session.DefinitionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _sessions.TryRemove(stableId, out _);
            await session.DisposeAsync();
        }

        foreach (var (stableId, item) in matched)
        {
            if (_sessions.ContainsKey(stableId))
            {
                continue;
            }

            var session = new DeviceSession(
                item.Transport,
                item.Candidate,
                item.Definition,
                _protocols,
                _state,
                _loggerFactory.CreateLogger($"DeviceSession.{item.Definition.Id}"));
            _sessions[stableId] = session;
            session.Start();
            _logger.LogInformation(
                "Tracking '{Device}' via definition '{Definition}'.",
                item.Definition.DisplayName,
                item.Definition.Id);
        }
    }

    private async Task<IReadOnlyList<(IDeviceTransport Transport, DeviceCandidate Candidate)>> DiscoverAsync(
        CancellationToken ct)
    {
        var results = new List<(IDeviceTransport, DeviceCandidate)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var transport in _transports)
        {
            try
            {
                var candidates = await transport.DiscoverAsync(ct);
                foreach (var candidate in candidates)
                {
                    if (seen.Add(candidate.StableId))
                    {
                        results.Add((transport, candidate));
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Transport '{Transport}' discovery failed.",
                    transport.Kind);
            }
        }

        return results;
    }
}
