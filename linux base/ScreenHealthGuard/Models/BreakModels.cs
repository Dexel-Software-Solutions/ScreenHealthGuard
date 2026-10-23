namespace ScreenHealthGuard.Models;

internal enum BreakOutcome { Completed, Skipped, Snoozed }

/// <summary>What the user is doing right now, as seen by the activity monitor.</summary>
internal enum Presence
{
    /// <summary>Recent keyboard/mouse input.</summary>
    Active,

    /// <summary>Screen unlocked but no input for a while (reading, watching, thinking).</summary>
    Idle,

    /// <summary>PC locked, asleep or otherwise unavailable.</summary>
    Away
}

internal sealed record BreakRequest(TimeSpan Duration);
