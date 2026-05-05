namespace Pecualia.Api.Configuration;

internal static class DotEnvLoader
{
    public static void LoadFromNearest(string startDirectory, string fileName = ".env")
    {
        var path = FindUpwards(startDirectory, fileName);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var currentValue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            Environment.SetEnvironmentVariable(key, Unquote(value));
        }
    }

    private static string? FindUpwards(string startDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }
        }

        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal);
    }
}
