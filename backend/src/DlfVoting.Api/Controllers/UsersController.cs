using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>Admin management of users. The Excel imports are in <see cref="UserImportsController"/>.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserQueryService _queries;
    private readonly UserAccountService _accounts;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UsersController(UserQueryService queries, UserAccountService accounts)
    {
        _queries = queries;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1) =>
        Ok(await _queries.GetPageAsync(page));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _queries.GetByIdAsync(id);
        return user is null
            ? NotFound(new { message = "This user no longer exists." })
            : Ok(UserResponse.From(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request) =>
        this.ToActionResult(await _accounts.CreateAsync(request), UserResponse.From);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request) =>
        this.ToActionResult(await _accounts.UpdateAsync(id, request), UserResponse.From);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        this.ToActionResult(await _accounts.DeleteAsync(id));

    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        await _accounts.DeleteAllAsync();
        return NoContent();
    }
}
