using System.Data;
using Dapper;

namespace Infrastructure.Persistence;

/// <summary>The one-time Dapper setup, applied at startup.</summary>
internal static class DapperConfiguration
{
    internal static void Apply()
    {
        // Snake_case columns to PascalCase properties, so no query needs an `as "DisplayName"` alias.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }
}

/// <summary>Teaches Dapper what a <see cref="DateOnly"/> is on the way in. A date, not a timestamp: "Thursday" has no time zone.</summary>
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
