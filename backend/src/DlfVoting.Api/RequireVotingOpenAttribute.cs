using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api;

public class RequireVotingOpenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isAdmin = context.HttpContext.User.Identities
            .Any(i => i.AuthenticationType == AuthSchemes.Admin && i.IsAuthenticated);

        if (isAdmin)
        {
            await next();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<DlfVotingDbContext>();
        var settings = await db.VotingSettings.AsNoTracking().FirstOrDefaultAsync();
        var isOpen = settings?.IsVotingOpen ?? true;

        if (!isOpen)
        {
            context.Result = new ObjectResult(new { message = "Voting polls are closed." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}