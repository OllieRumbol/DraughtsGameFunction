# Draughts Game Function — Plan

> **What this document is for:** It answers *how will we build it?* It turns the approved requirements in spec.md into a design, and it is agreed before any code is written.

Status: Approved
Last updated: 2026-11-06

---

## 1. Approach

Deliver in four steps, each one safe to ship alone. The first step is the only one with a deadline (spec section 2.4).

| Step | Scope | Spec |
| --- | --- | --- |
| 1. Platform | Port the code to .NET 10 on the isolated worker model with no change to the search. Add CI/CD. Create the new resources in UK South, with the cost limits. Switch the web app's URL, then delete the old resources. | Stage 1 in section 6.1.1. Goals 1 to 3. |
| 2. Move generation | Replace the tree and exception code with one generator. Fix defects 1, 2, 5, 9 and 10. Add the remaining board and `TipFor` validation. | Sections 6.1.2 to 6.1.4 |
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
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | Local only, in the git-ignored `local.settings.json`. Flex Consumption [deprecates this setting](https://learn.microsoft.com/azure/azure-functions/functions-app-settings#flex-consumption-plan-deprecations), and uses `functionAppConfig.runtime` instead. |
| `DEPLOYMENT_STORAGE_CONNECTION_STRING` | Set by `az functionapp create`, if the app's deployment storage uses a connection string | **Secret**, if present. Confirm it when the app is created. |

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

In the PicktimeAutomation repository, under `PicktimeAutomation/PicktimeAutomation.AzureFunctions`:

* `Program.cs` — host setup: `FunctionsApplication.CreateBuilder`, `ConfigureFunctionsWebApplication`, then middleware.
* `Middleware/ExceptionHandlingMiddleware.cs` — the one place for unexpected errors.
* `Extensions/TelemetryExtensions.cs` — the OpenTelemetry exporter, and the setting that stops it dropping logs.
* `host.json` and `appsettings.json` — where each log setting goes (section 5).

Also in the PicktimeAutomation repository:

* `TestHttp.cs` in the Functions test project — tests that call a function with a plain `DefaultHttpContext`.
* `.github/workflows/build-and-deploy.yml` — the workflow (section 8.4).

In this repository:

* `DraughtsGameFunctionService/Helpers/PieceComparer.cs` — stays as it is.
* `AutomatedPlayerServiceV1` — its choice rule moves to `ComputerPlayer` for Easy (spec section 6.1.5, item 4).
* `AutomatedPlayerServiceV2.Evaluate` — its scoring (man 1, king 2) becomes `MaterialEvaluator`.

The depth prototype (section 3.6) is not in the repository. Section 3.7 records the design it tested.

### 3.3 Interfaces and contracts

The HTTP contract is spec section 5 and section 6.1.1. The function names and routes do not change: `[Function("GetNextMove")]` and `[Function("GetPotentialMoves")]`, with `Route = null`, keep `/api/GetNextMove` and `/api/GetPotentialMoves`.

Reading a request, used by both functions:

```csharp
internal static class RequestReader
{
    public const int MaximumBodyBytes = 10 * 1024; // spec section 6.4, item 7

    // Reads at most MaximumBodyBytes + 1 bytes, so a chunked body cannot get past the limit.
    // Returns an error message for a body that is too large, cannot be parsed, or is the JSON value null.
    // So exactly one of the two results is set.
    public static async Task<(T? Request, string? Error)> ReadAsync<T>(HttpRequest request, CancellationToken ct);
}
```

The shared JSON options:

```csharp
new JsonSerializerOptions(JsonSerializerDefaults.Web)   // camelCase out, any letter case in
{
    NumberHandling = JsonNumberHandling.Strict,  // from step 3: the web defaults also read "2" as a number (spec section 6.1.1, item 8)
    Converters =
    {
        new BoardJsonConverter(),       // long[,] from rows, 8 by 8 (spec section 6.1.2, rule 2)
        new DifficultyJsonConverter(),  // the three names only, in any letter case (spec section 6.1.1, item 7)
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

* `FindMove.FindAvailableMoves(long[,], long)` stays as the public entry point. It calls `MoveGenerator` and converts the result to `List<NextMove>`.
* `Generate` treats the moving king's start square as empty during a chain, and returns each distinct move once (spec section 6.1.3, items 8 and 9).
* `Apply` clears `From` before it writes `To`, so a move that ends where it began keeps the piece.
* `PlayerTipsService` leaves out a move that ends where it began (spec section 6.1.4, item 4). This is its only change.

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
    // tieBreaker null: of the tied moves, the first in generator order, for the fixed-depth test mode (spec section 6.1.5, item 9).
    public SearchResult Search(int[] board, int player, SearchLimits limits);
}

public sealed record SearchResult(Move Move, int CompletedDepth, int Score);

public sealed record ComputerMove(NextMove Move, int CompletedDepth); // CompletedDepth is 0 when no search ran

public sealed class ComputerPlayer(IOptions<SearchOptions> options, TimeProvider timeProvider, Random random)
{
    // Null when the player has no legal move. The function always passes player 2. Self-play passes either player.
    public ComputerMove? GetNextMove(long[,] board, int player, Difficulty difficulty);
}
```

In production, `Random` is `Random.Shared`, because requests run at the same time (spec section 6.5, item 8) and a plain `Random` is not thread-safe. Tests pass a seeded `Random`.

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
| Error messages | Written by us, chosen by the JSON error's `Path`, compared ignoring letter case | The owner's standards forbid sending exception messages to clients. Checked by probe: `Path` is `$.Difficulty` for `3` and `"extreme"`, and it follows the client's letter case (`$.difficulty`). Also checked by probe on .NET 10: an inner `FormatException` or `InvalidOperationException` means a wrong value at that path, so it gets that path's message. Only a reader error means broken JSON. | Returning `ex.Message`, as the code does today. |
| Reading `Difficulty` | A small `DifficultyJsonConverter` that accepts only the three names | Checked by probe: `JsonStringEnumConverter` also accepts `"easy,hard"` and `" hard "`, which spec section 6.1.1, item 7 does not allow. | `JsonStringEnumConverter(allowIntegerValues: false)` |
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

### 3.7 Search design

`GameTreeSearch` is a negamax alpha-beta search, as the prototype tested it. A move is one ply, and a capture chain is one move.

1. **End of game first.** At each node, check both players (spec section 2.1, item 6). Neither can move scores 0. A win scores `Win - ply` and a loss scores `-Win + ply`, with `Win` at 100,000, so a faster win scores higher. Evaluation scores stay well below `Win`.
2. **Depth 0.** Return `Evaluate`, or run quiescence if `UsesQuiescence` is true.
3. **Table lookup.** See below.
4. **Move order.** The table's best move first, then more captures before fewer, then generator order.
5. **Root.** Each iteration searches the root moves with the window in section 3.5, and puts the last depth's best move first. Ties are decided by generator order, not by search order, so reordering does not change the fixed-depth result.
6. **Stopping.** Stop when the time limit passes (section 3.5), at the maximum depth, or when a finished depth finds a forced win.

**Transposition table.**
* It has 2^18 entries. Each holds the full 64-bit key, the score, the remaining depth, the bound (exact, lower or upper) and the best move. The slot is always replaced.
* The key is a Zobrist hash of the pieces and the side to move. Values 5 and 6 hash the same, as both are empty.
* A win or loss score is stored relative to the node, not the root: add the ply on the way in, and subtract it on the way out. Otherwise, a stored score reports the wrong distance to the win.
* An entry's score is used only when its depth equals the remaining depth exactly. An entry from a deeper search would be stronger, but it would give a different score from plain fixed-depth minimax, and test 13 could not then check the search. The prototype used any equal or greater depth, so section 3.6's figures may be a little high. Measure the effect when the table is added.
* The best move is used for ordering at any depth.

**Quiescence.** At depth 0, for Hard only:
* The score starts at `Evaluate`, because standing pat is legal when captures are optional.
* Search only capture moves from there, with no depth limit other than the ply cap. Quiescence results are not stored in the table.

---

## 4. Error handling

| Situation | Behaviour |
| --- | --- |
| Body larger than 10 KB | 400, `The request body must be 10 KB or smaller.` |
| Body is not JSON, or is the JSON value `null` | 400, `The request body is not valid JSON.` |
| The client stops sending the body, for example by disconnecting | 400, `The request body is not valid JSON.` No error is logged, because this is not a fault in the app. |
| JSON error with path `$.Board`, or `Board` missing | 400, `Board must be 8 rows of 8 values.` |
| JSON error with path `$.Difficulty` | 400, `Difficulty must be one of: easy, normal, hard` |
| JSON error with path `$.Version`, from stage 2 | 400, `Version must be 1, 2 or 3.`, even when `Difficulty` is valid (spec section 6.1.1, item 8) |
| JSON error with path `$.TipFor`, from step 2 | 400, `TipFor must be 1 or 2.` |
| JSON error at any other path | 400, `The request body is not valid JSON.` |
| Unknown `Version`, stage 1 | 400, `No version in body of request`, as today (spec section 5.1) |
| Neither `Difficulty` nor `Version`, stage 2 | 400, `Difficulty is missing. It must be one of: easy, normal, hard` |
| Invalid `Version`, stage 2 | 400, `Version must be 1, 2 or 3.` |
| Board value outside 0 to 6, from step 2 | 400, `Board values must be from 0 to 6.` |
| `TipFor` missing, or not 1 or 2, from step 2 | 400, `TipFor must be 1 or 2.` |
| Player 2 has no legal move, from step 2 | 400, `Player 2 has no legal moves.` |
| `Depth` missing or less than 1, for `Version` 2 or 3, from step 2 until step 3 | 400, `Depth must be 1 or more.` (spec section 6.1.2, rule 7) |
| Player 2 has no legal move, or `Depth` is missing or 0, in step 1 only | The old code throws a null reference, so the middleware returns 500, not today's 400 (spec section 6.3). The web app shows the same general message for both, and it always sends a `Depth`. Accepted for this short period. |
| Time limit reached during a search | Not an error. Returns the best move from the last finished depth. |
| Time limit reached before depth 1 finishes | Not an error. Returns the first legal move. Only possible on an extremely overloaded instance. |
| Any other exception | The middleware logs it and returns 500 with `successful: false` and `An unexpected error occurred.` The body keeps the same shape as other responses, so the web app handles it normally. |
| The caller cancels | Not treated as an error (same as PickTime). The search itself is short and CPU-only, so it does not check the token. |

All 400 and 500 responses use the existing response models (spec section 6.3).

---

## 5. Observability

As spec section 6.4, item 6 requires, and no more.

* A small middleware logs one line for each request, at Information level, from `RequestLoggingMiddleware` only: `"{Route} returned {StatusCode} in {DurationMs} ms"`. PickTime showed that named placeholders reach `customDimensions`.
* All other categories log at Warning and above.
* Errors are logged by the exception middleware.

The Functions host and the .NET worker log separately, so each setting goes in its own file. PickTime's plan, section 5.2, records this.

| File | Setting |
| --- | --- |
| `appsettings.json` | Worker log levels: `Default` at `Warning`, and `RequestLoggingMiddleware`'s full category name at `Information`. |
| `host.json` | `"telemetryMode": "OpenTelemetry"`, and `functionTimeout` of 5 minutes. The Flex Consumption default is 30 minutes, and until step 3 a request with a large `Depth` can run for that long, long after the 230-second HTTP limit. The host's own categories are also kept at `Warning`, other than one `Function.<name>.User` entry at `Information` for each function. Without these entries, the request line does not reach the local console (checked by a local run). |

Other points:

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
| Branch protection | `master` requires a pull request with 0 approvals and a passing `Build and test` check. It applies to admins too, and blocks force pushes and deletion. Same as PickTime. A merge deploys, so a red build must not be able to merge. |
| Deployment | OpenID Connect through a user-assigned managed identity. No stored credential. The client, tenant and subscription ids are GitHub secrets, only so that GitHub masks them in this public repository's workflow logs. They are not credentials, because signing in also needs the federated trust, so spec section 6.4, item 7 still holds. The owner chose this on 2026-11-06. |
| Publishing credentials | SCM and FTP basic authentication are off on the function app, as in PickTime. So no publish profile works. |
| HTTPS | HTTPS Only is on, as in PickTime. |
| Deployment rights | Website Contributor on the function app only. The first deployment showed that this is enough for Flex Consumption. |
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
* The existing tests stay. In step 2, `CheckMoveTests` tests classes that are removed, so each case moves to `FindMoveTests` with the same board and the same expected move. In step 3, the `Evaluate` tests move the same way. `GetAvailableBoardsTest` is empty, so it is deleted, and tests of `Apply` replace it.
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
| 9 | Examples 19 and 20: a chain can end on its start square, both directions give one move, a loop ends with no piece captured twice, and `Apply` keeps the piece when `From` equals `To` | 6.1.3 items 5, 8, 9, 6.5 #7, 6.2 #19, #20 | Defects 9, 10, double capture, a king lost from the board |
| 10 | A man reaching the far row ends its chain and is promoted | 6.1.3 item 7 | Chain carries on as a king |
| 11 | Value 6 squares behave as empty | 6.2 #9 | Highlighted squares block moves |
| 12 | Old and new generators agree on a fixed set of positions, except for defects 1 and 10. No position has a king chain that loops without ending on its start square, because defect 9 crashes the old code. Run once, in step 2, before the old code is removed. | 6.1.3 | A rule lost in the rewrite |
| 13 | Search at fixed depth gives the same score and the same move as plain minimax on a fixed set of positions, with the first tied move in generator order for both | 6.1.5 | A pruning or table bug |
| 14 | A position where two moves tie exactly returns both over repeated runs. A lower-scoring move is never returned. | 6.1.5 item 7 | Alpha-beta hides ties |
| 15 | Example 16: 20 Normal moves from the start give at least 2 different moves | 6.2 #16 | Defect 6 |
| 16 | Fake clock passes the limit: the search stops, and returns the last finished depth's move | 6.1.5 item 8 | Search overruns |
| 17 | Depth is capped at 6 for Normal, with no cap for Hard, read from `CompletedDepth` | 6.1.5 items 5, 6 | Normal is too strong |
| 18 | One legal move returns with no search | 6.2 #15 | Wasted time |
| 19 | A forced win in 1 is chosen over a forced win in 3. The slowest loss is chosen when every move loses. | 6.1.5 item 7 | Search misses or delays a win |
| 20 | Both end-of-game rules at a leaf, including a move that leaves the mover with no moves | 2.1 item 6, 6.5 #9 | AI disagrees with the screen |
| 21 | Examples 10 to 14: `Difficulty` letter case, numbers rejected, both fields, neither field. Also `{"difficulty":3}` gets the `Difficulty` message, `"easy,hard"` and `" hard "` are rejected, `Version: "abc"` with a valid `Difficulty` is rejected, and `Version: "2"` and `TipFor: "1"` are rejected. | 6.2, 6.1.1 items 2, 7, 8 | Contract errors |
| 22 | Example 7: `Depth: 50` has no effect | 6.2 #7, defect 3 | Client controls the cost |
| 23 | Easy always takes a capture when one exists | 6.1.5 item 4 | Easy loses its one rule |
| 24 | One self-play game at small depth runs to an end | 6.4 item 3 | The tool hangs or the draw rule fails |
| 25 | Example 5: no legal move for player 2 returns 400, and the message says so | 6.2 #5 | Defect 2 |
| 26 | From stage 2, a request with `Player: 1` at each level still returns a player 2 move | 6.1.5 item 1, 6.5 #5 | Defect 4 |
| 27 | Example 2: `Difficulty: "normal"` returns a legal player 2 move from row 2 to row 3 | 6.2 #2 | The new contract fails |
| 28 | A board value of 7, `TipFor: 3`, `TipFor: "abc"` and a missing `TipFor` return 400 | 6.1.2 rules 4, 5 | Bad input reaches the engine |
| 29 | A body that is not JSON, the JSON value `null`, a missing `Board` and `{"board":"x"}` return 400 with the messages in section 4 | 6.1.2 rule 1 | A bad request gets a 500 |
| 30 | Example 21: the tips leave out the king's own square | 6.1.4 item 4, 6.2 #21 | A highlight removes a king in the web app |
| 31 | Quiescence: a position in the middle of an exchange scores as the exchange ends, and Hard stands pat where the only capture loses material | 6.1.5 item 6, 2.1 item 1 | Hard misjudges exchanges, or forces a bad capture |
| 32 | `PositionalEvaluator`: one test for each feature in section 3.5, and the score for one player is the negative of the other's | 6.1.5 item 6 | A feature has no effect, or has the wrong sign |
| 33 | A forced win stops the search before the time limit, with a fake clock that does not advance | 6.1.5 item 7 | Time wasted after a win is found |
| 34 | The exporter's settings, read from the real registration, and no exporter without a connection string | 6.4 item 6 | Logs are dropped without warning |

### 7.3 Checks on the live app

Run by hand, with results in the pull request.

1. **Contract.** Replay the requests in spec section 5 against the new URL, and compare with the captured responses.
2. **Full game.** Before the switch, a script plays a game at each level, calling the new URL directly, because CORS blocks a local copy of the web app. After the release, the owner plays a full game at each level on the live site. The script works like this:
   * Player 1 is `Version: 1` with `Player: 1`, which plays random moves in stage 1. Player 2 is the level under test, with the depth the web app sends.
   * The game ends by the rule in spec section 2.1, item 6, or after 200 moves in total. A game that reaches 200 moves counts as finished, and the result says so.
   * Each move is checked against the board: the start holds the moving player's piece, the end is empty or is the start, and each captured square holds an opponent's piece. This finds a broken response. It does not prove the rules, which the tests in section 7.2 do.
   * A 500 response stops that game. The result records the board and the request, because it may be defect 9 (risk 12).
3. **Speed.** Spec section 6.4, item 1. A short script sends the warm-up, then 30 requests for each level from fixed positions, with a pause between levels. It also records `CompletedDepth` while a temporary log line is on, so we learn the real depth on Azure. The line is removed afterwards (spec section 6.4, item 6).
4. **Concurrency.** Send 12 requests at once to see how the single instance behaves (risk 2). Use client-side timings, and record each status code, because the logged time does not include a wait before the function starts.
5. **Logs.** Before the switch, confirm in Application Insights that each request has one line, with `Route`, `StatusCode` and `DurationMs` in `customDimensions`, and the correct status. No other category logs at Information. Compare the volume with the estimate in section 8.1, because the exporter also sends traces and metrics. The worker stops the host relaying its logs in Azure, so a fault here means no request logs at all.

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

In UK South, other than the global items that spec section 6.4, item 4 names. Application Insights is documented to create a smart detection rule and its action group automatically. Nothing uses them and they are global, so they would be deleted. None appeared for this app when it was created on 2026-11-06.

The app's URL is `https://func-draughts-game-chhwarhkf4hphjf0.uksouth-01.azurewebsites.net`, because "Secure unique default hostname" adds a random suffix. The function app's plan is `ASP-rgdraughtsgame-af4f`.

| Resource | Name | Plan | Free allowance | Expected usage |
| --- | --- | --- | --- | --- |
| Resource group | `rg-draughts-game` | — | No charge | — |
| Function app and plan | `func-draughts-game` | Flex Consumption, on demand, 512 MB, maximum 1 instance, no always-ready instances | 250,000 executions and 100,000 GB-s a month, per subscription, shared with PickTime | 0.5 GB-s a request at the 1 s billing minimum. The grant covers about 200,000 requests, or about 3,000 games. |
| Storage account | `stdraughtsgame`, which must also be free across Azure | Standard LRS | None | About £0.10 a month. Approved by the owner on 2026-11-02. |
| Application Insights | `appi-draughts-game` | Workspace-based | No charge itself | — |
| Log Analytics workspace | `log-draughts-game` | Per GB, with a daily cap of 0.1 GB | 5 GB a month, per billing account | About 1 KB a request |
| Managed identity | `id-draughts-game-deploy` | User-assigned | No charge | — |
| Budget, alert | `budget-alert` | £1 a month, on the resource group, actual and forecast | Budgets are free | Email only |
| Budget, stop | `budget-stop` | £3 a month, on the resource group | Budgets are free | Calls the action group below |
| Action group | `ag-draughts-game-stop` | Webhook action | 100,000 webhooks a month, from the Azure Monitor pricing page, read on 2026-11-04 | One webhook, only if the £3 budget is reached |
| Logic App | `la-draughts-game-stop` | Consumption | First 4,000 actions free | 2 actions a run. Metered per run, so nothing while idle. |

The Flex Consumption, Log Analytics, action group and Logic Apps figures were checked again on the Azure pricing pages on 2026-11-06, with no change. Those pages show no Flex Consumption rates. The budget figures come from PickTime's plan, checked on 2026-10-06.

**Worst case with the limits off.** One 512 MB instance, busy for a whole month, is about 1.3 million GB-s. At the example rate in Microsoft's docs ($0.000026 per GB-s), that is about $30 after the grant. Flex also [bills executions](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan#billing) after the grant, and this figure leaves them out. The £3 stop should act long before that, although it can lag (section 8.3).

### 8.2 Creating the resources

Created once, by hand. The owner created them in the portal on 2026-11-06, as on PickTime, to learn the setup. The pipeline only deploys code, so its identity stays narrow. These commands are an equivalent record, and they were not the ones run, other than the read-only checks. They need the owner's `az login`, and they change a real subscription.

The owner saw Flex Consumption offered for UK South on the portal's create page during the spec work, and created nothing there. Check it from the CLI first, because this also confirms .NET 10. List the existing default resource groups at the same time, so that the check after creation has something to compare with:

```bash
az functionapp list-flexconsumption-locations -o table
az functionapp list-flexconsumption-runtimes --location uksouth --runtime dotnet-isolated -o table
az group list --query "[?starts_with(name,'DefaultResourceGroup')].name"
```

Then create the resources. The workspace and `appi-draughts-game` come before the function app:

```bash
az group create -n rg-draughts-game -l uksouth
az monitor log-analytics workspace create -n log-draughts-game -g rg-draughts-game -l uksouth --quota 0.1
```

Create `appi-draughts-game` in the portal, workspace-based, in UK South, on `log-draughts-game`. Then:

```bash
az storage account create -n stdraughtsgame -g rg-draughts-game -l uksouth --sku Standard_LRS
az functionapp create -n func-draughts-game -g rg-draughts-game --storage-account stdraughtsgame --flexconsumption-location uksouth --runtime dotnet-isolated --runtime-version 10.0 --instance-memory 512 --maximum-instance-count 1 --app-insights appi-draughts-game --https-only true
az functionapp scale config set -n func-draughts-game -g rg-draughts-game --trigger-type http --trigger-settings perInstanceConcurrency=8
az functionapp cors add -n func-draughts-game -g rg-draughts-game --allowed-origins https://ollierumbol.github.io
az resource update -g rg-draughts-game --name scm --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies --parent sites/func-draughts-game --set properties.allow=false
az resource update -g rg-draughts-game --name ftp --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies --parent sites/func-draughts-game --set properties.allow=false
```

**`--app-insights` matters.** Without it, or `--disable-app-insights true`, `az functionapp create` makes its own Application Insights resource (CLI help, version 2.90.0). That resource could send logs to a workspace in a default resource group, outside the daily cap and outside both budgets, which cover only `rg-draughts-game`.

The higher concurrency (the default at 512 MB is 4) is on purpose. With one instance, a request above the limit may wait or be rejected. A search that shares the CPU still stops on time, at a lower depth, so sharing is better than rejecting. Section 7.3 tests this.

The identity and its federated credential follow PickTime's steps. The subject is `repo:OllieRumbol/DraughtsGameFunction:ref:refs/heads/master`. This repository's OpenID Connect setting returns `use_default: true` and `use_immutable_subject: false` (checked on 2026-11-05), so GitHub sends this classic form. The portal may offer the immutable form instead. Do not use it.

### 8.3 Automatic stop

As spec section 6.4, item 5 requires. No code is added to the app.

1. `budget-stop` reaches 100% of £3.
2. It calls `ag-draughts-game-stop`.
3. The action group calls the Logic App's HTTP trigger.
4. The Logic App sends one `POST` to Azure Resource Manager, to the app's `/stop` endpoint, signed in with its own identity. It then ends.

To restart: `az functionapp start -n func-draughts-game -g rg-draughts-game`.

The workflow's trigger must be **When an HTTP request is received**, type `Request`. The trigger named **HTTP** runs on a timer, so it would stop the app every 3 minutes. This happened once on 2026-11-06, while the workflow was being built.

The owner built this in the portal on 2026-11-06. Both budgets end on 2030-12-30. Extend them before then, or the alert and the stop end with them.

This is tested once by running the Logic App by hand, confirming that the app stops, and restarting it. The web app's general message is checked the same way after the release, because until then the web app does not call this app. The budget cannot be tested without spending £3, so the link from the budget to the action group is checked with the portal's configuration view.

The stop is not immediate. Cost data is typically available within 8 to 24 hours, and budgets are evaluated every 24 hours ([Create and manage budgets](https://learn.microsoft.com/azure/cost-management-billing/costs/tutorial-acm-create-budgets), read on 2026-11-04). So the bill can pass £3 by up to about two days of use before the app stops. One instance limits this.

A runbook in Azure Automation was rejected: it needs an extra account and script for what is one HTTP call.

### 8.4 CI/CD

One workflow, `.github/workflows/build-and-deploy.yml`, copied from PickTime, with these differences:

| Setting | PickTime | Here | Why |
| --- | --- | --- | --- |
| Branch | `main` | `master` | The repository's default branch |
| Runner | `windows-latest` | `ubuntu-latest` | Matches Flex Consumption's Linux |
| Solution | `.slnx` | `DraughtsGameFunction.sln` | Existing file |
| Azure ids | `vars.` | `secrets.` | GitHub masks them in public logs (section 6) |
| Sign-in and deploy actions | Major version tags | Commit hashes | This job holds a sign-in token, and a tag can move. The owner chose this on 2026-11-06. Update the hashes by hand. |
| After deploying | Nothing | A POST to the app must return `successful: true` | A deployment can succeed while the functions do not start. The owner chose this on 2026-11-06. |

Unchanged from PickTime: build and test on every pull request, NuGet Audit warnings as errors, deploy only on a push to the branch, `azure/login` with OpenID Connect, `Azure/functions-action`, and no `environment:` on the deploy job. Microsoft's docs confirm that Flex Consumption uses package deployment through this action, and that `sku` is only needed with a publish profile. The `include-hidden-files` setting on the artifact stays, because the isolated worker needs `.azurefunctions`.

**Branch protection depends on two things here (section 6).** The job keeps the name `Build and test`, because the required check matches that name. The `pull_request` trigger has no path filter, because a required check that never runs would stop a documents-only pull request from merging.

**Rollback.** Flex Consumption has no slots and keeps no previous package. The fastest way is to [re-run the last good workflow run](https://learn.microsoft.com/azure/azure-functions/functions-rollback-deployments). Then fix `master` through a pull request, because the next merge deploys what is there. A revert through a pull request also works.

### 8.5 Switching over

The web app's code has been unchanged since its last release in March 2022 (the `gh-pages` branch), so its release path has not been used for years. The owner chose to release by hand, because nothing shows that the old workflow still works.

**Build test, 2026-11-04.** A fresh clone of `main`, on Node 24.19.0, with nothing pushed:

* `npm ci` fails, because the lock file is out of step with `package.json`. `npm install` works.
* All 7 tests pass.
* `npm run build` fails with `ERR_OSSL_EVP_UNSUPPORTED`. It passes with `NODE_OPTIONS=--openssl-legacy-provider`.
* The JavaScript and CSS match the live `gh-pages` branch, ignoring line endings and source map lines. So a release changes only the two URLs.
* The publish step worked at the release on 2026-11-07, from a clean clone of `main`.

**Steps:**

1. The new app passes the checks in section 7.3, steps 1 and 2, with the URL called directly.
2. On a new branch from `origin/main` of `draughtsgame`, change the two URLs in `src/logic/api.js`. The owner has uncommitted work on another branch there, which stays untouched.
3. Ask the owner, then merge the branch to `main`. Ask before the merge, because the old workflow may publish at that point (risk 3).
4. Release from a clean copy of `main`, with `npm install`, then `npm run deploy` with `NODE_OPTIONS=--openssl-legacy-provider` set. The owner's git login on this machine does the push.
5. Watch the new app's logs, and play a full game at each level on the live site.
6. After a few days with no problems, ask the owner to confirm, then delete `DraughtsGameFunction20210321121511ResourceGroup` (spec section 9). Rollback until then: restore the old URL.

---

## 9. Risks

| # | Risk | Likelihood | Mitigation |
| --- | --- | --- | --- |
| 1 | 512 MB has 0.25 of a core, so the old search in step 1 is slow, or Hard reaches too little depth later. Depth 5 could take longer than today in step 1, even with .NET 10's 3 times gain. | Medium | Measure it in step 1 (section 7.3). The time limit protects response times from step 3. If it is too slow, moving to 2,048 MB (1 core) is one command. It lowers the free capacity to about 50,000 requests, or about 800 games a month, and it needs the owner's approval. |
| 2 | Microsoft warns that a maximum below 40 can reject requests when traffic exceeds capacity. | Low for a hobby game | Higher per-instance concurrency, and the test in section 7.3. If needed, raise the maximum instance count with the owner's approval. |
| 3 | The web app's old workflow also runs when the URL change reaches `main`. It uses Node 12 and old actions, its `TOKEN` secret dates from January 2021, and no runs are listed for it. It may fail. | Closed | It ran on 2026-11-07 and failed at its deploy step, publishing nothing. The release was run by hand (section 8.5). Fixing the workflow belongs to the frontend spec. |
| 4 | A Flex regional quota in UK South blocks creation, as a Y1 quota did for PickTime. | Low | A quota request is free. Start the resources early, not on the deadline. |
| 5 | The automatic stop fails silently, because the budget cannot be fully tested. | Low | The £1 email comes first, and the single instance limits the damage. Test the Logic App by hand (section 8.3). |
| 6 | Website Contributor may not be enough for a Flex package deployment. | Closed | The first deployment succeeded with this role on 2026-11-06. |
| 7 | The 10 November date slips. | Medium | Step 1 is kept small. The old app should keep running, unsupported. |
| 8 | Normal at depth 6 is too strong for casual players, or Hard misses the 80% target. | Medium | Depth is a setting. Self-play and the owner's own games decide. |
| 9 | Move generation changes the moves the web app expects. The web app has its own move validation and does not call this API to check a player's move. | Low | Test 12 compares old and new. The only intended differences are the defect fixes. The king capture fix means tips can show a move the web app's own code already allows. The web app handles a computer move that ends where it began (`CompleteBoard.js`, `player2Go`, read on 2026-11-04). It would not handle a tip on the king's own square, so test 30 covers that. |
| 10 | The transposition table returns a wrong score after a hash collision. | Low | 64-bit keys compared in full. The chosen move is still checked against the legal moves at the root. |
| 11 | The local tests and the Linux runner differ. | Low | Tests run in CI on Linux, and locally on Windows. Project references use the existing casing. |
| 12 | Defect 9 can crash the new app from its first deployment until the new generator goes live in step 2. An anonymous request can cause it, and so can the search in a rare position. | Low | The old app has the same defect today. Step 2 removes it. |
