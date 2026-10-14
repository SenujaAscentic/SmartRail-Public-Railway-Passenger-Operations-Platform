namespace SmartRail.Identity.Features.StaffSignIn;

internal static class StaffSignInValidator
{
    // A limit so nobody can make the server hash a huge "password".
    public const int MaxLength = 200;

    // Returns the field messages, or an empty dictionary when the request is acceptable.
    public static Dictionary<string, string[]> Validate(StaffSignInRequest? request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request?.StaffNumber))
        {
            errors["staffNumber"] = ["Enter your staff number."];
        }
        else if (request.StaffNumber.Length > MaxLength)
        {
            errors["staffNumber"] = [$"The staff number can be at most {MaxLength} characters."];
        }

        if (string.IsNullOrEmpty(request?.Password))
        {
            errors["password"] = ["Enter your password."];
        }
        else if (request.Password.Length > MaxLength)
        {
            errors["password"] = [$"The password can be at most {MaxLength} characters."];
        }

        return errors;
    }
}
