using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionService.Instance;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DraughtsGameFunction;

/// <summary>
/// GetNextMove, from stage 2 (spec section 6.1.1, item 5): the request sends <c>Difficulty</c>, or the old
/// <c>Version</c>, and the computer plays player 2.
/// </summary>
public sealed class AutomatedPlayer(ComputerPlayer computerPlayer, ILogger<AutomatedPlayer> logger)
{
    public const string MissingDifficultyMessage = "Difficulty is missing. It must be one of: easy, normal, hard";
    private const string NoMoveMessage = "Player 2 has no legal moves.";

    // Spec section 6.1.5, item 1: from stage 2, the computer always plays player 2, whatever Player says.
    private const int ComputerPlayerNumber = 2;

    [Function("GetNextMove")]
    public async Task<IResult> Run(
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

        var (difficulty, difficultyError) = DifficultyOf(getNextMove);
        if (difficulty is null)
        {
            return BadRequest(difficultyError);
        }

        // BoardError rejects a missing board, so the board is set here.
        var board = getNextMove.Board!;

        // Spec section 6.1.5, item 3 (defect 2).
        var computerMove = computerPlayer.GetNextMove(board, ComputerPlayerNumber, difficulty.Value);
        if (computerMove is null)
        {
            return BadRequest(NoMoveMessage);
        }

        // Temporary, for T18: the depth each level reaches on Azure (plan section 7.3, step 3). T19 removes it,
        // because spec section 6.4, item 6, does not allow search detail in the logs.
        logger.LogInformation(
            "GetNextMove {Difficulty} finished depth {CompletedDepth}", difficulty.Value, computerMove.CompletedDepth);

        var response = new AutomatedPlayerResponse { Successful = true, NextMove = computerMove.Move };

        return Results.Json(response, ApiJsonOptions.Default);
    }

    // Spec section 6.1.1, item 8: Difficulty is used when it is present. Otherwise Version is used, with the
    // meanings in spec section 2.3. Exactly one of the two results is set.
    private static (Difficulty? Difficulty, string? Error) DifficultyOf(GetNextMove request)
    {
        if (request.Difficulty is Difficulty difficulty)
        {
            return (difficulty, null);
        }

        return request.Version switch
        {
            null => (null, MissingDifficultyMessage),
            1 => (Difficulty.Easy, null),
            2 => (Difficulty.Normal, null),
            3 => (Difficulty.Hard, null),
            _ => (null, RequestReader.InvalidVersionMessage),
        };
    }

    private static IResult BadRequest(string? errorMessage)
    {
        var response = new AutomatedPlayerResponse { Successful = false, ErrorMessage = errorMessage };

        return Results.Json(response, ApiJsonOptions.Default, statusCode: StatusCodes.Status400BadRequest);
    }
}
