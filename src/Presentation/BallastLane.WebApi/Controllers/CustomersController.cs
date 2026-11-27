using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.WebApi.Models.Requests.Customers;
using BallastLane.WebApi.Models.Responses.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BallastLane.WebApi.Controllers;
[ApiController]
[Route("api/customers")]
[AllowAnonymous]
public sealed class CustomersController(CreateCustomerHandler createHandler, ListCustomersHandler listHandler) : ControllerBase
{
    /// <summary>Retrieves all registered customers.</summary>
    /// <remarks>Returns all customers ordered by name and then ID. Anonymous access is temporary and is not intended for production.</remarks>
    /// <response code="200">The customer array. When no customers are registered, the response body is [].</response>
    /// <response code="500">An unexpected error occurred. The response includes a request trace identifier.</response>
    [HttpGet]
    [ProducesResponseType<ListCustomersResponse[]>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListCustomersResponse[]>> List(CancellationToken cancellationToken)
    {
        var customers = await listHandler.HandleAsync(cancellationToken);
        return Ok(customers.Select(customer => new ListCustomersResponse(
            customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email)).ToArray());
    }

    /// <summary>Creates a customer.</summary>
    /// <remarks>CustomerType must be the string Individual or Company. TradeName is allowed only for Company. Anonymous access is temporary and is not intended for production.</remarks>
    [HttpPost]
    [ProducesResponseType<CreateCustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateCustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(new(request.Name, request.TradeName, request.CustomerType!.Value, request.TaxId, request.Email), cancellationToken);
        return Created($"/api/customers/{result.Id}", new CreateCustomerResponse(result.Id, result.Name, result.TradeName, result.CustomerType, result.TaxId, result.Email));
    }
}
