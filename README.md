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
| `/api/GetPotentialMoves` | `{ "Board": [...], "TipFor": 1 }` | The squares that player can move to |

`Difficulty` is `easy`, `normal` or `hard`, in any letter case. The web app still sends the older `Version` field (1, 2 or 3), which is used when `Difficulty` is missing. [Spec section 5](docs/spec.md#5-external-interfaces) has the full contract.

## Levels

| Level | How it plays |
| --- | --- |
| Easy | A random capture when there is one, otherwise a random move. No search. |
| Normal | Searches up to 6 moves ahead, for at most 500 ms, and counts pieces only. |
| Hard | Searches as deep as it can in 1,000 ms, and also judges the position. |

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

2. Start the app from its own folder. Run from the repository root, the Functions tools look for the build output in the wrong folder.

   ```bash
   cd DraughtsGameFunction
   dotnet run
   ```

3. Send a request to `http://localhost:7071/api/GetPotentialMoves` or `http://localhost:7071/api/GetNextMove`.

The HTTP functions work without a storage emulator.

## Self-play

`DraughtsGameFunctionSelfPlay` plays the levels against each other, to check that each level is clearly stronger than the one below it ([plan section 7.4](docs/plan.md#74-strength)):

```bash
dotnet run --project DraughtsGameFunctionSelfPlay -c Release -- --games 200 --time-scale 0.35
```

`--time-scale 0.35` shortens the time limits to match the speed of the live app. A full run takes about 25 minutes. For a quick check, use `--games 2`.

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
