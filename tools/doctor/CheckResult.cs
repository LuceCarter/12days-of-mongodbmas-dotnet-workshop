namespace SantasWorkshop.Doctor;

public enum Status { Pass, Warn, Fail, Skip }

/// <summary>The outcome of one check, plus how to fix it if it went wrong.</summary>
public sealed record CheckResult(
    Status Status,
    string Name,
    string Summary,
    string? Detail = null,
    string? Fix = null)
{
    public static CheckResult Pass(string name, string summary) =>
        new(Status.Pass, name, summary);

    public static CheckResult Warn(string name, string summary, string? fix = null) =>
        new(Status.Warn, name, summary, Fix: fix);

    public static CheckResult Fail(string name, string summary, string? detail, string fix) =>
        new(Status.Fail, name, summary, detail, fix);

    public static CheckResult Skipped(string name, string fix) =>
        new(Status.Skip, name, "not run", Fix: fix);

    public void Print()
    {
        var (icon, colour) = Status switch
        {
            Status.Pass => ("✅", ConsoleColor.Green),
            Status.Warn => ("⚠️ ", ConsoleColor.Yellow),
            Status.Fail => ("❌", ConsoleColor.Red),
            _           => ("⏭️ ", ConsoleColor.DarkGray)
        };

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = colour;
        Console.WriteLine($"  {icon} {Name}");
        Console.ForegroundColor = previous;
        Console.WriteLine($"       {Summary}");

        if (!string.IsNullOrWhiteSpace(Detail))
            Console.WriteLine($"       {Detail.ReplaceLineEndings("\n       ")}");

        if (!string.IsNullOrWhiteSpace(Fix))
        {
            Console.WriteLine();
            Console.WriteLine($"       → {Fix.ReplaceLineEndings("\n         ")}");
        }

        Console.WriteLine();
    }
}
