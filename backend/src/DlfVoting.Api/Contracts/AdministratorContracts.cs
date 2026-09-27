using DlfVoting.Domain;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace DlfVoting.Api.Contracts;

public record CreateAdministratorRequest(string? Username, string Email, string Password);

// Any field left empty stays unchanged.
public record UpdateAdministratorRequest(string? Username, string? Email, string? Password);

public record ChangeOwnPasswordRequest(string Password);

public record AdministratorResponse(Guid Id, string Username, DateTime CreatedAt)
{
    public static AdministratorResponse From(Administrator a) => new(a.Id, a.Username, a.CreatedAt);
}

// The frontend reads "pageSize"; renaming it silently breaks its page count ("Page 1 of NaN").
public record PagedAdministratorsResponse(List<AdministratorResponse> Items, int TotalCount, int Page, int PageSize);
