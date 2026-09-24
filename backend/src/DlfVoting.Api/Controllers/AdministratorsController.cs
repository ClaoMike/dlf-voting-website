using System.Security.Claims;
using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/administrators")]
[Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
public class AdministratorsController : ControllerBase
{
    private readonly AdministratorService _administrators;

    // ReSharper disable once ConvertToPrimaryConstructor
    public AdministratorsController(AdministratorService administrators)
    {
        _administrators = administrators;
    }

    private Guid CurrentAdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1) =>
        Ok(await _administrators.GetPageAsync(CurrentAdminId, page));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdministratorRequest request) =>
        this.ToActionResult(await _administrators.CreateAsync(request), AdministratorResponse.From);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdministratorRequest request) =>
        this.ToActionResult(await _administrators.UpdateAsync(CurrentAdminId, id, request), AdministratorResponse.From);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        this.ToActionResult(await _administrators.DeleteAsync(CurrentAdminId, id));

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangeOwnPasswordRequest request) =>
        this.ToActionResult(await _administrators.ChangeOwnPasswordAsync(CurrentAdminId, request.Password), AdministratorResponse.From);
}
