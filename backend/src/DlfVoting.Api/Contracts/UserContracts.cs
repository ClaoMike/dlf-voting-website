using DlfVoting.Domain;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace DlfVoting.Api.Contracts;

public record CreateUserRequest(string? Username, string? Email, string Password);

// Username and email are always sent with their final values (empty email removes it); password is optional.
public record UpdateUserRequest(string? Username, string? Email, string? Password);

public record UserResponse(
    Guid Id,
    string Username,
    string? Email,
    string? EmployeeCode,
    string? FirstName,
    string? LastName,
    string? CompanyCode,
    DateOnly? EmploymentDate,
    string? Electability,
    DateTime CreatedAt)
{
    public static UserResponse From(User u) =>
        new(u.Id, u.Username, u.Email, u.EmployeeCode, u.FirstName, u.LastName, u.CompanyCode, u.EmploymentDate,
            u.Electability, u.CreatedAt);
}

// The list only carries what the table shows; the full record comes from GET /api/users/{id}.
public record UserListItem(Guid Id, string? Name, string Username);

public record PagedUsersResponse(List<UserListItem> Items, int TotalCount, int Page, int PageSize);
