using System.IO;
using HidSharp;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Infrastructure.Hid;

internal sealed class HidSharpConnection : IHidConnection
{
    private readonly HidDevice _device;
    private readonly HidStream _stream;

    public HidSharpConnection(
        HidDevice device,
        HidStream stream,
        DeviceCandidate candidate)
    {
        _device = device;
        _stream = stream;
        Candidate = candidate;
    }

    public DeviceCandidate Candidate { get; }

    public bool IsOpen => _stream.CanRead && _stream.CanWrite;

    public Task WriteAsync(ReadOnlyMemory<byte> report, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stream.Write(report.ToArray());
        return Task.CompletedTask;
    }

    public Task<byte[]?> ReadAsync(TimeSpan timeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stream.ReadTimeout = Math.Max(1, (int)timeout.TotalMilliseconds);

        var length = Math.Max(_device.GetMaxInputReportLength(), 64);
        var buffer = new byte[length];
        try
        {
            var count = _stream.Read(buffer, 0, buffer.Length);
            return Task.FromResult<byte[]?>(count > 0 ? buffer[..count] : null);
        }
        catch (TimeoutException)
        {
            return Task.FromResult<byte[]?>(null);
        }
        catch (IOException)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }

    public Task<byte[]> GetFeatureAsync(
        byte reportId,
        int length,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var buffer = new byte[Math.Max(length, 1)];
        buffer[0] = reportId;
        _stream.GetFeature(buffer);
        return Task.FromResult(buffer);
    }

    public Task SetFeatureAsync(
        ReadOnlyMemory<byte> report,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stream.SetFeature(report.ToArray());
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
