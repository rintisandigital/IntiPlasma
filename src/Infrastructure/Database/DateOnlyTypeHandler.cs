using System.Data;
using System.Globalization;
using Dapper;

namespace Infrastructure.Database;

/// <summary>
/// Lets Dapper read PostgreSQL "date" columns into <see cref="DateOnly"/> properties and bind DateOnly parameters.
/// </summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) =>
        value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture)
        };
}
