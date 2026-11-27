using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Application.UseCases.Customers.DeleteCustomer;
using BallastLane.Application.UseCases.Customers.GetCustomerById;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.Application.UseCases.Customers.UpdateCustomer;
using BallastLane.WebApi.Models.Requests.Customers;
using BallastLane.WebApi.Extensions;
using BallastLane.WebApi.Models.Responses.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace BallastLane.WebApi.Controllers;
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    CreateCustomerHandler createHandler,
    ListCustomersHandler listHandler,
    GetCustomerByIdHandler getByIdHandler,
    UpdateCustomerHandler updateHandler,
    DeleteCustomerHandler deleteHandler) : ControllerBase
{
    /// <summary>Retrieves all registered customers.</summary>
    /// <remarks>Returns active and inactive customers ordered by name and then ID.</remarks>
    /// <response code="200">The customer array. When no customers are registered, the response body is [].</response>
    /// <response code="500">An unexpected error occurred. The response includes a request trace identifier.</response>
    [HttpGet]
    [ProducesResponseType<ListCustomersResponse[]>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListCustomersResponse[]>> List(CancellationToken cancellationToken)
    {
        var customers = await listHandler.HandleAsync(cancellationToken);
        return Ok(customers.Select(customer => new ListCustomersResponse(
            customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version, customer.Active)).ToArray());
    }

    /// <summary>Retrieves a customer by its unique identifier.</summary>
    /// <remarks>Inactive customers remain retrievable.</remarks>
    /// <param name="id">The unique, non-empty customer UUID.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="200">The requested customer.</response>
    /// <response code="400">The identifier is malformed or empty.</response>
    /// <response code="404">No customer has the requested identifier.</response>
    /// <response code="500">An unexpected error occurred. The response includes a request trace identifier.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<GetCustomerByIdResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<GetCustomerByIdResponse>> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var customer = await getByIdHandler.HandleAsync(new(id), cancellationToken);
        return Ok(new GetCustomerByIdResponse(
            customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version, customer.Active));
    }

    /// <summary>Updates an existing customer using optimistic concurrency control.</summary>
    /// <remarks>Replaces all editable values. An omitted trade name clears it. Each successful update generates a new version, even when values are unchanged. On a concurrency conflict, retrieve the latest customer before deciding whether to submit another update.</remarks>
    /// <param name="id">The unique, non-empty customer UUID.</param>
    /// <param name="request">The replacement values and expected customer version.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="200">The updated customer with the newly persisted version.</response>
    /// <response code="400">The identifier, version, or replacement values are invalid.</response>
    /// <response code="404">No customer has the requested identifier.</response>
    /// <response code="409">The customer version is stale or the tax ID belongs to another customer.</response>
    /// <response code="500">An unexpected error occurred. The response includes a request trace identifier.</response>
    [HttpPut("{id}")]
    [RequireCustomerAuthor]
    [ProducesResponseType<UpdateCustomerResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<UpdateCustomerResponse>> Update(
        [FromRoute] Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await updateHandler.HandleAsync(
            new(id, request.Name, request.TradeName, request.CustomerType!.Value, request.TaxId, request.Email, request.Version!.Value),
            RequireCustomerAuthorAttribute.GetUserId(User)!.Value,
            cancellationToken);
        return Ok(new UpdateCustomerResponse(
            result.Id, result.Name, result.TradeName, result.CustomerType, result.TaxId, result.Email, result.Version));
    }

    /// <summary>Deactivates an existing customer using soft delete and optimistic concurrency control.</summary>
    /// <remarks>The database record is retained. The supplied token must match the persisted version. Deactivation generates a new version. An already inactive customer with a matching version succeeds without a write. A stale token returns 409, including after deactivation.</remarks>
    /// <param name="id">The unique, non-empty customer UUID.</param>
    /// <param name="version">The optimistic concurrency token obtained from a previous customer response.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="204">The customer was deactivated or was already inactive with a matching version. The response has no body.</response>
    /// <response code="400">The identifier or version is invalid, or the version is missing.</response>
    /// <response code="404">No customer has the requested identifier.</response>
    /// <response code="409">The customer version is stale. Retrieve the latest customer state before deciding whether to retry.</response>
    /// <response code="500">An unexpected error occurred. The response includes a request trace identifier.</response>
    [HttpDelete("{id}")]
    [RequireCustomerAuthor]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id, [FromQuery, BindRequired] Guid version, CancellationToken cancellationToken)
    {
        await deleteHandler.HandleAsync(new(id, version), RequireCustomerAuthorAttribute.GetUserId(User)!.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>Creates a customer.</summary>
    /// <remarks>New customers are active by default. CustomerType must be the string Individual or Company. TradeName is allowed only for Company.</remarks>
    [HttpPost]
    [RequireCustomerAuthor]
    [ProducesResponseType<CreateCustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateCustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new(request.Name, request.TradeName, request.CustomerType!.Value, request.TaxId, request.Email),
            RequireCustomerAuthorAttribute.GetUserId(User)!.Value, cancellationToken);
        var location = Url.Action(nameof(GetById), new { id = result.Id })
            ?? throw new InvalidOperationException("The customer route could not be generated.");
        return Created(location, new CreateCustomerResponse(result.Id, result.Name, result.TradeName, result.CustomerType, result.TaxId, result.Email, result.Version));
    }
}
