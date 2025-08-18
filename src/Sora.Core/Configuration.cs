using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Sora.Core;

// Simple, no-caching configuration helper
public static class Configuration
{
    // Generic read with default(T)
    public static T? Read<T>(IConfiguration? cfg, string key)
        => Read(cfg, key, default(T)!);

    // Boolean convenience overloads (no cfg)
    public static bool Read(string key) => Read<bool>(null, key, default);
    public static bool Read(string key, bool defaultValue) => Read<bool>(null, key, defaultValue);

    // Boolean convenience overloads (cfg may be null)
    public static bool Read(IConfiguration? cfg, string key) => Read<bool>(cfg, key, default);
    public static bool Read(IConfiguration? cfg, string key, bool defaultValue) => Read<bool>(cfg, key, defaultValue);

    // Int convenience overloads
    public static int Read(string key, int defaultValue) => Read<int>(null, key, defaultValue);
    public static int Read(IConfiguration? cfg, string key, int defaultValue) => Read<int>(cfg, key, defaultValue);

    // String convenience overloads
    public static string? Read(string key, string? defaultValue) => Read<string?>(null, key, defaultValue);
    public static string? Read(IConfiguration? cfg, string key, string? defaultValue) => Read<string?>(cfg, key, defaultValue);

    // Read the first non-null string value across multiple keys; returns null if none found
    public static string? ReadFirst(IConfiguration? cfg, params string[] keys)
    {
        if (keys is null || keys.Length == 0) return null;
        foreach (var k in keys)
        {
            var v = Read<string?>(cfg, k, null);
            if (v is not null) return v;
        }
        return null;
    }

    // Read the first non-null string value, with an explicit default when none found
    public static string ReadFirst(IConfiguration? cfg, string defaultValue, params string[] keys)
        => ReadFirst(cfg, keys) ?? defaultValue;

    // First non-null across keys: bool
    public static bool ReadFirst(IConfiguration? cfg, bool defaultValue, params string[] keys)
    {
        if (keys is null || keys.Length == 0) return defaultValue;
        foreach (var k in keys)
        {
            var v = Read<bool?>(cfg, k, null);
            if (v is not null) return v.Value;
        }
        return defaultValue;
    }

    // First non-null across keys: int
    public static int ReadFirst(IConfiguration? cfg, int defaultValue, params string[] keys)
    {
        if (keys is null || keys.Length == 0) return defaultValue;
        foreach (var k in keys)
        {
            var v = Read<int?>(cfg, k, null);
            if (v is not null) return v.Value;
        }
        return defaultValue;
    }

    // Read with explicit default
    public static T Read<T>(IConfiguration? cfg, string key, T defaultValue)
    {
        // If cfg not provided, try ambient provider
        if (cfg is null && Sora.Core.SoraApp.Current is not null)
        {
            try
            {
                var sp = Sora.Core.SoraApp.Current;
                if (sp is not null)
                {
                    cfg = (sp as IServiceProvider)?.GetService(typeof(IConfiguration)) as IConfiguration;
                }
            }
            catch { /* ignore */ }
        }
        // Canonicalize key: if caller used single underscores, normalize to ':'
        if (key.IndexOf(':') < 0 && key.IndexOf('_') >= 0)
        {
            key = key.Replace('_', ':');
        }
        // 1) Environment variables (probe multiple shapes for resilience)
    foreach (var envKey in EnumerateEnvKeys(key))
        {
            var envVal = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envVal) && TryConvert(envVal!, out T parsed))
                return parsed;
        }

        // 2) IConfiguration (probe canonical and underscore shapes)
        if (cfg is not null)
        {
            // IConfiguration can bind directly when T is simple; prefer strong GetValue
            var val = cfg.GetValue<T?>(key);
            if (val is not null) return val;

            foreach (var cfgKey in EnumerateConfigKeys(key))
            {
                var str = cfg[cfgKey];
                if (!string.IsNullOrWhiteSpace(str) && TryConvert(str!, out T parsed))
                    return parsed;
            }
        }

        // 3) default
        return defaultValue;
    }

    private static IEnumerable<string> EnumerateEnvKeys(string key)
    {
        // Canonical double-underscore (Kestrel/ASP.NET convention)
        yield return key.Replace(":", "__");
        // Single-underscore common convention (e.g., OTEL_EXPORTER_OTLP_ENDPOINT)
        yield return key.Replace(":", "_");
        // Uppercase variants
        var upper = key.ToUpperInvariant();
        yield return upper.Replace(":", "__");
        yield return upper.Replace(":", "_");
    }

    private static IEnumerable<string> EnumerateConfigKeys(string key)
    {
        // Prefer canonical ':' path first
        yield return key;
        // Also try single underscore form for providers that expose them as flat keys
        yield return key.Replace(":", "_");
    }

    private static bool TryConvert<T>(string value, out T result)
    {
        try
        {
            object? boxed;
            var target = typeof(T);

            if (target == typeof(string))
            {
                boxed = value;
            }
            else if (target == typeof(bool) || target == typeof(bool?))
            {
                if (TryParseBool(value, out var b)) { boxed = b; }
                else { boxed = default(bool); }
            }
            else if (target.IsEnum)
            {
                boxed = Enum.Parse(target, value, ignoreCase: true);
            }
            else if (target == typeof(int) || target == typeof(int?))
            {
                boxed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : default(int);
            }
            else if (target == typeof(double) || target == typeof(double?))
            {
                boxed = double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ? d : default(double);
            }
            else if (target == typeof(TimeSpan) || target == typeof(TimeSpan?))
            {
                boxed = TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts) ? ts : default(TimeSpan);
            }
            else
            {
                // Fallback for other primitives
                boxed = (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(target) ?? target, CultureInfo.InvariantCulture);
            }

            result = (T)boxed!;
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    private static bool TryParseBool(string s, out bool value)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "y":
            case "on":
                value = true; return true;
            case "0":
            case "false":
            case "no":
            case "n":
            case "off":
                value = false; return true;
            default:
                return bool.TryParse(s, out value);
        }
    }
}
