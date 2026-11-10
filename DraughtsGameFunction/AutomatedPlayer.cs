using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Interface;
using DraughtsGameFunctionService.Intstance;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace DraughtsGameFunction;

public static class AutomatedPlayer
{
    private const string UnknownVersionMessage = "No version in body of request";
    private const string InvalidDepthMessage = "Depth must be 1 or more.";
    private const string NoMoveMessage = "Player 2 has no legal moves.";

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

        var boardError = RequestReader.BoardError(getNextMove.Board);
        if (boardError is not null)
        {
            return BadRequest(boardError);
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

        // Normal and Hard search to Depth, and a search of depth 0 has no move to return (defect 2).
        // Easy does not search. Depth stops being read in step 3 (plan section 4).
        var searches = getNextMove.Version is 2 or 3;
        if (searches && getNextMove.Depth < 1)
        {
            return BadRequest(InvalidDepthMessage);
        }

        // Spec section 6.1.5, item 3 (defect 2). Until step 3, Easy plays as the player in the request
        // (item 1), so the check applies to Easy only when that player is 2, as the web app always sends.
        var playsAsPlayer2 = getNextMove.Version != 1 || getNextMove.Player == 2;
        if (playsAsPlayer2 && !MoveGenerator.HasMove(Board.FromRows(getNextMove.Board), player: 2))
        {
            return BadRequest(NoMoveMessage);
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
