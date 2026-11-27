using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.WebApi.Models.Requests.Customers;
using BallastLane.WebApi.Models.Responses.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BallastLane.WebApi.Controllers;
[ApiController]
[Route("api/customers")]
[AllowAnonymous]
public sealed class CustomersController(CreateCustomerHandler handler) : ControllerBase
{
    /// <summary>Creates a customer.</summary>
    /// <remarks>CustomerType must be the string Individual or Company. TradeName is allowed only for Company. Anonymous access is temporary and is not intended for production.</remarks>
    [HttpPost]
    [ProducesResponseType<CreateCustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateCustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new(request.Name, request.TradeName, request.CustomerType!.Value, request.TaxId, request.Email), cancellationToken);
        return Created($"/api/customers/{result.Id}", new CreateCustomerResponse(result.Id, result.Name, result.TradeName, result.CustomerType, result.TaxId, result.Email));
    }
}
