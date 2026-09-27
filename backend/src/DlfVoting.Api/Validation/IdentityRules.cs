using System.Text.RegularExpressions;

namespace DlfVoting.Api.Validation;

/// <summary>
/// Username, email and password rules shared by users and administrators (mirrored in frontend/src/utils/validation.ts).
/// </summary>
public static partial class IdentityRules
{
    public const int UsernameMinLength = 5;

    // Matches the varchar(320) email columns (the RFC 5321 maximum).
    public const int EmailMaxLength = 320;

    public const string InvalidEmailMessage = "Please provide a valid email address.";
    public const string InvalidPasswordMessage =
        "Password must be 20-64 characters and include at least one uppercase letter, one digit, and one special character.";

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$")]
    private static partial Regex PasswordRegex();

    // No whitespace or control characters; anything else (including "@" and æ/ø/å) is allowed.
    [GeneratedRegex(@"^[^\s\p{C}]*$")]
    private static partial Regex UsernameCharactersRegex();

    public static bool IsValidEmail(string email) => email.Length <= EmailMaxLength && EmailRegex().IsMatch(email);

    public static bool IsValidPassword(string? password) => !string.IsNullOrEmpty(password) && PasswordRegex().IsMatch(password);

    public static bool IsValidUsername(string username, int maxLength) =>
        username.Length >= UsernameMinLength && username.Length <= maxLength && UsernameCharactersRegex().IsMatch(username);

    public static string InvalidUsernameMessage(int maxLength) =>
        $"Username must be {UsernameMinLength}-{maxLength} characters and cannot contain spaces.";

    /// <summary>Trims the email; blank means "no email".</summary>
    public static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}
