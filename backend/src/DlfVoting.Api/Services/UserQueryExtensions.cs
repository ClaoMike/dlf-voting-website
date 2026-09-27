using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

public static class UserQueryExtensions
{
    /// <summary>
    /// Alphabetical by the displayed name ("First Last") using the Danish alphabet (Æ, Ø, Å after Z);
    /// users without a name go last, ordered by username.
    /// </summary>
    public static IOrderedQueryable<User> OrderByName(this IQueryable<User> users) =>
        users
            .OrderBy(u => u.FirstName == null && u.LastName == null)
            .ThenBy(u => EF.Functions.Collate(((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(), DlfVotingDbContext.DanishCollation))
            .ThenBy(u => u.Username);

    /// <summary>"First Last", or null when the user has no name on record.</summary>
    public static string? FullName(string? firstName, string? lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return name.Length == 0 ? null : name;
    }
}
