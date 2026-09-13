using System.Data;
using Dapper;

namespace Infrastructure.Persistence;

/// <summary>
/// The one-time Dapper setup, applied at startup.
/// </summary>
internal static class DapperConfiguration
{
    internal static void Apply()
    {
        // PostgreSQL columns are snake_case and C# properties are PascalCase.
        // Matching them here removes an `as "DisplayName"` alias from every
        // column of every query, which is the kind of noise that eventually
        // gets one column wrong.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }
}

/// <summary>
/// Teaches Dapper what a <see cref="DateOnly"/> is.
/// </summary>
/// <remarks>
/// Npgsql maps a <c>date</c> column to <see cref="DateOnly"/> on the way out
/// already; Dapper is the half that does not know the type on the way in, and
/// without this a query filtered by a date throws rather than returning
/// nothing — which at least is loud.
///
/// A date and not a timestamp, throughout. "Thursday" has no time zone, and
/// storing one would move somebody's dinner when they travelled.
/// </remarks>
internal sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly date => date,
        DateTime moment => DateOnly.FromDateTime(moment),
        _ => DateOnly.Parse((string)value, System.Globalization.CultureInfo.InvariantCulture)
    };
}
