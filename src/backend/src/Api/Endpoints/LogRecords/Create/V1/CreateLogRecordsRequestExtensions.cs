using Application.LogRecords.Create;
using Request = Contracts.LogRecords.Create.Request;

namespace Api.Endpoints.LogRecords.Create.V1;

/// <summary>Turns the request body and its browser into the command.</summary>
internal static class CreateLogRecordsRequestExtensions
{
    internal static CreateLogRecordsCommand ToCommand(this Request request, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new CreateLogRecordsCommand(
            request.AppVersion,
            [.. request.Records.Select(record =>
                new ReportedRecord(record.Event, record.Message, record.Stack, record.Route))],
            context.Request.Headers.UserAgent.ToString());
    }
}
