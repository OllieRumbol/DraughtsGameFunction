using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Interface;
using DraughtsGameFunctionService.Intstance;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace DraughtsGameFunction;

public static class AutomatedPlayer
{
    private const string UnknownVersionMessage = "No version in body of request";

    [Function("GetNextMove")]
    public static async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
        CancellationToken ct)
    {
        var (getNextMove, readError) = await RequestReader.ReadAsync<GetNextMove>(request, ct);
        if (getNextMove is null)
        {
            return BadRequest(readError);
        }

        if (getNextMove.Board is null)
        {
            return BadRequest(RequestReader.InvalidBoardMessage);
        }

        IAutomatedPlayerService? service = getNextMove.Version switch
        {
            1 => new AutomatedPlayerServiceV1(),
            2 => new AutomatedPlayerServiceV2(),
            3 => new AutomatedPlayerServiceV3(),
            _ => null
        };

        if (service is null)
        {
            return BadRequest(UnknownVersionMessage);
        }

        NextMove nextMove = service.GetNextMoveForAutomatedPlayer(getNextMove);
        var response = new AutomatedPlayerResponse { Successful = true, NextMove = nextMove };

        return Results.Json(response, ApiJsonOptions.Default);
    }

    private static IResult BadRequest(string? errorMessage)
    {
        var response = new AutomatedPlayerResponse { Successful = false, ErrorMessage = errorMessage };

        return Results.Json(response, ApiJsonOptions.Default, statusCode: StatusCodes.Status400BadRequest);
    }
}
