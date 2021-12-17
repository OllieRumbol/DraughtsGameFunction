using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Interface;
using DraughtsGameFunctionService.Intstance;

namespace DraughtsGameFunction
{
    public static class AutomatedPlayer
    {
        [FunctionName("GetNextMove")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, ILogger log)
        {
            try
            {
                String requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                GetNextMove getNextMove = JObject.Parse(requestBody).ToObject<GetNextMove>();

                Int64 version = getNextMove.Version;
                IAutomatedPlayerService service;

                switch (version)
                {
                    case 1:
                        service = new AutomatedPlayerServiceV1();
                        break;
                    case 2:
                        service = new AutomatedPlayerServiceV2();
                        break;
                    case 3:
                        service = new AutomatedPlayerServiceV3();
                        break;
                    default:
                        service = null;
                        break;
                }

                if (service == null)
                {
                    return new BadRequestObjectResult(
                        new AutomatedPlayerResponse
                        {
                            Successful = false,
                            ErrorMessage = "No version in body of request"
                        }
                    );
                }

                NextMove nextmove = service.GetNextMoveForAutomatedPlayer(getNextMove);
                return new OkObjectResult(
                    new AutomatedPlayerResponse
                    {
                        Successful = true,
                        NextMove = nextmove
                    }
                );
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(
                    new AutomatedPlayerResponse
                    {
                        Successful = false,
                        ErrorMessage = ex.Message
                    }
                );
            }
        }
    }
}
