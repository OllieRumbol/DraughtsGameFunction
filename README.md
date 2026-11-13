# Draughts Game Function

The API behind the [Draughts Game](https://ollierumbol.github.io/draughtsgame) web app. It does two jobs:

1. It plays as the computer opponent, at three levels.
2. It tells the web app which squares a player can move to, so the web app can highlight them.

It is an Azure Functions app on .NET 10. The game uses the owner's family house rules ([spec section 2.1](docs/spec.md#21-house-rules)). They are fixed: do not change them to standard English draughts.

## Endpoints

Both endpoints take a `POST` with a JSON body. `Board` is 8 rows of 8 values, encoded as in [spec section 2.2](docs/spec.md#22-board-encoding).

| Endpoint | Request | Response |
| --- | --- | --- |
| `/api/GetNextMove` | `{ "Difficulty": "hard", "Board": [...] }` | The computer's move, as player 2 |
| `/api/GetPotentialMoves` | `{ "Board": [...], "TipFor": 1 }` | The squares that the `TipFor` player (1 or 2) can move to |

`Difficulty` is `easy`, `normal` or `hard`, in any letter case. The web app still sends the older `Version` field (1, 2 or 3), which is used only when `Difficulty` is missing. It also sends `Depth` and `Player`, which have no effect. [Spec section 5](docs/spec.md#5-external-interfaces) has the full contract.

Both endpoints are anonymous. The live app accepts browser requests from the web app's origin only (CORS).

## Levels

| Level | How it plays |
| --- | --- |
| Easy | A random capture when there is one, otherwise a random move. No search. |
| Normal | Searches up to 6 moves ahead, for at most 500 ms, and counts material only: a man is 1 and a king is 2. |
| Hard | Searches as deep as it can in 1,000 ms, and scores where the pieces stand as well as material. |

The depth and time limits are app settings with defaults in code ([plan section 2](docs/plan.md#2-configuration)).

## Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet test DraughtsGameFunction.sln
```

## Run locally

You also need [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) version 4.

1. Create `DraughtsGameFunction/local.settings.json`. Git ignores this file.

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
     }
   }
   ```

2. Start the app from its own folder. If you run `dotnet run` from the repository root, it starts the Functions host in the wrong folder and fails.

   ```bash
   cd DraughtsGameFunction
   dotnet run
   ```

3. In another terminal, ask for the computer's move from the starting board:

   ```bash
   curl -X POST http://localhost:7071/api/GetNextMove -H "Content-Type: application/json" -d '{"Difficulty":"normal","Board":[[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]}'
   ```

The host needs the `AzureWebJobsStorage` setting, but the HTTP functions do not use storage, so no storage emulator needs to run.

## Self-play

`DraughtsGameFunctionSelfPlay` plays the levels against each other, to check that each level is clearly stronger than the one below it ([plan section 7.4](docs/plan.md#74-strength)):

```bash
dotnet run --project DraughtsGameFunctionSelfPlay -c Release -- --games 200 --time-scale 0.35
```

`--time-scale 0.35` shortens the time limits so that the owner's machine searches as deep as the live app. On that machine, a full run took about 25 minutes. On another machine, both can differ. For a quick check, use `--games 2`.

## Deployment

GitHub Actions runs the build and the tests on every pull request. A merge to `master` builds, tests and deploys the app to Azure. A failing test stops the deployment. `master` is protected, so every change goes through a pull request.

## Projects

| Project | Contents |
| --- | --- |
| `DraughtsGameFunction` | The host: the two HTTP functions, middleware and JSON handling |
| `DraughtsGameFunctionModels` | Request and response models, and the search settings |
| `DraughtsGameFunctionService` | Move generation, evaluation, search, and the computer player |
| `DraughtsGameFunctionSelfPlay` | The self-play tool |
| `DraughtsGameFunctionTests` | MSTest tests |

## Documents

* [docs/spec.md](docs/spec.md): what the API does, and why.
* [docs/plan.md](docs/plan.md): how it is built.
* [docs/tasks.md](docs/tasks.md): the work, step by step.
