using Npgsql;

namespace Pecualia.Api.Configuration;

internal static class PostgresConnectionStringResolver
{
    public static string RequireNormalized(IConfiguration configuration)
    {
        var rawValue =
            configuration.GetConnectionString("Postgres") ??
            configuration["ConnectionStrings:Postgres"] ??
            configuration["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException("Falta la configuración obligatoria 'ConnectionStrings:Postgres'. Revísala en el entorno o en el fichero .env.");
        }

        return Normalize(rawValue);
    }

    public static string Normalize(string rawValue)
    {
        var value = rawValue.Trim().Trim('"', '\'');
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("La URL de PostgreSQL proporcionada no es válida.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var segments = uri.UserInfo.Split(':', 2);
            if (segments.Length > 0)
            {
                builder.Username = Uri.UnescapeDataString(segments[0]);
            }

            if (segments.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(segments[1]);
            }
        }

        foreach (var pair in ParseQuery(uri.Query))
        {
            ApplyQueryParameter(builder, pair.Key, pair.Value);
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }

    private static void ApplyQueryParameter(NpgsqlConnectionStringBuilder builder, string key, string value)
    {
        switch (key.Trim().ToLowerInvariant())
        {
            case "sslmode":
            case "ssl mode":
                if (Enum.TryParse<SslMode>(value, true, out var sslMode))
                {
                    builder.SslMode = sslMode;
                }
                break;
            case "pooling":
                if (bool.TryParse(value, out var pooling))
                {
                    builder.Pooling = pooling;
                }
                break;
            case "timeout":
                if (int.TryParse(value, out var timeout))
                {
                    builder.Timeout = timeout;
                }
                break;
            case "command timeout":
            case "commandtimeout":
                if (int.TryParse(value, out var commandTimeout))
                {
                    builder.CommandTimeout = commandTimeout;
                }
                break;
            case "search path":
            case "searchpath":
                builder.SearchPath = value;
                break;
        }
    }
}
