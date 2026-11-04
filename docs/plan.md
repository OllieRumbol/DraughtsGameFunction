# Draughts Game Function — Plan

> **What this document is for:** It answers *how will we build it?* It turns the approved requirements in spec.md into a design, and it is agreed before any code is written.

Status: Approved
Last updated: 2026-11-04

---

## 1. Approach

Deliver in four steps, each one safe to ship alone. The first step is the only one with a deadline (spec section 2.4).

| Step | Scope | Spec |
| --- | --- | --- |
| 1. Platform | Port the code to .NET 10 on the isolated worker model with no change to the search. Add CI/CD. Create the new resources in UK South, with the cost limits. Switch the web app's URL, then delete the old resources. | Stage 1 in section 6.1.1. Goals 1 to 3. |
| 2. Move generation | Replace the tree and exception code with one generator. Fix defects 1, 2 and 5. Add the remaining board and `TipFor` validation. | Sections 6.1.2 to 6.1.4 |
| 3. Search | Add one shared search in place of the copied V2 and V3 code, with `Difficulty`. | Stage 2. Section 6.1.5. Goal 5. |
| 4. Levels | Finish the Hard evaluation. Add the self-play tool, tune, and meet the strength targets. | Section 6.4, item 3. Goal 6. |

### 1.1 Options considered

**Search algorithm**

| Option | Pros | Cons |
| --- | --- | --- |
| Alpha-beta, with iterative deepening | Finds the same move as minimax, and visits far fewer positions. Measured gain: 3 to 5 levels in the same time (section 3.6). A time limit is natural: return the last finished depth. | Needs care with ties (section 3.5). |
| Monte Carlo tree search | Needs no evaluation function | Weak on sharp capture sequences, which are the core of draughts. It would not give the clear evaluation-based difference between levels that spec section 6.1.5 asks for. |

Chosen: alpha-beta. It is the established approach for checkers engines, and the prototype measured it directly.

**Board representation**

| Option | Pros | Cons |
| --- | --- | --- |
| Flat `int[64]` | Simple to read and test. Uses the spec's encoding directly. The prototype reached 1.5 to 3 million positions per second with it. | Slower than bitboards |
| Bitboards | Fastest | Much harder to read and maintain. Not needed to meet the requirements. |

Chosen: flat array. The owner's first principle is code that is easy to understand later.

**How to move region**

Chosen: build the new app side by side, then switch the web app's URL (spec section 2.6). The old app keeps serving the game until the switch, and rollback is putting the old URL back.

---

## 2. Configuration

| Setting | Default | Notes |
| --- | --- | --- |
| `Search__NormalMaxDepth` | `6` | Spec section 6.1.5, item 5. Not secret. |
| `Search__NormalTimeLimitMs` | `500` | Spec section 6.4, item 1. Not secret. |
| `Search__HardTimeLimitMs` | `1000` | Spec section 6.4, item 1. Not secret. |
| `AzureWebJobsStorage` | Set by `az functionapp create` | **Secret** (connection string). Stored in the app's settings in Azure. Locally, `UseDevelopmentStorage=true` in the git-ignored `local.settings.json`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Set when Application Insights is linked | Not read locally. Without it, logs go to the `func start` console. |

The `Search` settings bind to a typed options class, validated at startup, with the defaults above in code. So a limit can be tuned in Azure with no deployment, as spec section 6.4, item 1 requires.

The API URL is not a setting of this app. CORS is set on the function app in Azure, and locally in `local.settings.json` under `Host`.

---

## 3. Architecture

### 3.1 Components

The solution keeps its layout and project names. One project is added.

```
DraughtsGameFunction          Host: the two HTTP functions, Program.cs, middleware, JSON handling
DraughtsGameFunctionModels    Request and response models
DraughtsGameFunctionService   Move generation, evaluation, search, and the player services
DraughtsGameFunctionSelfPlay  New. A console tool that plays the levels against each other (spec section 6.4, item 3)
DraughtsGameFunctionTests     MSTest tests
Directory.Build.props         New. Target framework for all projects
Directory.Packages.props      New. All package versions
```

New code in the service project, added in steps 2 to 4:

```
Engine/Board.cs               Converts long[,] to the flat array, and holds the step tables
Engine/Move.cs                A move: from, to, and a bitmask of captured squares
Engine/MoveGenerator.cs       Legal moves, HasMove, and applying a move
Engine/IEvaluator.cs          MaterialEvaluator and PositionalEvaluator implement it
Engine/GameTreeSearch.cs      Alpha-beta, iterative deepening, time limit, ties
Engine/TranspositionTable.cs  Remembers scored positions
Instance/ComputerPlayer.cs    Chooses the move for a difficulty
```

Removed in step 2: `CheckMove`, `Tree`, `KingTree` and `TreeTake`.

Removed in step 3: `AutomatedPlayerServiceV1`, `V2`, `V3`, `IAutomatedPlayerService`, `MinimaxOutcome` and `PotentialNextMove`. This fixes defects 6, 7 and 8 in spec section 4.1.

### 3.2 Existing code to follow or reuse

* `PicktimeAutomation/PicktimeAutomation.AzureFunctions/Program.cs` — host setup: `FunctionsApplication.CreateBuilder`, `ConfigureFunctionsWebApplication`, then middleware.
* `PicktimeAutomation/.../Middleware/ExceptionHandlingMiddleware.cs` — the one place for unexpected errors.
* `PicktimeAutomation/.../Extensions/TelemetryExtensions.cs` — the OpenTelemetry exporter, and the setting that stops it dropping logs.
* `PicktimeAutomation/.../AzureFunctionsTests/TestHttp.cs` — tests that call a function with a plain `DefaultHttpContext`.
* `PicktimeAutomation/.github/workflows/build-and-deploy.yml` — the workflow (section 8.4).
* `DraughtsGameFunctionService/Helpers/PieceComparer.cs` and `PlayerTipsService` — stay as they are.
* `AutomatedPlayerServiceV1` — its choice rule moves to `ComputerPlayer` for Easy (spec section 6.1.5, item 4).
* `AutomatedPlayerServiceV2.Evaluate` — its scoring (man 1, king 2) becomes `MaterialEvaluator`.
* The depth prototype (section 3.6) is not in the repository. Its design for the move generator, the end-of-game check and the search is the starting point for the engine.

### 3.3 Interfaces and contracts

The HTTP contract is spec section 5 and section 6.1.1. The function names and routes do not change: `[Function("GetNextMove")]` and `[Function("GetPotentialMoves")]`, with `Route = null`, keep `/api/GetNextMove` and `/api/GetPotentialMoves`.

Reading a request, used by both functions:

```csharp
internal static class RequestReader
{
    public const int MaximumBodyBytes = 10 * 1024; // spec section 6.4, item 7

    // Reads at most MaximumBodyBytes + 1 bytes, so a chunked body cannot get past the limit.
    // Returns an error message for a body that is too large or cannot be parsed.
    public static async Task<(T? Request, string? Error)> ReadAsync<T>(HttpRequest request, CancellationToken ct);
}
```

The shared JSON options:

```csharp
new JsonSerializerOptions(JsonSerializerDefaults.Web)   // camelCase out, any letter case in
{
    Converters =
    {
        new BoardJsonConverter(),                                 // long[,] from rows, 8 by 8 (spec section 6.1.2, rule 2)
        new JsonStringEnumConverter(allowIntegerValues: false),   // names only (spec section 6.1.1, item 7)
    }
};
```

The engine, from step 2:

```csharp
public readonly record struct Move(int From, int To, ulong Captured);

public static class MoveGenerator
{
    public static void Generate(int[] board, int player, List<Move> moves);
    public static bool HasMove(int[] board, int player);
    public static void Apply(int[] board, Move move);
}
```

`FindMove.FindAvailableMoves(long[,], long)` stays as the public entry point. It calls `MoveGenerator` and converts the result to `List<NextMove>`, so `PlayerTipsService` does not change.

The search, from step 3:

```csharp
public interface IEvaluator
{
    int Evaluate(int[] board, int player);   // Positive is good for this player
    bool UsesQuiescence { get; }
}

public sealed record SearchLimits(int? MaxDepth, TimeSpan? TimeLimit);

public sealed class GameTreeSearch(IEvaluator evaluator, TimeProvider timeProvider, Random? tieBreaker)
{
    // tieBreaker null: the first of the tied moves, for the fixed-depth test mode (spec section 6.1.5, item 9).
    public SearchResult Search(int[] board, int player, SearchLimits limits);
}

public sealed record SearchResult(Move Move, int CompletedDepth, int Score);

public sealed class ComputerPlayer(IOptions<SearchOptions> options, TimeProvider timeProvider, Random random)
{
    // Null when player 2 has no legal move.
    public NextMove? GetNextMove(long[,] board, Difficulty difficulty);
}
```

### 3.4 Data model

There is no stored data. Requests carry the board.

* `GetNextMove` in stage 1 is unchanged. In stage 2 it becomes `Difficulty? Difficulty`, `long? Version` and `long[,] Board`. `Depth` and `Player` are removed.
* `Difficulty` is a new enum in the models project: `Easy`, `Normal`, `Hard`.
* Response models do not change.

Inside the engine, the board is `int[64]`, indexed by `row * 8 + column`, with the values in spec section 2.2. `Move.Captured` is a bitmask. It also stops a piece being captured twice in a chain (spec section 6.1.3, item 5).

### 3.5 Key decisions

| Decision | Choice | Why | Alternatives rejected |
| --- | --- | --- | --- |
| Hosting | Flex Consumption, Linux, UK South | Microsoft's recommended serverless plan, and it runs .NET 10. PickTime chose Windows only because of its timer's time zone setting, which does not apply here. | Windows Consumption is marked legacy. Linux Consumption cannot run .NET 10. |
| Instance size | 512 MB to start | The owner confirmed this on 2026-11-04, knowing it has 0.25 of a CPU core, for the smallest cost. See risk 1 in section 9. The size can be changed with one command. | 2,048 MB is held in reserve. |
| Execution model | Isolated worker with ASP.NET Core integration, returning `IResult` | Spec section 2.4. Same as PickTime. | — |
| Reading the board | A custom `BoardJsonConverter` that reads rows into `long[,]` | `System.Text.Json` cannot read `long[,]` (checked by probe). A converter means no model, service or test has to change in step 1, which keeps the deadline work small. It also rejects boards that are not 8 by 8. Checked by probe: the error's `Path` is `$.Board`. | `long[][]` in the models changes every test and service in the urgent step. Keeping Newtonsoft breaks "one JSON library per project". |
| Error messages | Written by us, chosen by the JSON error's `Path` | The owner's standards forbid sending exception messages to clients. Checked by probe: `Path` is `$.Difficulty` for `3` and `"extreme"`. | Returning `ex.Message`, as the code does today. |
| Web API style | HTTP-triggered functions | The project is a function app | — |
| Ties in the search | At the root, search each move with the window `(best - 1, beta)` | A plain alpha-beta window cuts off a move that only equals the best, and returns a bound, so a tie cannot be seen. One point lower makes an equal move return its exact score. The search then keeps every move with the top score, and the tie breaker picks one (spec section 6.1.5, item 7). | Shuffling the moves first does not choose fairly. Scores within a margin were rejected by the owner. |
| Time limit | Check the clock every 1,024 positions. Abandon the unfinished depth, and use the result of the last finished depth. | A part-finished depth can return a poor move. At the measured speeds, the check runs at least every few milliseconds. | Using the best move so far from an unfinished depth is stronger, but harder to get right. Can be added later. |
| No state between requests | A new search and transposition table for each request | Spec section 6.5, item 8. About 6 MB each, at 2^18 entries. | A shared table needs locking, and different evaluations would pollute it. |
| Quiescence | Hard only. At depth 0, keep searching captures, and allow standing pat because captures are optional (spec section 2.1, item 1). | It stops Hard misjudging a position in the middle of an exchange. Leaving it out of Normal gives Normal natural human-like misjudgements. | Using it for both narrows the gap between levels. |
| Normal evaluation | Man 1, king 2 | Spec section 6.1.5, item 5. Coarse scores give many exact ties, so Normal varies its play. | — |
| Hard evaluation, first values | Man 100, king 200, advancement 3 per row, back-row guard 6, centre 2, plus a bonus for trading pieces when ahead | A king is worth more than in standard draughts, because men cannot capture kings (spec section 2.1, item 4). Values are tuned with the self-play tool. | Mobility is left out at first, because it costs a move generation at each leaf. |
| Testing the clock | `TimeProvider` is injected | A fake clock makes the time-limit test exact, not timing-dependent | — |
| Target framework | `net10.0` in every project, from one file | The owner's standard: one target framework, set in one place | Keeping netstandard2.0 for the libraries has no benefit now. |
| Nullable | On for the host and tests, and for new engine files. Models and old service code follow as they are rewritten. | Keeps step 1 small | Fixing every warning in the urgent step |
| API version naming | `Version` stays as a fallback until stage 3 | Spec section 6.1.1, item 5 | — |

No ADR is needed. These decisions stay inside this project, and the reasons are recorded above.

### 3.6 Measured basis

From a throwaway prototype on 2026-11-03, on 48 positions from random games, on the owner's machine with .NET 10. Average depth finished, for the opening and early middle game:

| Search | 50 ms | 100 ms | 500 ms | 1,000 ms |
| --- | --- | --- | --- | --- |
| Current code | 3.5–3.6 | 4.0–4.1 | 4.9–5.0 | 5.0 |
| Alpha-beta, best moves first | 6.9–7.4 | 7.3–7.6 | 8.5–8.8 | 9.2–9.6 |
| Plus the transposition table | 7.8–8.9 | 8.3–9.3 | 9.9–10.7 | 10.3–11.3 |

So the transposition table is included from the start. The prototype did not include quiescence, which will use some of the time.

**Speed on Azure is not yet known.** The figures above do not transfer directly:
* The live app's 0.98 s at depth 5 compares with 388 ms locally on .NET 6, so the old plan is about 2.5 times slower than this machine, including network time.
* A 512 MB Flex instance has 0.25 of a core, which may be several times slower again.

Section 7.3 measures it on the real app.

---

## 4. Error handling

| Situation | Behaviour |
| --- | --- |
| Body larger than 10 KB | 400, `The request body must be 10 KB or smaller.` |
| Body is not JSON | 400, `The request body is not valid JSON.` |
| JSON error with path `$.Board`, or `Board` missing | 400, `Board must be 8 rows of 8 values.` |
| JSON error with path `$.Difficulty` | 400, `Difficulty must be one of: easy, normal, hard` |
| Unknown `Version`, stage 1 | 400, `No version in body of request`, as today (spec section 5.1) |
| Neither `Difficulty` nor `Version`, stage 2 | 400, `Difficulty is missing. It must be one of: easy, normal, hard` |
| Invalid `Version`, stage 2 | 400, `Version must be 1, 2 or 3.` |
| Board value outside 0 to 6, from step 2 | 400, `Board values must be from 0 to 6.` |
| `TipFor` not 1 or 2, from step 2 | 400, `TipFor must be 1 or 2.` |
| Player 2 has no legal move, from step 2 | 400, `Player 2 has no legal moves.` |
| Player 2 has no legal move, in step 1 only | The old code throws a null reference, so the middleware returns 500, not today's 400. The web app shows the same general message for both. Accepted for this short period. |
| Time limit reached during a search | Not an error. Returns the best move from the last finished depth. |
| Time limit reached before depth 1 finishes | Not an error. Returns the first legal move. Only possible on an extremely overloaded instance. |
| Any other exception | The middleware logs it and returns 500 with `successful: false` and `An unexpected error occurred.` The body keeps the same shape as other responses, so the web app handles it normally. |
| The caller cancels | Not treated as an error (same as PickTime). The search itself is short and CPU-only, so it does not check the token. |

All 400 and 500 responses use the existing response models (spec section 6.3).

---

## 5. Observability

As spec section 6.4, item 6 requires, and no more.

* A small middleware logs one line for each request, at Information level, from `RequestLoggingMiddleware` only: `"{Route} returned {StatusCode} in {DurationMs} ms"`. PickTime showed that named placeholders reach `customDimensions`.
* All other categories log at Warning and above. Set in `host.json`.
* Errors are logged by the exception middleware.
* The exporter's limit of 5 traces per second is turned off, as in PickTime, or the response-time check would lose records.
* The workspace's daily cap is 0.1 GB (section 8.1).

To check the response times (spec section 6.4, item 1), in Logs:

```kusto
traces
| where message has "returned"
| extend Route = tostring(customDimensions.Route), DurationMs = toreal(customDimensions.DurationMs)
| summarize Requests = count(), P95 = percentile(DurationMs, 95) by Route
```

The difficulty is not logged by default. The check in section 7.3 runs each level in its own time window.

---

## 6. Security

| Item | Decision |
| --- | --- |
| Authentication | Anonymous, as spec section 5.3. The computer's moves and the rules are public. |
| CORS | `https://ollierumbol.github.io` only, set on the function app. Blocks other sites in a browser, but not direct calls, which is why the cost limits exist. |
| Deployment | OpenID Connect through a user-assigned managed identity. No stored secret. The ids are repository variables. |
| Deployment rights | Website Contributor on the function app only, if that is enough for a Flex deployment (risk 6 in section 9) |
| Automatic stop rights | The Logic App's system-assigned identity has Website Contributor on the function app only |
| Storage account key | The default connection string in app settings. It is not in the repository. The account holds only the deployment package and host data. Moving to identity-based access is a possible later improvement. |
| Request size | 10 KB, checked while reading, not from the `Content-Length` header |
| Vulnerable packages | NuGet Audit warnings are errors in CI |
| Error messages | No exception text is sent to clients (section 4) |
| Logs | No board or player data is logged |
| Public repository | The Logic App's trigger URL contains a key. It is stored only in the action group. Do not paste it into this repository. |

---

## 7. Testing

### 7.1 Principles

* MSTest, as the project uses today, on the current version. PickTime's versions are the starting point.
* Tests call the functions directly with a `DefaultHttpContext`, and read the JSON that is written (`TestHttp`, from PickTime). The isolated host has no in-memory test server.
* Every worked example in spec section 6.2 is an automated test.
* The existing tests stay. In step 2, `CheckMoveTests` tests classes that are removed, so each case moves to `FindMoveTests` with the same board and the same expected move. In step 3, the `Evaluate` and `GetAvailableBoards` tests move the same way.
* Tests do not call real Azure.
* A fake `TimeProvider` tests the time limit, so the test does not depend on the machine's speed.

### 7.2 Tests

| # | Test | Spec | Defect it catches |
| --- | --- | --- | --- |
| 1 | The web app's exact request (spec section 5.1, PascalCase, with `Depth` and `Player`) returns 200 and camelCase fields | 5.1, 6.1.1 | The port changes the field names or casing |
| 2 | The tips request returns the four squares in spec section 5.2 | 5.2, 6.2 #1 | Same, for tips |
| 3 | Unknown `Version` returns 400, with `successful: false` | 5.1, 6.2 #6 | Error response shape changes |
| 4 | Board of 7 rows, and rows of uneven length, return 400 | 6.1.2 rule 2, 6.2 #8 | Uneven boards get into the move code |
| 5 | A body over 10 KB returns 400, with and without a `Content-Length` header | 6.1.2 rule 3 | The limit relies on a header that a client can omit |
| 6 | Unexpected exception gives 500, a generic message and no exception text | 6.3 | Internal detail leaks to clients |
| 7 | Search options bind and validate from configuration | 6.4 item 1 | A bad setting is only found during a game |
| 8 | Worked examples 3, 4, 17, 18 give exactly the listed moves and captures | 6.2, 6.1.3 | Defect 1, and any change to the house rules |
| 9 | A king chain that could loop back captures no piece twice | 6.1.3 item 5, 6.5 #7 | Double capture |
| 10 | A man reaching the far row ends its chain and is promoted | 6.1.3 item 7 | Chain carries on as a king |
| 11 | Value 6 squares behave as empty | 6.2 #9 | Highlighted squares block moves |
| 12 | Old and new generators agree on a fixed set of positions, except for the known defects. Run once, in step 2, before the old code is removed. | 6.1.3 | A rule lost in the rewrite |
| 13 | Search at fixed depth gives the same move as plain minimax on a fixed set of positions | 6.1.5 | A pruning bug |
| 14 | A position where two moves tie exactly returns both over repeated runs. A lower-scoring move is never returned. | 6.1.5 item 7 | Alpha-beta hides ties |
| 15 | Example 16: 20 Normal moves from the start give at least 2 different moves | 6.2 #16 | Defect 6 |
| 16 | Fake clock passes the limit: the search stops, and returns the last finished depth's move | 6.1.5 item 8 | Search overruns |
| 17 | Depth is capped at 6 for Normal, with no cap for Hard, read from `CompletedDepth` | 6.1.5 items 5, 6 | Normal is too strong |
| 18 | One legal move returns with no search | 6.2 #15 | Wasted time |
| 19 | A forced win in 1 is chosen over a forced win in 3. The slowest loss is chosen when every move loses. | 6.1.5 item 7 | Search misses or delays a win |
| 20 | Both end-of-game rules at a leaf, including a move that leaves the mover with no moves | 2.1 item 6, 6.5 #9 | AI disagrees with the screen |
| 21 | Examples 10 to 14: `Difficulty` letter case, numbers rejected, both fields, neither field | 6.2 | Contract errors |
| 22 | Example 7: `Depth: 50` has no effect | 6.2 #7, defect 3 | Client controls the cost |
| 23 | Easy always takes a capture when one exists | 6.1.5 item 4 | Easy loses its one rule |
| 24 | One self-play game at small depth runs to an end | 6.4 item 3 | The tool hangs or the draw rule fails |
| 25 | Example 5: no legal move for player 2 returns 400, and the message says so | 6.2 #5 | Defect 2 |
| 26 | From stage 2, a request with `Player: 1` at each level still returns a player 2 move | 6.1.5 item 1, 6.5 #5 | Defect 4 |
| 27 | Example 2: `Difficulty: "normal"` returns a legal player 2 move from row 2 to row 3 | 6.2 #2 | The new contract fails |
| 28 | A board value of 7, and `TipFor: 3`, return 400 | 6.1.2 rules 4, 5 | Bad input reaches the engine |

### 7.3 Checks on the live app

Run by hand, with results in the pull request.

1. **Contract.** Replay the requests in spec section 5 against the new URL, and compare with the captured responses.
2. **Full game.** Play a full game at each level in the web app.
3. **Speed.** Spec section 6.4, item 1. A short script sends the warm-up, then 30 requests for each level from fixed positions, with a pause between levels. It also records `CompletedDepth` while a temporary log line is on, so we learn the real depth on Azure. The line is removed afterwards (spec section 6.4, item 6).
4. **Concurrency.** Send 12 requests at once to see how the single instance behaves (risk 2).

### 7.4 Strength

The `DraughtsGameFunctionSelfPlay` tool applies the rules in spec section 6.4, item 3. It is run by hand, not in CI, because of the time it takes.

```bash
dotnet run --project DraughtsGameFunctionSelfPlay -c Release -- --games 200 --time-scale 0.1
```

* 200 games for each pairing: Hard against Normal, and Normal against Easy. The margin of error near 80% is about 5.5 points either way, at 95% confidence.
* `--time-scale` shortens the time limits to match the speed measured on Azure, in section 7.3. At the real limits, 200 games take about 2 hours. At 0.1, about 12 minutes. The value 0.1 is a placeholder until that measurement.
* Each game opens with 4 random moves.
* The first thing to try if Hard falls short is a lower Normal depth (decided with the owner). After that, a small margin in Normal's choice.

---

## 8. Infrastructure and deployment

### 8.1 Azure resources

All in UK South (spec section 6.4, item 4). Names are proposals, and the function app name must be free across Azure.

| Resource | Name | Plan | Free allowance | Expected usage |
| --- | --- | --- | --- | --- |
| Resource group | `rg-draughts-game` | — | No charge | — |
| Function app and plan | `func-draughts-game` | Flex Consumption, on demand, 512 MB, maximum 1 instance, no always-ready instances | 250,000 executions and 100,000 GB-s a month, per subscription, shared with PickTime | 0.5 GB-s a request at the 1 s billing minimum. The grant covers about 200,000 requests, or about 3,000 games. |
| Storage account | `stdraughtsgame` | Standard LRS | None | About £0.10 a month. Approved by the owner on 2026-11-02. |
| Application Insights | `appi-draughts-game` | Workspace-based | No charge itself | — |
| Log Analytics workspace | `log-draughts-game` | Per GB, with a daily cap of 0.1 GB | 5 GB a month, per billing account | About 1 KB a request |
| Managed identity | `id-draughts-game-deploy` | User-assigned | No charge | — |
| Budget, alert | `budget-alert` | £1 a month, on the resource group, actual and forecast | Budgets are free | Email only |
| Budget, stop | `budget-stop` | £3 a month, on the resource group | Budgets are free | Calls the action group below |
| Action group | `ag-draughts-game-stop` | — | Check when created | Runs only if the £3 budget is reached |
| Logic App | `la-draughts-game-stop` | Consumption | First 4,000 actions free | 2 actions a run. Metered per run, so nothing while idle. |

Figures were checked on 2026-11-02 and 2026-11-04, on the Azure pricing pages. The Log Analytics and budget figures come from PickTime's plan, checked on 2026-10-06. Check them again when creating the resources.

**Worst case with the limits off.** One 512 MB instance, busy for a whole month, is about 1.3 million GB-s. At the example rate in Microsoft's docs ($0.000026 per GB-s), that is about $30 after the grant. The £3 stop acts long before that.

### 8.2 Creating the resources

Created once, by hand, with the Azure CLI, as PickTime does. The pipeline only deploys code, so its identity stays narrow. These commands are the record. They need the owner's `az login`, and they change a real subscription.

```bash
az group create -n rg-draughts-game -l uksouth
az storage account create -n stdraughtsgame -g rg-draughts-game -l uksouth --sku Standard_LRS
az functionapp create -n func-draughts-game -g rg-draughts-game --storage-account stdraughtsgame --flexconsumption-location uksouth --runtime dotnet-isolated --runtime-version 10.0 --instance-memory 512 --maximum-instance-count 1
az functionapp scale config set -n func-draughts-game -g rg-draughts-game --trigger-type http --trigger-settings perInstanceConcurrency=8
az functionapp cors add -n func-draughts-game -g rg-draughts-game --allowed-origins https://ollierumbol.github.io
```

The higher concurrency (the default at 512 MB is 4) is on purpose. With one instance, a request above the limit may wait or be rejected. A search that shares the CPU still stops on time, at a lower depth, so sharing is better than rejecting. Section 7.3 tests this.

Application Insights, the workspace with its daily cap, and the identity with its federated credential follow PickTime's steps. The federated credential subject is `repo:OllieRumbol/DraughtsGameFunction:ref:refs/heads/master`.

### 8.3 Automatic stop

As spec section 6.4, item 5 requires. No code is added to the app.

1. `budget-stop` reaches 100% of £3.
2. It calls `ag-draughts-game-stop`.
3. The action group calls the Logic App's HTTP trigger.
4. The Logic App sends one `POST` to Azure Resource Manager, to the app's `/stop` endpoint, signed in with its own identity. It then ends.

To restart: `az functionapp start -n func-draughts-game -g rg-draughts-game`.

This is tested once by running the Logic App by hand, confirming that the app stops and the web app shows its general message, and then restarting. The budget cannot be tested without spending £3, so the link from the budget to the action group is checked with the portal's configuration view. Budget data can lag by several hours (spec section 2.6).

A runbook in Azure Automation was rejected: it needs an extra account and script for what is one HTTP call.

### 8.4 CI/CD

One workflow, `.github/workflows/build-and-deploy.yml`, copied from PickTime, with these differences:

| Setting | PickTime | Here | Why |
| --- | --- | --- | --- |
| Branch | `main` | `master` | The repository's default branch |
| Runner | `windows-latest` | `ubuntu-latest` | Matches Flex Consumption's Linux |
| Solution | `.slnx` | `DraughtsGameFunction.sln` | Existing file |

Unchanged from PickTime: build and test on every pull request, NuGet Audit warnings as errors, deploy only on a push to the branch, `azure/login` with OpenID Connect, `Azure/functions-action`, and no `environment:` on the deploy job. Microsoft's docs confirm that Flex Consumption uses package deployment through this action, and that `sku` is only needed with a publish profile. The `include-hidden-files` setting on the artifact stays, because the isolated worker needs `.azurefunctions`.

**Rollback.** Flex Consumption has no slots and keeps no previous package. Revert the commit on `master`, and the pipeline deploys the previous version.

### 8.5 Switching over

The web app's code has been unchanged since its last release in March 2022 (the `gh-pages` branch), so its release path has not been used for years. The owner chose to release by hand, because nothing shows that the old workflow still works.

**Build test, 2026-11-04.** A fresh clone of `main`, on Node 24.19.0, with nothing pushed:

* `npm ci` fails, because the lock file is out of step with `package.json`. `npm install` works.
* All 7 tests pass.
* `npm run build` fails with `ERR_OSSL_EVP_UNSUPPORTED`. It passes with `NODE_OPTIONS=--openssl-legacy-provider`.
* The JavaScript and CSS match the live `gh-pages` branch, ignoring line endings and source map lines. So a release changes only the two URLs.
* Not tested: the publish step, because it pushes to `gh-pages`.

**Steps:**

1. The new app passes the checks in section 7.3, steps 1 and 2, with the URL called directly.
2. On a new branch from `origin/main` of `draughtsgame`, change the two URLs in `src/logic/api.js`, and merge it to `main`. The owner has uncommitted work on another branch there, which stays untouched.
3. Ask the owner, then release from a clean copy of `main`, with `npm install`, then `npm run deploy` with `NODE_OPTIONS=--openssl-legacy-provider` set. The owner's git login on this machine does the push.
4. Watch the new app's logs, and play a game.
5. After a few days with no problems, ask the owner to confirm, then delete `DraughtsGameFunction20210321121511ResourceGroup` (spec section 9). Rollback until then: restore the old URL.

---

## 9. Risks

| # | Risk | Likelihood | Mitigation |
| --- | --- | --- | --- |
| 1 | 512 MB has 0.25 of a core, so the old search in step 1 is slow, or Hard reaches too little depth later. Depth 5 could take longer than today in step 1, even with .NET 10's 3 times gain. | Medium | Measure it in step 1 (section 7.3). The time limit protects response times from step 3. If it is too slow, moving to 2,048 MB (1 core) is one command. It lowers the free capacity to about 50,000 requests, or about 800 games a month, and it needs the owner's approval. |
| 2 | Microsoft warns that a maximum below 40 can reject requests when traffic exceeds capacity. | Low for a hobby game | Higher per-instance concurrency, and the test in section 7.3. If needed, raise the maximum instance count with the owner's approval. |
| 3 | The web app's old workflow also runs when the URL change reaches `main`. It uses Node 12 and old actions, its `TOKEN` secret dates from January 2021, and no runs are listed for it. It may fail. | High | The release does not depend on it (section 8.5). A failed run stops before it publishes anything. A successful run publishes the same code. Fixing the workflow belongs to the frontend spec. |
| 4 | A Flex regional quota in UK South blocks creation, as a Y1 quota did for PickTime. | Low | A quota request is free. Start the resources early, not on the deadline. |
| 5 | The automatic stop fails silently, because the budget cannot be fully tested. | Low | The £1 email comes first, and the single instance limits the damage. Test the Logic App by hand (section 8.3). |
| 6 | Website Contributor may not be enough for a Flex package deployment. Microsoft's docs name this role for other plans, but not clearly for Flex. | Medium | Find out on the first deployment. If it fails, use the narrowest role that works, on this app only, and record it. |
| 7 | The 10 November date slips. | Medium | Step 1 is kept small. The old app should keep running, unsupported. |
| 8 | Normal at depth 6 is too strong for casual players, or Hard misses the 80% target. | Medium | Depth is a setting. Self-play and the owner's own games decide. |
| 9 | Move generation changes the moves the web app expects. The web app has its own move validation and does not call this API to check a player's move. | Low | Test 12 compares old and new. The only intended differences are the defect fixes. The king capture fix means tips can show a move the web app's own code already allows. |
| 10 | The transposition table returns a wrong score after a hash collision. | Low | 64-bit keys compared in full. The chosen move is still checked against the legal moves at the root. |
| 11 | The local tests and the Linux runner differ. | Low | Tests run in CI on Linux, and locally on Windows. Project references use the existing casing. |
