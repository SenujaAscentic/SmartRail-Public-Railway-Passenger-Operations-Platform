namespace SmartRail.Identity.Authentication;

internal static class PasswordSignInValidator
{
    // A limit so nobody can make the server hash a huge "password".
    public const int MaxLength = 200;

    public static Dictionary<string, string[]> Validate(PasswordSignInRequest? request) =>
        Validate(request?.StaffNumber, request?.Password);

    // Returns the field messages, or an empty dictionary when the fields are acceptable.
    public static Dictionary<string, string[]> Validate(string? staffNumber, string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(staffNumber))
        {
            errors["staffNumber"] = ["Enter your staff number."];
        }
        else if (staffNumber.Length > MaxLength)
        {
            errors["staffNumber"] = [$"The staff number can be at most {MaxLength} characters."];
        }

        if (string.IsNullOrEmpty(password))
        {
            errors["password"] = ["Enter your password."];
        }
        else if (password.Length > MaxLength)
        {
            errors["password"] = [$"The password can be at most {MaxLength} characters."];
        }

        return errors;
    }
}
