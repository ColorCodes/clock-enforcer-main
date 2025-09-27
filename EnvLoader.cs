using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;


namespace ClockEnforcer

{
internal static class EnvLoader
{
    public static void LoadIfPresent()
    {
        foreach (var path in CandidatePaths())
        {
            var file = Path.Combine(path, ".env");
            if (!File.Exists(file)) continue;

            foreach (var (k, v) in ParseEnv(File.ReadAllLines(file)))
            {
                if (Environment.GetEnvironmentVariable(k) is null)
                    Environment.SetEnvironmentVariable(k, v); // process scope
            }
            break; // stop on first found
        }
    }

    // Precedence: user -> machine -> install folder -> CWD
    private static IEnumerable<string> CandidatePaths()
    {
        var (company, product) = DetectBranding();

        var appData     = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);       // per-user
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); // machine
        var baseDir     = AppContext.BaseDirectory;
        var cwd         = Directory.GetCurrentDirectory();

        yield return Path.Combine(appData, company, product);        // %AppData%\Company\Product
        yield return Path.Combine(programData, company, product);    // %ProgramData%\Company\Product
        yield return baseDir;                                        // install folder
        yield return cwd;                                            // optional CLI fallback
    }

    private static (string company, string product) DetectBranding()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var company = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
                      ?? "Default Company Name";
        var product = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                      ?? "setup-clock-enforcer";
        return (San(company), San(product));
    }

    private static string San(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }

    private static IEnumerable<(string key, string val)> ParseEnv(string[] lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var eq = line.IndexOf('=');
            if (eq < 1) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\"")) val = val[1..^1];
            val = Environment.ExpandEnvironmentVariables(val);
            if (!string.IsNullOrWhiteSpace(key)) yield return (key, val);
        }
    }
}
}
