using BallastLane.Application.UseCases.Examples;
using BallastLane.WebApi.Controllers;
using BallastLane.WebApi.Models.Requests;
using BallastLane.WebApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BallastLane.WebApi.UnitTests;

public class ExampleControllerTests
{
    [Fact]
    public void MapsRequestToHandlerAndReturnsMappedResponse()
    {
        var controller = new ExampleController(new CalculatePercentageHandler(new CalculatePercentageValidator()));
        var result = controller.Calculate(new CalculatePercentageRequest(200, 25));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(50m, Assert.IsType<CalculatePercentageResponse>(ok.Value).Amount);
    }
}
