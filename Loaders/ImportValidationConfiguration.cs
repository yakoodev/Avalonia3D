using System;
using System.Collections.Generic;

namespace Avalonia3D.Loaders;

public enum ImportValidationPolicy
{
    Strict,
    RelaxedWithWarnings
}

public static class ImportValidationConfiguration
{
    private const string PolicyArgPrefix = "--import-validation=";
    private const string PolicyEnv = "AVALONIA3D_IMPORT_VALIDATION";

    public static ImportValidationPolicy CurrentPolicy { get; private set; } = ImportValidationPolicy.RelaxedWithWarnings;

    public static void Configure(ImportValidationPolicy policy)
    {
        CurrentPolicy = policy;
    }

    public static ImportValidationPolicy ResolveFrom(string[]? args, IReadOnlyDictionary<string, string?>? environment = null, ImportValidationPolicy fallback = ImportValidationPolicy.RelaxedWithWarnings)
    {
        var argsPolicy = TryParseFromArgs(args);
        if (argsPolicy.HasValue)
        {
            return argsPolicy.Value;
        }

        var envPolicy = TryParseFromEnvironment(environment);
        if (envPolicy.HasValue)
        {
            return envPolicy.Value;
        }

        return fallback;
    }

    private static ImportValidationPolicy? TryParseFromArgs(string[]? args)
    {
        if (args == null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(PolicyArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = arg[PolicyArgPrefix.Length..];
            return TryParsePolicy(value);
        }

        return null;
    }

    private static ImportValidationPolicy? TryParseFromEnvironment(IReadOnlyDictionary<string, string?>? environment)
    {
        string? value;
        if (environment != null)
        {
            environment.TryGetValue(PolicyEnv, out value);
        }
        else
        {
            value = Environment.GetEnvironmentVariable(PolicyEnv);
        }

        return TryParsePolicy(value);
    }

    private static ImportValidationPolicy? TryParsePolicy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "strict", StringComparison.OrdinalIgnoreCase))
        {
            return ImportValidationPolicy.Strict;
        }

        if (string.Equals(value, "relaxed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "relaxedwithwarnings", StringComparison.OrdinalIgnoreCase))
        {
            return ImportValidationPolicy.RelaxedWithWarnings;
        }

        return null;
    }
}
