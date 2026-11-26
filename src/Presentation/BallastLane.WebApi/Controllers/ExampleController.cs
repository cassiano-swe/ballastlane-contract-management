using BallastLane.Application.UseCases.Examples;
using BallastLane.WebApi.Models.Requests;
using BallastLane.WebApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace BallastLane.WebApi.Controllers;

// Temporary architectural demonstration.
[ApiController]
[Route("api/examples")]
[DevelopmentOnly]
public sealed class ExampleController(CalculatePercentageHandler handler) : ControllerBase
{
    /// <summary>Calculates a percentage amount as a temporary architectural example.</summary>
    [HttpPost("calculate-percentage")]
    [ProducesResponseType<CalculatePercentageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<CalculatePercentageResponse> Calculate(CalculatePercentageRequest request)
    {
        var result = handler.Handle(new(request.Value, request.Percentage));
        return Ok(new CalculatePercentageResponse(result.Amount));
    }
}

internal sealed class DevelopmentOnlyAttribute : Attribute, IActionConstraint
{
    public int Order => 0;
    public bool Accept(ActionConstraintContext context) => context.RouteContext.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
}
