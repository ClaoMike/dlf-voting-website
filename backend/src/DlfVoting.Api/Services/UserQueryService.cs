using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

public class UserQueryService
{
    public const int PageSize = 25;

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserQueryService(DlfVotingDbContext db)
    {
        _db = db;
    }

    public async Task<PagedUsersResponse> GetPageAsync(int page)
    {
        page = Paging.ClampPage(page, PageSize);

        var totalCount = await _db.Users.CountAsync();

        var rows = await _db.Users
            .OrderByName()
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Username })
            .ToListAsync();

        var items = rows
            .Select(u => new UserListItem(u.Id, UserQueryExtensions.FullName(u.FirstName, u.LastName), u.Username))
            .ToList();

        return new PagedUsersResponse(items, totalCount, page, PageSize);
    }

    public async Task<User?> GetByIdAsync(Guid id) => await _db.Users.FindAsync(id);
}
