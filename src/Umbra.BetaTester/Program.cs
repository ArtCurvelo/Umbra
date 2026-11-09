using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Umbra.BetaTester;

// AI beta tester of UMBRA (vertical slice). Plays headless with several player profiles and writes a report.
//   dotnet run --project src/Umbra.BetaTester -- [--classe barbaro|mago|ladino|paladino|druida] [--runs N] [--seed S]
//                                                [--perfis novato,veterano] [--saida pasta] [--versao txt]
var args_ = args.ToList();
string? Opt(string name) { int i = args_.IndexOf(name); return i >= 0 && i + 1 < args_.Count ? args_[i + 1] : null; }

string[] playable = { "barbaro", "mago", "ladino", "paladino", "druida" };      // Bruxo and Necromante: 5.8d2–d4
Facts.Class = Opt("--classe") ?? "barbaro";
if (!playable.Contains(Facts.Class)) { Console.Error.WriteLine($"classe não jogável na slice: {Facts.Class} ({string.Join(", ", playable)})"); return 1; }
Console.WriteLine($"{Facts.ClassName}: {Facts.Name(Facts.Basic)}, {Facts.Name(Facts.Main)}, {Facts.Name(Facts.Mobility)}");

int runs = int.TryParse(Opt("--runs"), out var r) ? Math.Max(1, r) : 40;
ulong first = ulong.TryParse(Opt("--seed"), out var s) ? s : 1;
string output = Opt("--saida") ?? Path.Combine("docs", "betatest");
string version = Opt("--versao") ?? Git("rev-parse --short HEAD") ?? "desconhecida";
var personas = (Opt("--perfis")?.Split(',').Select(Persona.Find).OfType<Persona>().ToList() is { Count: > 0 } chosen) ? chosen : Persona.All.ToList();

var clock = Stopwatch.StartNew();
var summaries = new List<Summary>();
var replays = new List<(RunRecord, RunRecord)>();
foreach (var persona in personas)
{
    // one at a time: the rules' services (SkillEnv, EnemyAI.Active) are process-wide, as in UMBRA
    var records = Enumerable.Range(0, runs).Select(i => Session.Play(persona, first + (ulong)i)).ToList();
    summaries.Add(new Summary { Persona = persona, Runs = records });
    foreach (var rec in records.Take(3)) replays.Add((rec, Session.Play(persona, rec.Seed)));
    Console.WriteLine($"{persona.Name,-10} {records.Count} partidas: venceu {records.Count(x => x.Outcome == Outcome.Cleared)}, " +
                      $"morreu {records.Count(x => x.Outcome == Outcome.Died)}, outros {records.Count(x => x.Outcome is Outcome.Stuck or Outcome.Crashed or Outcome.Timeout)}");
}
var findings = Analyst.Analyze(summaries, replays);
string date = DateTime.Now.ToString("yyyy-MM-dd");
Directory.CreateDirectory(output);
string md = Path.Combine(output, $"relatorio_{date}.md"), json = Path.Combine(output, $"dados_{date}.json");
File.WriteAllBytes(md, Encoding.UTF8.GetBytes(Report.Markdown(summaries, findings, date, version, first).Replace("\r\n", "\n")));
File.WriteAllBytes(json, JsonSerializer.SerializeToUtf8Bytes(new
{
    date, version, firstSeed = first, runsPerProfile = runs,
    findings = findings.Select(x => new { severity = x.Severity.ToString(), x.Area, x.Title, x.Evidence, x.Suggestion }),
    runs = summaries.SelectMany(x => x.Runs),
}, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
Console.WriteLine($"{findings.Count(x => x.Severity != Severity.Ok)} pontos a mudar, {findings.Count(x => x.Severity == Severity.Bug)} bugs; {clock.Elapsed.TotalSeconds:0.0} s");
Console.WriteLine(md);
return findings.Any(x => x.Severity == Severity.Bug) ? 2 : 0;

static string? Git(string arguments)
{
    try
    {
        var p = Process.Start(new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, UseShellExecute = false });
        string text = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
        return p.ExitCode == 0 && text.Length > 0 ? text : null;
    }
    catch (Exception) { return null; }
}
