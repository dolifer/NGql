/// BenchmarkMerger — merges BenchmarkDotNet CSV reports from two separate runs and renders a side-by-side comparison.
///
/// Usage:
///   dotnet run --project tests/BenchmarkMerger -- <local-artifacts-dir> <published-artifacts-dir>
///
/// Example (from repo root):
///   dotnet run --project tests/BenchmarkRunner         -c Release -- --filter "*VersionComparison*SimpleQuery*" --artifacts artifacts/benchmarks/local
///   dotnet run --project tests/BenchmarkRunner.Published -c Release -- --filter "*VersionComparison*SimpleQuery*" --artifacts artifacts/benchmarks/published
///   dotnet run --project tests/BenchmarkMerger -- artifacts/benchmarks/local artifacts/benchmarks/published
///
/// Both runner projects compile the same VersionComparisonBenchmark source linked from BenchmarkRunner/,
/// but reference different NGql.Core versions (ProjectRef vs NuGet 1.5.0) giving genuine A/B comparison.

if (args.Length < 2)
{
    await Console.Error.WriteLineAsync("Usage: BenchmarkMerger <local-artifacts-dir> <published-artifacts-dir>");
    return 1;
}

var localCsvs = FindCsvs(args[0]);
var publishedCsvs = FindCsvs(args[1]);

if (localCsvs.Length == 0) { await Console.Error.WriteLineAsync($"No CSV report files found under: {args[0]}"); return 1; }
if (publishedCsvs.Length == 0) { await Console.Error.WriteLineAsync($"No CSV report files found under: {args[1]}"); return 1; }

// Pair by benchmark class name (filename stem before "-report.csv")
var localByClass = localCsvs.ToDictionary(BenchmarkClassName);
var publishedByClass = publishedCsvs.ToDictionary(BenchmarkClassName);

var allClasses = localByClass.Keys.Union(publishedByClass.Keys).OrderBy(x => x);

foreach (var cls in allClasses)
{
    var localRows = localByClass.TryGetValue(cls, out var lf) ? ParseCsv(lf) : [];
    var publishedRows = publishedByClass.TryGetValue(cls, out var pf) ? ParseCsv(pf) : [];
    PrintComparison(cls, localRows, publishedRows);
}

return 0;

// ──────────────────────────────────────────────────────────────────────────────

static string[] FindCsvs(string dir) =>
    Directory.Exists(dir)
        ? Directory.GetFiles(dir, "*-report.csv", SearchOption.AllDirectories)
        : [];

static string BenchmarkClassName(string path) =>
    Path.GetFileNameWithoutExtension(path).Replace("-report", "");

static List<Row> ParseCsv(string path)
{
    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return [];

    var headers = SplitCsvLine(lines[0]);
    int Col(string name) => Array.IndexOf(headers, name);

    int iMethod = Col("Method"), iMean = Col("Mean"), iError = Col("Error"),
        iStdDev = Col("StdDev"), iP95 = Col("P95"),
        iGen0 = Col("Gen0"), iGen1 = Col("Gen1"), iAlloc = Col("Allocated");

    var rows = new List<Row>();
    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var cols = SplitCsvLine(line);
        if (cols.Length <= iMethod) continue;

        // Collect parameter columns (any column whose name starts with a known param prefix)
        var parameters = string.Empty;
        // BDN emits explicit param columns like "iterations", "fieldCount" etc.
        // We grab them all except the fixed stats/config columns
        var knownFixed = new HashSet<string>
        {
            "Method","Job","AnalyzeLaunchVariance","EvaluateOverhead","MaxAbsoluteError","MaxRelativeError",
            "MinInvokeCount","MinIterationTime","OutlierMode","Affinity","EnvironmentVariables","Jit",
            "LargeAddressAware","Platform","PowerPlanMode","Runtime","AllowVeryLargeObjects","Concurrent",
            "CpuGroups","Force","HeapAffinitizeMask","HeapCount","NoAffinitize","RetainVm","Server",
            "Arguments","BuildConfiguration","Clock","EngineFactory","NuGetReferences","Toolchain",
            "IsMutator","InvocationCount","IterationCount","IterationTime","LaunchCount","MaxIterationCount",
            "MaxWarmupIterationCount","MemoryRandomization","MinIterationCount","MinWarmupIterationCount",
            "RunStrategy","UnrollFactor","WarmupCount",
            "Mean","Error","StdDev","P95","Gen0","Gen1","Allocated","Median","RatioSD","Ratio","AllocRatio"
        };
        var paramParts = new List<string>();
        for (int i = 0; i < headers.Length && i < cols.Length; i++)
        {
            if (!knownFixed.Contains(headers[i]) && !string.IsNullOrEmpty(cols[i]) && cols[i] != "?")
                paramParts.Add($"{headers[i]}={cols[i]}");
        }
        parameters = paramParts.Count > 0 ? string.Join(", ", paramParts) : string.Empty;

        rows.Add(new Row(
            Method: cols[iMethod],
            Parameters: parameters,
            Mean: iMean >= 0 && iMean < cols.Length ? cols[iMean] : "N/A",
            Error: iError >= 0 && iError < cols.Length ? cols[iError] : "",
            StdDev: iStdDev >= 0 && iStdDev < cols.Length ? cols[iStdDev] : "",
            P95: iP95 >= 0 && iP95 < cols.Length ? cols[iP95] : "",
            Gen0: iGen0 >= 0 && iGen0 < cols.Length ? cols[iGen0] : "",
            Gen1: iGen1 >= 0 && iGen1 < cols.Length ? cols[iGen1] : "",
            Allocated: iAlloc >= 0 && iAlloc < cols.Length ? cols[iAlloc] : "N/A"
        ));
    }
    return rows;
}

static string[] SplitCsvLine(string line)
{
    // BDN quotes fields that contain commas (e.g. "1,707.5 ns"), so we need a real CSV parser.
    var fields = new List<string>();
    var sb = new System.Text.StringBuilder();
    bool inQuotes = false;
    foreach (var ch in line)
    {
        if (ch == '"') { inQuotes = !inQuotes; }
        else if (ch == ',' && !inQuotes) { fields.Add(sb.ToString()); sb.Clear(); }
        else { sb.Append(ch); }
    }
    fields.Add(sb.ToString());
    return [.. fields];
}

static void PrintComparison(string className, List<Row> local, List<Row> published)
{
    // Join by Method + Parameters key
    var localMap = local.ToDictionary(r => r.Key);
    var publishedMap = published.ToDictionary(r => r.Key);
    var allKeys = localMap.Keys.Union(publishedMap.Keys).OrderBy(x => x).ToList();

    if (allKeys.Count == 0) return;

    Console.WriteLine();
    Console.WriteLine($"## {className}");
    Console.WriteLine();
    Console.WriteLine($"| {"Benchmark",-35} | {"Job",-10} | {"Mean",10} | {"Error",8} | {"StdDev",8} | {"P95",10} | {"Gen0",6} | {"Allocated",10} | {"Ratio",6} | {"Alloc Ratio",11} |");
    Console.WriteLine($"|{new string('-', 37)}|{new string('-', 12)}|{new string('-', 12)}|{new string('-', 10)}|{new string('-', 10)}|{new string('-', 12)}|{new string('-', 8)}|{new string('-', 12)}|{new string('-', 8)}|{new string('-', 13)}|");

    foreach (var key in allKeys)
    {
        localMap.TryGetValue(key, out var loc);
        publishedMap.TryGetValue(key, out var pub);

        var label = key.Length > 35 ? key[..32] + "..." : key;

        if (pub is not null)
            PrintRow(label, "Published", pub, null, null);   // baseline first
        if (loc is not null)
        {
            var speedRatio = ComputeRatio(loc.Mean, pub?.Mean);
            var allocRatio = ComputeRatio(loc.Allocated, pub?.Allocated);
            PrintRow(label, "Local", loc, speedRatio, allocRatio);
        }
        Console.WriteLine($"|{new string(' ', 37)}|{new string(' ', 12)}|{new string(' ', 12)}|{new string(' ', 10)}|{new string(' ', 10)}|{new string(' ', 12)}|{new string(' ', 8)}|{new string(' ', 12)}|{new string(' ', 8)}|{new string(' ', 13)}|");
    }

    Console.WriteLine();
    Console.WriteLine("*Ratio < 1.00 = Local is faster/smaller than Published (baseline)*");
}

static void PrintRow(string label, string job, Row r, string? ratio, string? allocRatio)
{
    Console.WriteLine(
        $"| {label,-35} | {job,-10} | {r.Mean,10} | {r.Error,8} | {r.StdDev,8} | {r.P95,10} | {r.Gen0,6} | {r.Allocated,10} | {ratio ?? "1.00",6} | {allocRatio ?? "1.00",11} |");
}

static string? ComputeRatio(string current, string? baseline)
{
    if (baseline is null) return null;
    var c = ParseValue(current);
    var b = ParseValue(baseline);
    if (c is null || b is null || b == 0.0) return null;
    var ratio = c.Value / b.Value;
    var isFaster = ratio < 0.95;
    var isSlower = ratio > 1.05;
    var marker = isFaster ? " 🟢" : isSlower ? " 🔴" : "";
    return $"{ratio:F2}{marker}";
}

static double? ParseValue(string s)
{
    if (string.IsNullOrEmpty(s) || s == "N/A") return null;
    var num = new string(s.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray()).Replace(",", "");
    return double.TryParse(num, System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
}

record Row(string Method, string Parameters, string Mean, string Error, string StdDev,
           string P95, string Gen0, string Gen1, string Allocated)
{
    public string Key => string.IsNullOrEmpty(Parameters) ? Method : $"{Method}({Parameters})";
}
