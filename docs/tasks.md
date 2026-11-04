# Draughts Game Function — Tasks

> **What this document is for:** It answers *what do we do next?* It splits the approved design into small, verifiable steps and tracks progress against them.

Last updated: 2026-11-04

<!--
How to use this file
* This file holds the order of work and its progress. It does not repeat the requirements or the design.
* In "Refs", "spec 6.2" means spec.md section 6.2, and "plan 3.1" means plan.md section 3.1.
  "Test N" refers to the numbered tests in plan section 7.2.
* One task is one working session and one pull request.
* A task that grows beyond that is split into smaller tasks here.
* Tick a task only when every "Done when" item is true, "Verify" has passed and, for a task that changes behaviour, the review has no open blockers.
* Record deviations from the spec or plan in "Notes", and update that document to match.
* If a session ends before the task is done, write a checkpoint in "Notes": what is done, decisions and why, what failed, and the next step.
* When a task is ticked, cut its "Notes" to at most 5 short lines. Put the evidence, the review rounds and the final report in the pull request description.
* The first unticked task whose dependencies are done is the next task.
* Tasks marked (manual) change a real Azure subscription or a live site. The owner signs in and approves each one. Claude can prepare and check them.
-->

Unless a task says otherwise, run commands from the repository root. Task branches start from `master`, and their pull requests merge into `master`. The standard test command is:

```
dotnet test DraughtsGameFunction.sln
```

**From T5 on, every merge to `master` deploys to the new app.** From T8 on, that app is the one the live game uses.

## Phase 1 — Platform

Goal: the API runs on .NET 10 in UK South with no change to the search, every merge deploys automatically, the cost limits are in place, and the live web app uses the new URL.

**Deadline:** T1 to T8 are done before 10 November 2026 (spec 2.4). T9 can follow after that date.

- [x] **T1 — Port the solution to .NET 10 on the isolated worker**
  - Refs: spec 2.4, spec 3 goal 1, spec 4, spec 5, spec 6.1.1 items 1–3 and 5, spec 6.1.2 rules 1–3, spec 6.3, spec 6.4 items 7–8, plan 3.1, plan 3.2, plan 3.3, plan 3.5, plan 4, plan 7.1, tests 1–5, 29
  - Depends on: —
  - Done when:
    - `Directory.Build.props` sets `net10.0` for every project, and `Directory.Packages.props` holds every package version.
    - All packages are on supported versions. The models and service projects no longer reference the Functions SDK.
    - The host uses the isolated worker with ASP.NET Core integration, and both functions keep their names and routes.
    - `RequestReader`, `BoardJsonConverter` and the shared JSON options exist as in plan section 3.3, other than `DifficultyJsonConverter`, which T17 adds. `System.Text.Json` is the only JSON library.
    - The test project references the host project, so tests can call the functions.
    - Move generation and the search are unchanged, and the existing tests pass with no change to what they check.
    - Tests 1–5 and 29 pass.
  - Verify: the standard test command; then `dotnet list DraughtsGameFunction.sln package --vulnerable --include-transitive` reports no vulnerable package; then `dotnet list DraughtsGameFunction.sln package --deprecated` and `--outdated`, with the result in the pull request
  - Notes: Nullable stays off for old code (plan 3.5). The 500 response for "no legal move" is accepted until T12 (plan 4).
    - For T2: `func start` warns that `dotnet run` is the preferred way to start an isolated project. `local.settings.json` now sets `dotnet-isolated` on this machine.
    - For T17: `RequestReader.MessageFor` treats a `JsonException` with an inner exception as broken JSON, so that a body cut off inside the board does not get the board message. Keep this when adding the `Difficulty`, `Version` and `TipFor` paths. The functions stay static, creating services with `new`, until T17.
    - For T17: the owner approved strict number reading on 2026-11-04 (plan 3.3). Until then, `"Version": "2"` is accepted, as it is today.
    - Evidence: this task's pull request.

- [ ] **T2 — Add the error handling and request logging**
  - Refs: spec 6.3, spec 6.4 item 6, plan 2, plan 3.2, plan 4, plan 5, tests 6, 34
  - Depends on: T1
  - Done when:
    - The exception middleware returns the 500 response in plan section 4, with no exception text.
    - `RequestLoggingMiddleware` writes one line for each request, as in plan section 5, and other categories log at Warning and above.
    - `appsettings.json` and `host.json` hold the settings in plan section 5, including `functionTimeout`.
    - The OpenTelemetry exporter is set up as in plan section 5, and does not run when no connection string is set.
    - Tests 6 and 34 pass.
    - The app starts locally, and answers a GetPotentialMoves request with one log line.
  - Verify: the standard test command; then, with Azurite running, `func start` in `DraughtsGameFunction` and a POST of the spec section 5.2 request to `http://localhost:7071/api/GetPotentialMoves` returns the four squares, and the console shows the line `returned 200 in`
  - Notes: Azurite is not on the PATH on this machine. It is at `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe`. The git-ignored `local.settings.json` needs the settings in plan section 2.

- [ ] **T3 — Build and test every pull request**
  - Refs: spec 3 goal 2, spec 6.4 item 7, spec 9, plan 8.4, risk 11
  - Depends on: T1
  - Done when:
    - `.github/workflows/build-and-deploy.yml` builds and tests on every pull request and on a push to `master`, with the differences in plan section 8.4. It has no deploy job yet.
    - NuGet Audit warnings are errors in CI.
    - A pull request shows the result.
  - Verify: `gh pr checks` on this task's pull request shows the build passed. Push a temporary failing test to the branch, confirm that the check fails, then remove the test and confirm that it passes again.
  - Notes:

- [ ] **T4 — Create the Azure resources (manual)**
  - Refs: spec 2.6, spec 6.4 items 4–5, plan 2, plan 6, plan 8.1, plan 8.2, risks 2, 4
  - Depends on: —
  - Done when:
    - The region check in plan section 8.2 shows that UK South offers Flex Consumption with .NET 10. If it does not, stop and tell the owner, because the spec requires UK South.
    - The resources in plan section 8.1 exist, other than the budgets, the action group and the Logic App. The final names and the new URL are recorded in plan section 8.1.
    - The allowances in plan section 8.1 were checked again on the Azure pricing pages. Any change is recorded in the plan.
    - The app has a maximum of 1 instance, 512 MB and a per-instance concurrency of 8.
    - HTTPS Only is on, and SCM and FTP basic authentication are off.
    - CORS allows only `https://ollierumbol.github.io`.
    - The workspace's daily cap is 0.1 GB.
    - The app's logs go to `appi-draughts-game`, and creating the app made no second Application Insights resource or new default resource group.
    - The identity has its federated credential and its role on this app only, with the subject form this repository sends.
    - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` are repository variables.
  - Verify: `az resource list -g rg-draughts-game --query "[].{name:name,location:location}" -o table` shows every resource in UK South, and only one Application Insights resource; `az group list --query "[?starts_with(name,'DefaultResourceGroup')].name"` shows no new group; `az functionapp scale config show -n func-draughts-game -g rg-draughts-game` shows the instance limits; `az functionapp cors show -n func-draughts-game -g rg-draughts-game` shows one origin; `az functionapp show -n func-draughts-game -g rg-draughts-game --query httpsOnly` shows `true`; the portal shows `appi-draughts-game` linked to the app
  - Notes: Needs the owner's `az login`. It can start before T1 if time gets short, because a quota problem takes time to solve (risk 4). This is a public repository, so do not put the subscription ID or any connection string in this file or a pull request.

- [ ] **T5 — Deploy on every merge to `master`**
  - Refs: spec 3 goal 2, spec 6.4 item 7, spec 9, plan 8.4, risk 6
  - Depends on: T2, T3, T4
  - Done when:
    - The workflow has a deploy job that runs only on a push to `master`, and needs the build job, so a failing test stops the deployment.
    - It signs in with OpenID Connect, and no credential is stored in the repository or in GitHub secrets.
    - Merging this task deploys the app, and the new URL answers.
  - Verify: `gh run list --branch master --limit 1` shows the deploy succeeded after the merge; then a POST of the spec section 5.2 request to the new URL returns the four squares; SCM basic authentication is still off
  - Notes: The first deployment tests whether Website Contributor is enough for Flex Consumption (risk 6). If it is not, use the narrowest role that works, and record it in plan section 6.

- [ ] **T6 — Add the cost limits (manual)**
  - Refs: spec 6.4 items 4–5, spec 9, plan 6, plan 8.1, plan 8.3, risk 5
  - Depends on: T4, T5
  - Done when:
    - The allowances for the action group and the Logic App in plan section 8.1 were checked again on the Azure pricing pages. Any change is recorded in the plan.
    - The £1 budget emails the owner on actual and forecast cost.
    - The £3 budget calls the action group, and the action group calls the Logic App.
    - The Logic App's identity has its role on this app only.
    - The Logic App, run by hand, stops the function app. The app is then started again and answers.
    - The Logic App's trigger URL is not in the repository.
  - Verify: `az functionapp show -n func-draughts-game -g rg-draughts-game --query state` shows `Stopped` after the manual run and `Running` after the restart; the link from the budget to the action group is checked in the portal
  - Notes: Budgets have no region. `az monitor action-group create` takes `--location`, so set it to UK South if the service accepts that. If it does not, stop and propose a change to spec section 6.4, item 4.

- [ ] **T7 — Check the new app directly (manual)**
  - Refs: spec 5, spec 6.4 item 2, plan 7.3 steps 1 and 4, plan 8.5 step 1, risks 1, 2
  - Depends on: T5
  - Done when:
    - The requests in spec section 5 give the captured responses from the new URL.
    - A scripted game, calling the new URL directly, plays to its end at Versions 1, 2 and 3 with the depths the web app sends. Each move is legal.
    - The response times at depth 4 and depth 5 are recorded and compared with the live app's. A warm-up request comes first.
    - 12 requests at once are sent, and the result is recorded.
  - Verify: the results above, in the pull request. The owner decides whether the speed is good enough to switch.
  - Notes: The full game in the web app comes in T8 (plan 7.3 step 2). If the speed is too slow, moving to 2,048 MB needs the owner's approval (risk 1).

- [ ] **T8 — Release the web app with the new URL (manual)**
  - Refs: spec 3 goal 1, spec 3 non-goals, spec 6.4 item 5, spec 9, plan 7.3 step 2, plan 8.3, plan 8.5 steps 2–5, risk 3
  - Depends on: T6, T7
  - Done when:
    - In `draughtsgame`, a branch from `origin/main` changes only the two URLs in `src/logic/api.js`.
    - The owner approves before the merge to `main`, because the old workflow may publish at that point.
    - The release runs as in plan section 8.5, from a clean copy of `main`.
    - The live site calls the new URL.
    - The owner plays a full game at each level on the live site.
    - The new app's logs show the requests, with no errors.
    - With the app stopped by the Logic App, the live site shows its general error message. The app is then started again.
  - Verify: the site at `https://ollierumbol.github.io/draughtsgame` makes its requests to the new URL, shown in the browser's network view; then the query in plan section 5 returns rows
  - Notes: Leave the owner's uncommitted work on `feature/refactor-complete-board` untouched. The publish step has not been tested (plan 8.5). The old workflow also runs on the merge, and it may fail (risk 3). Rollback: restore the old URL and release again. The pull request and the merge are in the `draughtsgame` repository. Record the result in this repository's pull request.

- [ ] **T9 — Delete the old resources (manual)**
  - Refs: spec 9, plan 8.5 step 6
  - Depends on: T8, plus a few days with no problems
  - Done when:
    - The owner confirms that the old resources can go.
    - `DraughtsGameFunction20210321121511ResourceGroup` is deleted.
    - The old publish profile files under `DraughtsGameFunction/Properties` are removed from the repository.
    - The live site still plays a game.
  - Verify: `az group exists -n DraughtsGameFunction20210321121511ResourceGroup` returns `false`
  - Notes: This cannot be undone. Look at what the group holds, and show the owner, before deleting it. Until this task is ready, carry on with the next task whose dependencies are done.

## Phase 2 — Move generation

Goal: one move generator follows the house rules, defects 1, 2, 5, 9 and 10 are fixed, and all request validation is in place.

- [ ] **T10 — Add the new move generator beside the old code**
  - Refs: spec 2.1, spec 2.2, spec 6.1.3, spec 6.2 examples 3, 4, 9, 17–20, spec 6.5 items 1, 6, 7, plan 3.1, plan 3.2, plan 3.3, plan 3.4, risk 9, tests 8–12
  - Depends on: T8
  - Done when:
    - `Engine/Board.cs`, `Engine/Move.cs` and `Engine/MoveGenerator.cs` exist as in plan section 3.3. No existing code calls them yet.
    - Tests 8–11 pass.
    - Test 12 passes. Its only differences between old and new are defects 1 and 10, and each one is listed in the pull request.
  - Verify: the standard test command
  - Notes: Do not change the house rules (spec 2.1). Captures stay optional, a chain may stop after any jump, a man cannot capture a king, and a king may land back on its start square.

- [ ] **T11 — Switch to the new move generator and remove the old code**
  - Refs: spec 3 goal 4, spec 4.1 defects 1, 5, 9, 10, spec 6.1.4, spec 6.2 examples 1, 21, plan 3.1, plan 3.3, plan 7.1, risks 9, 12, tests 12, 30
  - Depends on: T10
  - Done when:
    - `FindMove.FindAvailableMoves` calls `MoveGenerator`, with the same signature.
    - `PlayerTipsService` leaves out a move that ends where it began. Test 30 passes.
    - `CheckMove`, `Tree`, `KingTree` and `TreeTake` are removed, with test 12's copy of the old behaviour.
    - Each case in `CheckMoveTests` moves to `FindMoveTests`, with the same board and the same expected move.
    - No exception is used to detect the board edge.
  - Verify: the standard test command; then `git grep -n -E "CheckMove|KingTree|TreeTake" -- "*.cs"` finds only the moved test names, if any
  - Notes: The old search still runs after this task, on the new generator. Its board update clears the square before it writes the piece (`AutomatedPlayerServiceV2.cs:114`, and V3), so a move that ends where it began keeps the king.

- [ ] **T12 — Add the remaining validation and the no-move error**
  - Refs: spec 3 goal 4, spec 4.1 defect 2, spec 6.1.2 rules 4–5, spec 6.1.4 item 3, spec 6.1.5 item 3, spec 6.2 example 5, spec 6.5 item 2, plan 4, tests 25, 28
  - Depends on: T11
  - Done when:
    - A board value outside 0 to 6, and a `TipFor` that is not 1 or 2, return the messages in plan section 4.
    - No legal move for player 2 returns the 400 in plan section 4.
    - GetPotentialMoves returns an empty list with `successful: true` when the player has no legal move.
    - Tests 25 and 28 pass.
  - Verify: the standard test command
  - Notes:

## Phase 3 — Search

Goal: one shared search replaces the V2 and V3 code, the API accepts `Difficulty`, and the response-time limits are met on the live app.

- [ ] **T13 — Add the alpha-beta search at a fixed depth**
  - Refs: spec 2.1 item 6, spec 6.1.5 items 5, 7, 9, spec 6.5 item 9, plan 1.1, plan 3.1, plan 3.2, plan 3.3, plan 3.7, tests 13, 19, 20
  - Depends on: T11
  - Done when:
    - `IEvaluator`, `MaterialEvaluator`, `SearchLimits`, `SearchResult` and `GameTreeSearch` exist as in plan section 3.3. No existing code calls them yet.
    - The search can play as either player.
    - Wins and losses follow spec section 2.1, item 6, with the fastest win and the slowest loss preferred.
    - Tests 13, 19 and 20 pass.
  - Verify: the standard test command
  - Notes: Test 13 needs a plain minimax written in the test project.

- [ ] **T14 — Add the time limit, iterative deepening and ties**
  - Refs: spec 6.1.5 items 7–9, spec 6.5 items 3, 4, plan 3.3, plan 3.5, plan 3.7, plan 4, plan 7.1, risk 10, tests 14, 16, 18, 33
  - Depends on: T13
  - Done when:
    - The search deepens one level at a time, and stops at its limit using the injected `TimeProvider`.
    - It returns the last finished depth's move, or the first legal move if depth 1 did not finish.
    - It keeps every move tied for the top score, as in plan section 3.5. With no tie breaker it takes the first in generator order, so a fixed-depth run always gives the same move.
    - It returns at once with one legal move, and stops on a forced win.
    - Tests 14, 16, 18 and 33 pass, and tests 13 and 19 still pass.
  - Verify: the standard test command
  - Notes:

- [ ] **T15 — Add the transposition table, quiescence and the first Hard evaluation**
  - Refs: spec 2.1 items 1, 4, spec 6.1.5 item 6, plan 3.1, plan 3.5, plan 3.6, plan 3.7, risk 10, tests 31, 32
  - Depends on: T14
  - Done when:
    - `TranspositionTable` exists as in plan section 3.7, with one table for each search and 64-bit keys compared in full.
    - `PositionalEvaluator` uses the first values in plan section 3.5.
    - Quiescence runs when `UsesQuiescence` is true, and allows standing pat.
    - Tests 13, 14 and 19 still pass with the table on, and tests 31 and 32 pass.
    - The depth reached with the table is measured locally on the positions in plan section 3.6, and the result is in the pull request.
  - Verify: the standard test command
  - Notes: The values are tuned in T21. Plan section 3.7 says why the table uses only an exact depth match.

- [ ] **T16 — Add `ComputerPlayer` and the search settings**
  - Refs: spec 6.1.5 items 1, 2, 4–7, spec 6.2 example 16, spec 6.4 item 1, plan 2, plan 3.2, plan 3.3, plan 3.4, tests 7, 15, 17, 23
  - Depends on: T15
  - Done when:
    - The `Difficulty` enum, `SearchOptions` and `ComputerPlayer` exist as in plan sections 2 and 3.3. No function calls them yet.
    - Easy uses the rule from `AutomatedPlayerServiceV1`, Normal uses `MaterialEvaluator`, and Hard uses `PositionalEvaluator`.
    - Production uses `Random.Shared` (plan 3.3).
    - A bad setting stops start-up.
    - Tests 7, 15, 17 and 23 pass.
  - Verify: the standard test command
  - Notes:

- [ ] **T17 — Switch GetNextMove to the new contract and remove the old services**
  - Refs: spec 3 goal 5, spec 4.1 defects 3, 4, 6, 7, 8, spec 6.1.1 items 5–8, spec 6.1.2 rule 6, spec 6.2 examples 2, 6, 7, 10–15, spec 6.5 item 5, plan 3.1, plan 3.4, plan 4, plan 7.1, tests 21, 22, 26, 27
  - Depends on: T12, T16
  - Done when:
    - GetNextMove takes `Difficulty` and `Board`, with `Version` as the fallback. `Depth` and `Player` have no effect, and the web app's current request still works.
    - `DifficultyJsonConverter` exists, and the error messages match plan section 4.
    - `AutomatedPlayerServiceV1`, `V2` and `V3`, `IAutomatedPlayerService`, `MinimaxOutcome` and `PotentialNextMove` are removed.
    - The misspelled `Intstance` namespace is gone.
    - Both functions get their services through dependency injection, not `new`.
    - The `Evaluate` tests move, and `GetAvailableBoardsTest` is deleted, as in plan section 7.1.
    - Test 3 now checks the stage 2 message for `Version: 9` (spec 6.2 example 6). Example 15 is also covered through the function.
    - Tests 21, 22, 26 and 27 pass.
  - Verify: the standard test command; then `git grep -n -E "Intstance|AutomatedPlayerServiceV[123]" -- "*.cs"` returns nothing
  - Notes: The merge changes the live game, because the web app uses this app from T8. Play a game at each level after the deployment.

- [ ] **T18 — Measure the search on the live app (manual)**
  - Refs: spec 3 goal 5, spec 6.4 items 1, 6, plan 3.6, plan 5, plan 7.3 steps 3–4, plan 7.4, risks 1, 2
  - Depends on: T17
  - Done when:
    - A temporary log line records `CompletedDepth`, and is deployed.
    - The script sends the warm-up, then 30 requests for each of Normal and Hard from several positions, with a pause between levels.
    - At least 95% of the requests finish within 700 ms for Normal and 1,200 ms for Hard, read from the logs.
    - The real depth for each level is recorded in plan section 3.6, and `--time-scale` in plan section 7.4 is set to match.
    - 12 requests at once are sent again, and the result is recorded.
  - Verify: the query in plan section 5, for each level's time window, in the pull request
  - Notes: If a limit is not met, the first choices are a lower time limit or 2,048 MB. Both need the owner's approval (risk 1). T19 removes the log line.

- [ ] **T19 — Remove the temporary log line**
  - Refs: spec 6.4 item 6, plan 7.3 step 3
  - Depends on: T18
  - Done when:
    - The log line from T18 is removed, and the change is deployed.
    - The logs show only the per-request line, warnings and errors.
  - Verify: the standard test command; then `git grep -n "CompletedDepth" -- "DraughtsGameFunction/*.cs" "DraughtsGameFunctionService/Instance/*.cs"` finds no log call
  - Notes:

## Phase 4 — Levels

Goal: each level is measurably stronger than the one below it, and the definition of done is met.

- [ ] **T20 — Add the self-play tool**
  - Refs: spec 6.1.5 item 9, spec 6.4 item 3, plan 3.1, plan 7.4, test 24
  - Depends on: T16
  - Done when:
    - `DraughtsGameFunctionSelfPlay` is in the solution, and takes the options in plan section 7.4.
    - It follows the game rules in spec section 6.4, item 3, and opens each game with the random moves in plan section 7.4.
    - It prints each pairing's score, and each side moves first in half of the games.
    - Test 24 passes.
  - Verify: the standard test command; then `dotnet run --project DraughtsGameFunctionSelfPlay -c Release -- --games 2 --time-scale 0.1` finishes and prints the scores
  - Notes:

- [ ] **T21 — Tune the levels and meet the strength targets**
  - Refs: spec 2.3, spec 3 goal 6, spec 6.1.5 items 5, 6, spec 6.4 item 3, plan 3.5, plan 7.4, risk 8
  - Depends on: T18, T20
  - Done when:
    - The final `PositionalEvaluator` values are in the code and in plan section 3.5.
    - Hard scores at least 80% against Normal, and Normal at least 90% against Easy, over 200 games for each pairing.
    - The owner has played each level, and is happy with how each one feels.
  - Verify: the command in plan section 7.4, with the `--time-scale` from T18; the scores go in the pull request
  - Notes: A change to Normal's depth is decided with the owner (plan 7.4). Repeat the speed check in T18 if a time limit changes, or if the new evaluation adds a feature that costs more than one pass over the board.

- [ ] **T22 — Check the definition of done**
  - Refs: spec 9
  - Depends on: T9, T19, T21
  - Done when:
    - Each item in spec section 9 has its evidence listed in the pull request.
    - Each worked example in spec section 6.2 is matched to its automated test.
    - The Azure cost so far is checked against spec section 6.4, item 5.
    - The speed evidence from T18 still applies to the final code, or the check has been repeated.
  - Verify: the standard test command; then the list above, in the pull request
  - Notes: When this task is ticked, suggest running `/retrospective`.

---

## Blocked

None.
