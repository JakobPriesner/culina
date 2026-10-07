using System.Globalization;
using Application.LogRecords.Create;
using Contracts.LogRecords.Create;
using Request = Contracts.LogRecords.Create.Request;

namespace Api.Endpoints.LogRecords.Create.V1;

/// <summary>Turns the request body and its browser into the command.</summary>
/// <remarks>Attribute names are queried by operators, so they are as stable as an API field: OpenTelemetry's where one exists, else <c>culina.web.*</c>.</remarks>
internal static class CreateLogRecordsRequestExtensions
{
    internal static CreateLogRecordsCommand ToCommand(this Request request, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new CreateLogRecordsCommand(
            request.AppVersion,
            [.. request.Records.Select(record =>
                new ReportedRecord(record.Event, record.Message, record.Stack, record.Route, record.ToAttributes()))],
            context.Request.Headers.UserAgent.ToString(),
            request.Client?.ToAttributes() ?? []);
    }

    private static List<KeyValuePair<string, object>> ToAttributes(this Client client) =>
        Present(
            ("session.id", client.SessionId),
            ("browser.language", client.Languages is [var preferred, ..] ? preferred : null),
            ("browser.brands", client.Brands),
            ("browser.platform", client.Platform),
            ("browser.mobile", client.Mobile),
            ("os.version", client.PlatformVersion),
            ("host.arch", client.Architecture),
            ("device.model.name", client.Model),
            ("culina.web.locale", client.Locale),
            ("culina.web.languages", client.Languages),
            ("culina.web.time_zone", client.TimeZone),
            ("culina.web.screen.width", client.ScreenWidth),
            ("culina.web.screen.height", client.ScreenHeight),
            ("culina.web.viewport.width", client.ViewportWidth),
            ("culina.web.viewport.height", client.ViewportHeight),
            ("culina.web.pixel_ratio", (double?)client.PixelRatio),
            ("culina.web.orientation", client.Orientation),
            ("culina.web.color_scheme", client.ColorScheme),
            ("culina.web.reduced_motion", client.ReducedMotion),
            ("culina.web.display_mode", client.DisplayMode),
            ("culina.web.cpu.count", client.Cores),
            ("culina.web.device_memory", (double?)client.DeviceMemory),
            ("culina.web.touch_points", client.TouchPoints),
            ("culina.web.connection.effective_type", client.Connection),
            ("culina.web.connection.downlink", (double?)client.Downlink),
            ("culina.web.connection.rtt", client.RoundTrip),
            ("culina.web.connection.save_data", client.SaveData),
            ("culina.web.navigation_type", client.NavigationType),
            ("culina.web.service_worker", client.ServiceWorker));

    private static List<KeyValuePair<string, object>> ToAttributes(this Record record) =>
        Present(
            ("culina.web.occurred_at", record.OccurredAt?.ToString("O", CultureInfo.InvariantCulture)),
            ("culina.web.page_age", record.PageAge),
            ("culina.web.online", record.Online),
            ("culina.web.visible", record.Visible),
            ("culina.web.heap.used", record.HeapUsed),
            ("culina.web.heap.limit", record.HeapLimit));

    private static List<KeyValuePair<string, object>> Present(params (string Name, object? Value)[] attributes) =>
        [.. attributes
            .Where(attribute => attribute.Value is not null)
            .Select(attribute => new KeyValuePair<string, object>(attribute.Name, attribute.Value!))];
}
