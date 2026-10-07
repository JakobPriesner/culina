using System.Diagnostics.Metrics;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace IntegrationTests.Pipeline;

/// <summary>
/// What the server says about its machine, so an operator with only telemetry can tell where a line
/// came from and whether memory or disk ran out.
/// </summary>
public class MachineTelemetryTests
{
    [Fact]
    public void ApiTelemetry_ShouldNameTheMachineAndProcess()
    {
        var exported = new Exported();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.AddObservability();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(exported));

        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Api.Anything").LogError("The server broke");

        Assert.Contains("host.name", exported.Attributes);
        Assert.Contains("os.type", exported.Attributes);
        Assert.Contains("process.pid", exported.Attributes);
        Assert.Contains("process.runtime.name", exported.Attributes);
    }

    [Fact]
    public void MachineMetrics_ShouldReportMemoryAndTheRoomLeftOnEachVolume()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var storage = new StorageSettings
        {
            ImagePath = directory,
            DataProtectionKeyPath = directory,
            ConfigPath = Path.Combine(directory, "missing")
        };
        var measured = new List<(string Instrument, long Value, string? Directory)>();

        using var metrics = new MachineMetrics(storage);
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == MachineMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? volume = null;

            foreach (var tag in tags)
            {
                volume = tag.Key == "culina.storage.directory" ? tag.Value as string : volume;
            }

            measured.Add((instrument.Name, value, volume));
        });
        listener.Start();

        listener.RecordObservableInstruments();

        Assert.Contains(measured, measure => measure is { Instrument: "system.memory.limit", Value: > 0 });
        Assert.Contains(measured, measure => measure is { Instrument: "culina.storage.limit", Directory: "images", Value: > 0 });
        Assert.Equal(2, measured.Count(measure => measure is { Instrument: "culina.storage.usage", Directory: "keys" }));

        // A directory that does not exist is left out rather than failing the collection.
        Assert.DoesNotContain(measured, measure => measure.Directory == "config");
    }

    private sealed class Exported : BaseProcessor<LogRecord>
    {
        public List<string> Attributes { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            if (data.CategoryName == CulinaTelemetry.WebAppCategory)
            {
                return;
            }

            Attributes.AddRange(ParentProvider!.GetResource().Attributes.Select(attribute => attribute.Key));
        }
    }
}
