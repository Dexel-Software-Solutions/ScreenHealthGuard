namespace ScreenHealthGuard.Infrastructure;

/// <summary>Application and developer metadata.</summary>
internal static class AppInfo
{
    public const string Name = "Screen Health Guard";
    public const string Tagline = "Digital Eye-Strain & Screen Health Guard";
    public const string DeveloperName = "Demiyan Dissanayake";
    public const string GitHubUrl = "https://github.com/dexel-software-solutions";
    public const string PrimaryEmail = "demiyandissanayake@gmail.com";
    public const string SecondaryEmail = "dexelsoftwaresolutions@gmail.com";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string AboutText =>
        $"{Name} v{Version}\n{Tagline}\n\n" +
        $"Developer : {DeveloperName}\n" +
        $"GitHub    : {GitHubUrl}\n" +
        $"Email     : {PrimaryEmail}\n" +
        $"                  {SecondaryEmail}\n\n" +
        "Privacy: only the time since your last input is read. Keystrokes, " +
        "window titles and screen content are never recorded. All data stays on this PC.";
}
