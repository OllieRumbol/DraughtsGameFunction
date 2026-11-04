using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Instance;
using DraughtsGameFunctionService.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace DraughtsGameFunction;

public static class PlayerTips
{
    [Function("GetPotentialMoves")]
    public static async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
        CancellationToken ct)
    {
        var (getPlayerTips, readError) = await RequestReader.ReadAsync<GetPlayerTips>(request, ct);
        if (getPlayerTips is null)
        {
            return BadRequest(readError);
        }

        if (getPlayerTips.Board is null)
        {
            return BadRequest(RequestReader.InvalidBoardMessage);
        }

        IPlayerTipsService service = new PlayerTipsService();
        List<Piece> potentialMoves = service.GetPotentialMoves(getPlayerTips);
        var response = new PlayersTipsResponse { Successful = true, PotentialMoves = potentialMoves };

        return Results.Json(response, ApiJsonOptions.Default);
    }

    private static IResult BadRequest(string? errorMessage)
    {
        var response = new PlayersTipsResponse { Successful = false, ErrorMessage = errorMessage };

        return Results.Json(response, ApiJsonOptions.Default, statusCode: StatusCodes.Status400BadRequest);
    }
}
