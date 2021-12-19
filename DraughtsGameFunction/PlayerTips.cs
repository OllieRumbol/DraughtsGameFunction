using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DraughtsGameFunctionModels.Controller;
using Newtonsoft.Json.Linq;
using DraughtsGameFunctionService.Interface;
using DraughtsGameFunctionService.Instance;
using System.Collections.Generic;
using DraughtsGameFunctionModels.Service;

namespace DraughtsGameFunction
{
    public static class PlayerTips
    {
        [FunctionName("GetPotentialMoves")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            try
            {
                String requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                GetPlayerTips getPlayerTips = JObject.Parse(requestBody).ToObject<GetPlayerTips>();

                IPlayerTipsService service = new PlayerTipsService();
                List<Piece> potentialMoves = service.GetPotentialMoves(getPlayerTips);

                return new OkObjectResult(
                    new PlayersTipsResponse
                    {
                        Successful = true,
                        PotentialMoves = potentialMoves
                    }
                );
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(
                    new PlayersTipsResponse
                    {
                        Successful = false,
                        ErrorMessage = ex.Message
                    }
                );
            }
        }
    }
}