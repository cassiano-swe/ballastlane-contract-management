using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BallastLane.WebApi.Extensions;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCustomerAuthorAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (GetUserId(context.HttpContext.User) is null)
            context.Result = new ChallengeResult();
    }

    internal static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var subjects = principal.Identities.Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.Claims).Where(claim => claim.Type == "sub").Take(2).ToArray();
        return subjects.Length == 1 && Guid.TryParse(subjects[0].Value, out var userId) && userId != Guid.Empty
            ? userId
            : null;
    }
}
