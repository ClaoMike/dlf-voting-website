using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Common;

public static class DbUpdateExceptionExtensions
{
    /// <summary>True when the save failed on a unique index (Postgres error 23505).</summary>
    public static bool IsUniqueConstraintViolation(this DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    /// <summary>Name of the unique index that was violated, e.g. "IX_Users_Email".</summary>
    public static string? ViolatedConstraint(this DbUpdateException ex) =>
        (ex.InnerException as Npgsql.PostgresException)?.ConstraintName;
}
