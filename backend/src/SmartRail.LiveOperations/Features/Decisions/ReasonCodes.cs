namespace SmartRail.LiveOperations.Features.Decisions;

// The reason codes a controller can give for a delay, hold or cancellation (agreed with Senuja, 18 Oct). Passengers see the code
// (the apps turn it into words) and the optional plain-text note. A new code is a new line here and a new line in the apps.
internal static class ReasonCodes
{
    public static readonly IReadOnlyList<string> All =
        ["SignalFailure", "TrackWork", "TrainFault", "Weather", "CrewShortage", "Congestion", "Other"];

    public const int MaxNoteLength = 300;

    // Adds an error for the code and the note if they are not acceptable. `required`: a delay and a cancellation must have a code;
    // a hold may go without.
    public static void Check(string? code, string? note, bool required, Dictionary<string, string[]> errors, string codeField = "reasonCode")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            if (required)
            {
                errors[codeField] = [$"Give a reason code: {string.Join(", ", All)}."];
            }
            else if (!string.IsNullOrWhiteSpace(note))
            {
                errors[codeField] = ["A note needs a reason code."];
            }
        }
        else if (!All.Contains(code))
        {
            errors[codeField] = [$"The reason code must be one of: {string.Join(", ", All)}."];
        }

        if (note is { Length: > MaxNoteLength })
        {
            errors["note"] = [$"The note can have at most {MaxNoteLength} characters."];
        }
    }
}
