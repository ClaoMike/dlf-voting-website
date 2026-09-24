using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Validation;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

public class AdministratorService
{
    public const int PageSize = 25;

    private const string AdminGoneMessage = "This administrator no longer exists.";
    private const string AdminAlreadyRemovedMessage = "This administrator has already been removed.";
    private const string OwnAccountGoneMessage = "Your account could not be found.";
    private const string DuplicateUsernameMessage = "An administrator with this username already exists.";
    private const string DuplicateEmailMessage = "An administrator with this email already exists.";

    private static readonly string InvalidUsernameMessage = IdentityRules.InvalidUsernameMessage(Administrator.UsernameMaxLength);

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public AdministratorService(DlfVotingDbContext db)
    {
        _db = db;
    }

    /// <summary>The signed-in admin first, then everyone else by username.</summary>
    public async Task<PagedAdministratorsResponse> GetPageAsync(Guid currentAdminId, int page)
    {
        if (page < 1) page = 1;

        var totalCount = await _db.Administrators.CountAsync();
        var items = await _db.Administrators
            .OrderBy(a => a.Id == currentAdminId ? 0 : 1)
            .ThenBy(a => a.Username)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new AdministratorResponse(a.Id, a.Username, a.CreatedAt))
            .ToListAsync();

        return new PagedAdministratorsResponse(items, totalCount, page, PageSize);
    }

    public async Task<OperationResult<Administrator>> CreateAsync(CreateAdministratorRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email.Trim();

        if (!IdentityRules.IsValidUsername(username, Administrator.UsernameMaxLength)) return OperationResult.Invalid(InvalidUsernameMessage);
        if (!IdentityRules.IsValidEmail(email)) return OperationResult.Invalid(IdentityRules.InvalidEmailMessage);
        if (!IdentityRules.IsValidPassword(request.Password)) return OperationResult.Invalid(IdentityRules.InvalidPasswordMessage);

        if (await _db.Administrators.AnyAsync(a => a.Username == username)) return OperationResult.Conflict(DuplicateUsernameMessage);
        if (await _db.Administrators.AnyAsync(a => a.Email == email)) return OperationResult.Conflict(DuplicateEmailMessage);

        var admin = new Administrator
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };
        _db.Administrators.Add(admin);

        return await SaveAsync(admin, AdminGoneMessage);
    }

    /// <summary>Updates another administrator; any field left empty stays unchanged.</summary>
    public async Task<OperationResult<Administrator>> UpdateAsync(Guid currentAdminId, Guid id, UpdateAdministratorRequest request)
    {
        if (id == currentAdminId) return OperationResult.Forbidden("You cannot edit your own administrator account here.");

        var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var password = string.IsNullOrEmpty(request.Password) ? null : request.Password;

        if (username is null && email is null && password is null) return OperationResult.Invalid("Provide a new username, email or password.");
        if (username is not null && !IdentityRules.IsValidUsername(username, Administrator.UsernameMaxLength)) return OperationResult.Invalid(InvalidUsernameMessage);
        if (email is not null && !IdentityRules.IsValidEmail(email)) return OperationResult.Invalid(IdentityRules.InvalidEmailMessage);
        if (password is not null && !IdentityRules.IsValidPassword(password)) return OperationResult.Invalid(IdentityRules.InvalidPasswordMessage);

        var admin = await _db.Administrators.FindAsync(id);
        if (admin is null) return OperationResult.NotFound(AdminGoneMessage);

        if (username is not null)
        {
            if (await _db.Administrators.AnyAsync(a => a.Username == username && a.Id != id)) return OperationResult.Conflict(DuplicateUsernameMessage);
            admin.Username = username;
        }

        if (email is not null)
        {
            if (await _db.Administrators.AnyAsync(a => a.Email == email && a.Id != id)) return OperationResult.Conflict(DuplicateEmailMessage);
            admin.Email = email;
        }

        if (password is not null) admin.PasswordHash = PasswordHashing.Hash(password);

        return await SaveAsync(admin, AdminGoneMessage);
    }

    public async Task<OperationResult> DeleteAsync(Guid currentAdminId, Guid id)
    {
        if (id == currentAdminId) return OperationResult.Forbidden("You cannot remove your own administrator account.");

        var admin = await _db.Administrators.FindAsync(id);
        if (admin is null) return OperationResult.NotFound(AdminAlreadyRemovedMessage);

        _db.Administrators.Remove(admin);
        var saved = await SaveAsync(admin, AdminAlreadyRemovedMessage);
        return saved.Status == OperationStatus.Ok ? OperationResult.Success : new OperationResult(saved.Status, saved.Message);
    }

    public async Task<OperationResult<Administrator>> ChangeOwnPasswordAsync(Guid currentAdminId, string? password)
    {
        if (!IdentityRules.IsValidPassword(password)) return OperationResult.Invalid(IdentityRules.InvalidPasswordMessage);

        var admin = await _db.Administrators.FindAsync(currentAdminId);
        if (admin is null) return OperationResult.NotFound(OwnAccountGoneMessage);

        admin.PasswordHash = PasswordHashing.Hash(password!);
        return await SaveAsync(admin, OwnAccountGoneMessage);
    }

    private async Task<OperationResult<Administrator>> SaveAsync(Administrator admin, string goneMessage)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.NotFound(goneMessage);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Lost a race with a concurrent create/update: the index says which value was taken.
            return OperationResult.Conflict(ex.ViolatedConstraint() == "IX_Administrators_Username" ? DuplicateUsernameMessage : DuplicateEmailMessage);
        }

        return OperationResult<Administrator>.Success(admin);
    }
}
