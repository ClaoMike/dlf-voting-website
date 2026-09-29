using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Validation;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

/// <summary>Creating, editing and removing users by hand (the imports live in DlfVoting.Api.Imports).</summary>
public class UserAccountService
{
    private const string UserGoneMessage = "This user no longer exists.";
    private const string UserAlreadyDeletedMessage = "This user has already been deleted.";
    private const string DuplicateUsernameMessage = "A user with this username already exists.";
    private const string DuplicateEmailMessage = "A user with this email already exists.";

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserAccountService(DlfVotingDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<User>> CreateAsync(CreateUserRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = IdentityRules.NormalizeEmail(request.Email);

        var invalid = Validate(username, email, request.Password, passwordRequired: true);
        if (invalid is not null) return invalid;

        var conflict = await FindConflictAsync(username, email, excludeId: null);
        if (conflict is not null) return conflict;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);

        return await SaveAsync(user);
    }

    public async Task<OperationResult<User>> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = IdentityRules.NormalizeEmail(request.Email);

        var invalid = Validate(username, email, request.Password, passwordRequired: false);
        if (invalid is not null) return invalid;

        var user = await _db.Users.FindAsync(id);
        if (user is null) return OperationResult.NotFound(UserGoneMessage);

        var conflict = await FindConflictAsync(username, email, excludeId: id);
        if (conflict is not null) return conflict;

        user.Username = username;
        user.Email = email;
        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = PasswordHashing.Hash(request.Password);
        }

        return await SaveAsync(user);
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return OperationResult.NotFound(UserAlreadyDeletedMessage);

        _db.Users.Remove(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.NotFound(UserAlreadyDeletedMessage);
        }

        return OperationResult.Success;
    }

    public Task DeleteAllAsync() => _db.Users.ExecuteDeleteAsync();

    private static OperationResult? Validate(string username, string? email, string? password, bool passwordRequired)
    {
        if (!IdentityRules.IsValidUsername(username, User.UsernameMaxLength))
            return OperationResult.Invalid(IdentityRules.InvalidUsernameMessage(User.UsernameMaxLength));

        if (email is not null && !IdentityRules.IsValidEmail(email))
            return OperationResult.Invalid(IdentityRules.InvalidEmailMessage);

        if ((passwordRequired || !string.IsNullOrEmpty(password)) && !IdentityRules.IsValidUserPassword(password))
            return OperationResult.Invalid(IdentityRules.InvalidUserPasswordMessage);

        return null;
    }

    private async Task<OperationResult?> FindConflictAsync(string username, string? email, Guid? excludeId)
    {
        if (await _db.Users.AnyAsync(u => u.Username == username && u.Id != excludeId))
            return OperationResult.Conflict(DuplicateUsernameMessage);

        if (email is not null && await _db.Users.AnyAsync(u => u.Email == email && u.Id != excludeId))
            return OperationResult.Conflict(DuplicateEmailMessage);

        return null;
    }

    private async Task<OperationResult<User>> SaveAsync(User user)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.NotFound(UserGoneMessage);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Lost a race with a concurrent create/update: the index says which value was taken.
            return OperationResult.Conflict(ex.ViolatedConstraint() == "IX_Users_Email" ? DuplicateEmailMessage : DuplicateUsernameMessage);
        }

        return OperationResult<User>.Success(user);
    }
}
