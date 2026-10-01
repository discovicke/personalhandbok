namespace Blazor.Config;

public static class EnvLoader
{
    public static IReadOnlyDictionary<string, string> Load(
        string fileName = ".env",
        bool overrideExisting = false)
    {
        var path = FindEnvFile(fileName);
        if (path is null)
            return new Dictionary<string, string>();

        var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());

            if (key.Length == 0)
                continue;

            loaded[key] = value;

            if (overrideExisting || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }

        return loaded;
    }

    public static string GetRequired(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Missing required environment variable '{key}'. " +
                $"Copy '.env.example' to '.env' and fill it in (never commit .env).");
        return value;
    }

    public static void EnsureRequired(params string[] keys)
    {
        var missing = keys.Where(k => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(k))).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing required environment variables: {string.Join(", ", missing)}. " +
                $"Copy '.env.example' to '.env' and fill them in (never commit .env).");
    }

    private static string? FindEnvFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
            return value[1..^1];
        return value;
    }
}
