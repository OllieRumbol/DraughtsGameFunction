using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace DraughtsGameFunction;

/// <summary>
/// GetPotentialMoves: the squares a player can move to (spec section 6.1.4). The request does not change.
/// </summary>
public sealed class PlayerTips(IPlayerTipsService playerTipsService)
{
    [Function("GetPotentialMoves")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
        CancellationToken ct)
    {
        var (getPlayerTips, readError) = await RequestReader.ReadAsync<GetPlayerTips>(request, ct);
        if (getPlayerTips is null)
        {
            return BadRequest(readError);
        }

        var boardError = RequestReader.BoardError(getPlayerTips.Board);
        if (boardError is not null)
        {
            return BadRequest(boardError);
        }

        // Spec section 6.1.2, rule 5. A missing TipFor reads as 0.
        if (getPlayerTips.TipFor is not (1 or 2))
        {
            return BadRequest(RequestReader.InvalidTipForMessage);
        }

        List<Piece> potentialMoves = playerTipsService.GetPotentialMoves(getPlayerTips);
        var response = new PlayersTipsResponse { Successful = true, PotentialMoves = potentialMoves };

        return Results.Json(response, ApiJsonOptions.Default);
    }

    private static IResult BadRequest(string? errorMessage)
    {
        var response = new PlayersTipsResponse { Successful = false, ErrorMessage = errorMessage };

        return Results.Json(response, ApiJsonOptions.Default, statusCode: StatusCodes.Status400BadRequest);
    }
}
