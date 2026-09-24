namespace DlfVoting.Domain;

public class User
{
    public const int UsernameMaxLength = 20;
    public const int EmployeeFieldMaxLength = 200;

    public Guid Id { get; init; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    // Optional employee data, filled in by the employee CSV import.
    public string? EmployeeCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyCode { get; set; }
    public DateOnly? EmploymentDate { get; set; }
    public string? Electability { get; set; }
}
