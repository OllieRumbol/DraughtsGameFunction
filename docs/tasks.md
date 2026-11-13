# Draughts Game Function — Tasks

> **What this document is for:** It answers *what do we do next?* It splits the approved design into small, verifiable steps and tracks progress against them.

Last updated: 2026-11-06

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

**From T3 on, `master` is protected.** Every task ends with a pull request, including a manual task. Its pull request ticks the task and holds the evidence.

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
    - For T12 and T17: `RequestReader.MessageFor` treats a `JsonException` with an inner exception as broken JSON, so that a body cut off inside the board does not get the board message. This is too broad once the `TipFor` and `Version` paths are added. Plan 3.5 says which inner exceptions count. The functions stay static, creating services with `new`, until T17.
    - For T17: the owner approved strict number reading on 2026-11-04 (plan 3.3). Until then, `"Version": "2"` is accepted, as it is today.
    - Evidence: this task's pull request.

- [x] **T2 — Add the error handling and request logging**
  - Refs: spec 6.3, spec 6.4 items 1, 6, plan 2, plan 3.2, plan 4, plan 5, tests 6, 34
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
    - `RequestLoggingMiddleware` logs right after the function, and reads the status from the returned `IResult`, because the host writes it later. So `DurationMs` leaves out writing the response. A local probe showed that logging from `Response.OnCompleted` does not reach the console. The owner approved this on 2026-11-05, and spec 6.4 item 1 was changed to match.
    - A request cancelled through its token writes no request line, because there is no response to time. No test covers `Invoke` or the middleware order, because a fake context cannot supply the invocation result. A local run checks them.
    - For T7: the exporter has not run against Azure. Confirm that the request line reaches Application Insights with `Route`, `StatusCode` and `DurationMs` in `customDimensions`, with one line for each request, correct statuses, and no Information lines from the host. Compare the volume with the estimate in plan 8.1, because the exporter also sends traces and metrics.
    - Evidence: this task's pull request.

- [x] **T3 — Build and test every pull request**
  - Refs: spec 3 goal 2, spec 6.4 item 7, spec 9, plan 6, plan 8.4, risk 11
  - Depends on: T1
  - Done when:
    - `.github/workflows/build-and-deploy.yml` builds and tests on every pull request and on a push to `master`, with the differences in plan section 8.4. It has no deploy job yet.
    - NuGet Audit warnings are errors in CI.
    - A pull request shows the result.
    - The owner approves, and `master` is protected as in plan section 6.
  - Verify: `gh pr checks` on this task's pull request shows the build passed. Push a temporary failing test to the branch, confirm that the check fails, then remove the test and confirm that it passes again. After the merge, `gh api repos/OllieRumbol/DraughtsGameFunction/branches/master/protection --jq '{checks: .required_status_checks.contexts, approvals: .required_pull_request_reviews.required_approving_review_count, admins: .enforce_admins.enabled, forcePush: .allow_force_pushes.enabled, deletions: .allow_deletions.enabled}'` shows the settings in plan section 6.
  - Notes: The owner approved the protection on 2026-11-05, and it is on, with the same settings as PickTime.
    - The publish and upload steps in PickTime's workflow are left out here, because only the deploy job needs them. T5 adds them.
    - Evidence: pull request 18 for the workflow, and this tick's follow-up pull request for the `gh api` result.

- [x] **T4 — Create the Azure resources (manual)**
  - Refs: spec 2.6, spec 6.4 items 4–6, plan 2, plan 6, plan 8.1, plan 8.2, risks 2, 4
  - Depends on: —
  - Done when:
    - The region check in plan section 8.2 shows that UK South offers Flex Consumption with .NET 10. If it does not, stop and tell the owner, because the spec requires UK South. The list of default resource groups from before the work is kept for the check below.
    - The resources in plan section 8.1 exist, other than the budgets, the action group and the Logic App. The final names and the new URL are recorded in plan section 8.1.
    - The smart detection rule and action group that Application Insights creates are deleted, if they exist, as plan section 8.1 says.
    - The allowances in plan section 8.1 were checked again on the Azure pricing pages. Any change is recorded in the plan.
    - The app has a maximum of 1 instance, 512 MB and a per-instance concurrency of 8.
    - HTTPS Only is on, and SCM and FTP basic authentication are off.
    - CORS allows only `https://ollierumbol.github.io`.
    - The workspace's daily cap is 0.1 GB.
    - The app's logs go to `appi-draughts-game`, and creating the app made no second Application Insights resource or new default resource group.
    - The identity has its federated credential and its role on this app only, with the subject form this repository sends.
    - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` are GitHub secrets (plan 6).
  - Verify: `az resource list -g rg-draughts-game --query "[].{name:name,location:location}" -o table` shows every resource in UK South, and only one Application Insights resource; `az group list --query "[?starts_with(name,'DefaultResourceGroup')].name"` shows no group that was not on the list from before the work; `az functionapp scale config show -n func-draughts-game -g rg-draughts-game` shows the instance limits; `az functionapp cors show -n func-draughts-game -g rg-draughts-game` shows one origin; `az resource show -g rg-draughts-game -n func-draughts-game --resource-type Microsoft.Web/sites --query properties.httpsOnly` shows `true`; `az resource show -g rg-draughts-game --name scm --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies --parent sites/func-draughts-game --query properties.allow`, and the same with `--name ftp`, show `false`; `az monitor log-analytics workspace show -n log-draughts-game -g rg-draughts-game --query workspaceCapping.dailyQuotaGb` shows `0.1`; `az identity federated-credential list --identity-name id-draughts-game-deploy -g rg-draughts-game --query "[].subject"` shows the subject in plan 8.2; `az role assignment list --assignee <the identity's principal id> --all --query "[].{role:roleDefinitionName,scope:scope}"` shows one role, on this app; `gh secret list` shows the three names; the portal shows `appi-draughts-game` linked to the app
  - Notes: The owner created the resources in the portal on 2026-11-06, and Claude checked them read-only (plan 8.2). The owner's `az login` needs `--tenant`, because the default directory has no subscription.
    - This is a public repository, so do not put an id or a connection string in this file or a pull request, including when pasting command output.
    - For T5: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` have only their names checked. A wrong value fails at the first sign-in.
    - For T6: until then, the single instance is the only cost limit.
    - Evidence: this task's pull request.

- [x] **T5 — Deploy on every merge to `master`**
  - Refs: spec 3 goal 2, spec 6.4 item 7, spec 9, plan 8.4, risk 6
  - Depends on: T2, T3, T4
  - Done when:
    - The build job also has PickTime's publish and upload steps, including `include-hidden-files`.
    - The workflow has a deploy job that runs only on a push to `master`, and needs the build job, so a failing test stops the deployment.
    - It signs in with OpenID Connect, and no credential is stored in the repository or in GitHub secrets. The three Azure ids come from `secrets.`, and are not credentials (plan 6).
    - The sign-in and deploy actions are pinned to commit hashes, and the job fails if the deployed app does not answer (plan 8.4).
    - Merging this task deploys the app, and the new URL answers.
  - Verify: `gh run view <id> --json jobs` on the merge's run shows the `Deploy` job succeeded, because the run's overall result stays green if a job is skipped; then a POST of the spec section 5.2 request to the new URL returns the four squares; SCM basic authentication is still off, by the command in T4; the deploy log shows the three ids masked
  - Notes: Website Contributor is enough for Flex Consumption (risk 6 closed). The two pinned action hashes are updated by hand (plan 8.4).
    - For T7: one POST with `Version: 9` returned an empty 404 just after the first deployment, and 15 further requests of 5 kinds were all correct. The cause is not known. Record any 404 in T7's checks.
    - Evidence: pull request 22 for the workflow, and this tick's follow-up pull request for the deployment.

- [x] **T6 — Add the cost limits (manual)**
  - Refs: spec 6.4 items 4–5, spec 9, plan 6, plan 8.1, plan 8.3, risk 5
  - Depends on: T4, T5
  - Done when:
    - The allowances for the action group and the Logic App in plan section 8.1 were checked again on the Azure pricing pages. Any change is recorded in the plan.
    - The £1 budget emails the owner on actual and forecast cost.
    - The £3 budget calls the action group, and the action group calls the Logic App.
    - The Logic App's identity has its role on this app only.
    - The Logic App, run by hand, stops the function app. The app is then started again and answers.
    - The Logic App's trigger URL is not in the repository.
  - Verify: `az resource show -g rg-draughts-game -n func-draughts-game --resource-type Microsoft.Web/sites --query properties.state` shows `Stopped` after the manual run and `Running` after the restart; the link from the budget to the action group is checked in the portal
  - Notes: The owner built these in the portal on 2026-11-06. Budgets and the action group are global (spec 6.4 item 4).
    - The forecast alert is on the £1 budget only, so a forecast never stops the app (spec 6.4 item 5).
    - For T8: the web app's general message while the app is stopped is checked there (plan 8.3). The Logic App's trigger URL stops the app, so keep it out of the repository and chat.
    - Evidence: this task's pull request.

- [x] **T7 — Check the new app directly (manual)**
  - Refs: spec 5, spec 6.4 items 2, 6, plan 5, plan 7.3 steps 1, 2, 4, 5, plan 8.5 step 1, risks 1, 2, 12
  - Depends on: T5
  - Done when:
    - The requests in spec section 5 give the captured responses from the new URL.
    - A scripted game plays at Versions 1, 2 and 3, as plan 7.3 step 2 says. Each game finishes, and each move passes the script's check.
    - The response times at depth 4 and depth 5 are recorded and compared with the live app's, using client-side timings for both. A warm-up request comes first, and its time is recorded as the cold start.
    - 12 requests at once are sent, and each client-side time and status code is recorded.
    - The logs pass the check in plan 7.3 step 5.
  - Verify: the results above, in the pull request. The owner decides whether the speed is good enough to switch.
  - Notes: The owner approved the switch on 2026-11-07, and kept 512 MB and 1 instance (risks 1, 2).
    - The new app is about twice as fast as the old one at depth 4 and 5, with identical moves, mostly because it is near the players. Cold start 1.3 s.
    - 12 requests at once sometimes wait 25 to 36 seconds before the function starts, which the logs do not show. 8 at once did not. T18 tests this again.
    - The request logs and the host's records agree, one line for each request.
    - Evidence: this task's pull request.

- [x] **T8 — Release the web app with the new URL (manual)**
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
  - Notes: The live game switched on 2026-11-07, through draughtsgame pull request 57 and a release by hand. The old workflow failed at its deploy step (risk 3 closed).
    - Two 500s came from Easy at the end of a game, when player 2 had no moves. Plan 4 accepts this until T12. They were 400s on the old app, and the web app shows the same general message for both.
    - The owner saw a couple of web app bugs not caused by this work. They belong to the frontend spec.
    - Rollback until T9: restore the old URL and release again.
    - Evidence: this task's pull request.

- [x] **T9 — Delete the old resources (manual)**
  - Refs: spec 9, plan 8.5 step 6
  - Depends on: T8, plus a few days with no problems
  - Done when:
    - The owner confirms that the old resources can go.
    - `DraughtsGameFunction20210321121511ResourceGroup` is deleted.
    - The old publish profile files under `DraughtsGameFunction/Properties` are removed from the repository.
    - The live site still plays a game.
  - Verify: `az group exists -n DraughtsGameFunction20210321121511ResourceGroup` returns `false`
  - Notes: Done on 2026-11-07, the same day as the switch, not after a few days. The owner chose this, knowing it ends the rollback to the old app. The owner deleted the group in the portal.
    - The group held the old app, its plan and its storage account (the storage was in East US).
    - Only the old app's deployment template is removed from `Properties`. The two `serviceDependencies` files are general Visual Studio settings, so they stay.
    - Evidence: this task's pull request.

## Phase 2 — Move generation

Goal: one move generator follows the house rules, defects 1, 2, 5, 9 and 10 are fixed, and all request validation is in place.

- [x] **T10 — Add the new move generator beside the old code**
  - Refs: spec 2.1, spec 2.2, spec 6.1.3, spec 6.2 examples 3, 4, 9, 17–20, spec 6.5 items 1, 6, 7, plan 3.1, plan 3.2, plan 3.3, plan 3.4, risk 9, tests 8–12
  - Depends on: T8
  - Done when:
    - `Engine/Board.cs`, `Engine/Move.cs` and `Engine/MoveGenerator.cs` exist as in plan section 3.3. No existing code calls them yet.
    - Tests 8–11 pass.
    - Test 12 passes. Its only differences between old and new are defects 1 and 10, and each one is listed in the pull request.
  - Verify: the standard test command
  - Notes: Do not change the house rules (spec 2.1). Captures stay optional, a chain may stop after any jump, a man cannot capture a king, and a king may land back on its start square.
    - The engine files start with `#nullable enable`, because the service project has no `Nullable` setting. `Generate` and `HasMove` throw for a player other than 1 or 2, or a board that is not 64 squares.
    - Test 12 compares about 14,400 positions and finds 1 defect 10 and 25 defect 1 differences, listed in pull request 28. It skips positions that could crash the old code (defect 9).
    - T11 deletes `MoveGeneratorComparisonTests` with the old code.

- [x] **T11 — Switch to the new move generator and remove the old code**
  - Refs: spec 3 goal 4, spec 4.1 defects 1, 5, 9, 10, spec 6.1.4, spec 6.2 examples 1, 21, plan 3.1, plan 3.3, plan 7.1, risks 9, 12, tests 12, 30
  - Depends on: T10
  - Done when:
    - `FindMove.FindAvailableMoves` calls `MoveGenerator`, with the same signature.
    - `PlayerTipsService` leaves out a move that ends where it began. Test 30 passes.
    - `CheckMove`, `Tree`, `KingTree` and `TreeTake` are removed, with test 12's copy of the old behaviour.
    - Each case in `CheckMoveTests` moves to `FindMoveTests`, with the same board and the same expected move.
    - No exception is used to detect the board edge.
  - Verify: the standard test command; then `git grep -n -E "CheckMove|KingTree|TreeTake" -- "*.cs"` finds only the moved test names, if any
  - Notes: The old search runs on the new generator until T17. Its board update clears the start square first, so a move that ends where it began keeps the king.
    - For T12: `FindAvailableMoves` returns an empty list for a player other than 1 or 2, which keeps `Run_NoTipFor_ReturnsEmptyListAsToday` passing until rule 5.
    - For T12: `Board.FromRows` reads a value outside 0 to 6 as a blocked square. A piece on a light square can now move; rule 4 does not reject it, and the web app never sends it.
    - `Takes` is in board order, and moves come in a new order. The web app clears all takes at once (`CompleteBoard.js`), so neither matters.
    - Evidence and reviews: pull request 29.

- [x] **T12 — Add the remaining validation and the no-move error**
  - Refs: spec 3 goal 4, spec 4.1 defect 2, spec 6.1.2 rules 4–5, spec 6.1.4 item 3, spec 6.1.5 item 3, spec 6.2 example 5, spec 6.5 item 2, plan 3.5, plan 4, tests 25, 28
  - Depends on: T11
  - Done when:
    - A board value outside 0 to 6, and a `TipFor` that is not 1 or 2, return the messages in plan section 4.
    - No legal move for player 2 returns the 400 in plan section 4.
    - GetPotentialMoves returns an empty list with `successful: true` when the player has no legal move.
    - Tests 25 and 28 pass.
  - Verify: the standard test command
  - Notes:
    - The owner approved on 2026-11-10 that a `Depth` missing or less than 1, for `Version` 2 or 3, returns 400 `Depth must be 1 or more.` (spec 6.1.2 rule 7, plan 4). For T17: remove it when `Depth` is no longer read.
    - For T17: until stage 2, Easy plays as the request's `Player`, so the no-move check covers Easy only when `Player` is 2. Easy with another `Player` and no move still returns 500.
    - `RequestReader.MessageFor` follows plan 3.5. Only `$.Board` and `$.TipFor` have their own message; T17 adds `$.Version` and `$.Difficulty`.
    - Evidence and reviews: pull request 30.

## Phase 3 — Search

Goal: one shared search replaces the V2 and V3 code, the API accepts `Difficulty`, and the response-time limits are met on the live app.

- [x] **T13 — Add the alpha-beta search at a fixed depth**
  - Refs: spec 2.1 item 6, spec 6.1.5 items 5, 7, 9, spec 6.5 item 9, plan 1.1, plan 3.1, plan 3.2, plan 3.3, plan 3.7, tests 13, 19, 20
  - Depends on: T11
  - Done when:
    - `IEvaluator`, `MaterialEvaluator`, `SearchLimits`, `SearchResult` and `GameTreeSearch` exist as in plan section 3.3. No existing code calls them yet.
    - The search can play as either player.
    - Wins and losses follow spec section 2.1, item 6, with the fastest win and the slowest loss preferred.
    - Tests 13, 19 and 20 pass.
  - Verify: the standard test command
  - Notes: Test 13 compares with a plain minimax in the test project (`PlainMinimax.cs`).
    - For T14: `GameTreeSearch` does not read `TimeProvider` or the tie breaker yet. `Search` needs `MaxDepth` from 1 to `MaxPly` (64) and throws for a `TimeLimit`. It reallocates its buffers on each call, and checks `HasMove` before `Generate` at each node.
    - For T15: no move ordering yet. The root must keep ties in generator order.
    - For T16: `Search` throws when the player has no legal move, and does not check the root for the end of the game.
    - Evidence and reviews: pull request 31.

- [x] **T14 — Add the time limit, iterative deepening and ties**
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
    - The one-move return is in `GameTreeSearch`, so test 13 skips one-move positions and two test 20 positions gained a second move.
    - The clock is also read before each depth. `Search` needs a maximum depth, a time limit or both, and stops at `MaxPly` without a depth. It stops on a forced win, or when every move loses (approved 2026-11-10, plan 3.7 item 6).
    - For T15: quiescence must count positions and check the clock as `Negamax` does, and the table must not store a score from a stopped depth.
    - For T16: with one legal move, or when depth 1 did not finish, `SearchResult` has `CompletedDepth` and `Score` of 0.
    - Evidence and reviews: pull request 32.

- [x] **T15 — Add the transposition table, quiescence and the first Hard evaluation**
  - Refs: spec 2.1 items 1, 4, spec 6.1.5 item 6, plan 3.1, plan 3.5, plan 3.6, plan 3.7, risk 10, tests 31, 32
  - Depends on: T14
  - Done when:
    - `TranspositionTable` exists as in plan section 3.7, with one table for each search and 64-bit keys compared in full.
    - `PositionalEvaluator` uses the first values in plan section 3.5.
    - Quiescence runs when `UsesQuiescence` is true, and allows standing pat.
    - Tests 13, 14 and 19 still pass with the table on, and tests 31 and 32 pass.
    - The depth reached with the table is measured locally on the positions in plan section 3.6, and the result is in the pull request.
  - Verify: the standard test command
  - Notes:
    - Also done: move ordering (plan 3.7 item 4). Plan 3.5 details (a 4 MB table, and the Hard evaluation details the plan left open) were approved by the owner on 2026-11-10. Plan 3.6 has the measured depth.
    - Test 13 also runs with the Hard evaluation, and with kings at depths 5 and 6. Its reference uses full-window alpha-beta in quiescence. Test 31 uses material scores with quiescence on. The exact-depth rule and the position counting in quiescence are checked by inspection.
    - For T16: each new `GameTreeSearch` allocates a 4 MB table, and about one in 3 new searches caused a full garbage collection. A search can be reused, because each call clears its table.
    - For T18: look for garbage-collection pauses; the first choice is to reuse tables between requests. For T21: the evaluation values are tuned there.
    - Evidence and reviews: pull request 33.

- [x] **T16 — Add `ComputerPlayer` and the search settings**
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
    - Placement: `Difficulty` in the models project under `Enums`, `SearchOptions` under `Options` (depth 1 to 64, tied to `MaxPly` by a test; time limits 1 to 10,000 ms), and `ComputerPlayer` and `ComputerMove` in the service under `Instance`.
    - Also done: `FindMove.ToNextMove` is public. New packages `Microsoft.Extensions.Options` and `Microsoft.Extensions.Options.DataAnnotations`, 10.0.10. Test 7 starts a Generic Host with the `Program.cs` registration; the real worker was not started with a bad setting.
    - For T17: inject `ComputerPlayer`. It returns null with no legal move, and throws for a bad player, board or difficulty.
    - For T20: `ComputerPlayer` has no fixed-depth mode with no randomness (spec 6.1.5 item 9), and creates a new 4 MB search for each call.
    - Evidence and reviews: pull request 34.

- [x] **T17 — Switch GetNextMove to the new contract and remove the old services**
  - Refs: spec 3 goal 5, spec 4.1 defects 3, 4, 6, 7, 8, spec 6.1.1 items 5–8, spec 6.1.2 rule 6, spec 6.2 examples 2, 6, 7, 10–15, spec 6.5 item 5, plan 3.1, plan 3.3, plan 3.4, plan 3.5, plan 4, plan 7.1, tests 21, 22, 26, 27
  - Depends on: T12, T16
  - Done when:
    - GetNextMove takes `Difficulty` and `Board`, with `Version` as the fallback. `Depth` and `Player` have no effect, and the web app's current request still works.
    - `DifficultyJsonConverter` exists, the shared JSON options read numbers strictly as in plan section 3.3, and the error messages match plan section 4.
    - `AutomatedPlayerServiceV1`, `V2` and `V3`, `IAutomatedPlayerService`, `MinimaxOutcome` and `PotentialNextMove` are removed.
    - The misspelled `Intstance` namespace is gone.
    - Both functions get their services through dependency injection, not `new`.
    - The `Evaluate` tests move, and `GetAvailableBoardsTest` is deleted, as in plan section 7.1.
    - Test 3 now checks the stage 2 message for `Version: 9` (spec 6.2 example 6). Example 15 is also covered through the function.
    - Tests 21, 22, 26 and 27 pass.
  - Verify: the standard test command; then `git grep -n -E "Intstance|AutomatedPlayerServiceV[123]" -- "*.cs"` returns nothing
  - Notes:
    - The merge changes the live game. Play a game at each level after the deployment.
    - The T12 `Depth` rule is gone. Easy plays player 2 whatever `Player` says (test 26). A `Difficulty` of null counts as missing, and `Version` is checked only when it is used.
    - Example 13 uses a board where Easy's forced capture loses at once. `PlayerTips` gets `IPlayerTipsService` through `AddPlayerTips`; the interface was kept.
    - Checked locally with `func start`: the web app's requests at each level returned 200, and a bad `Search` setting stopped the real worker at start-up, which closes the T16 caveat.
    - Evidence and reviews: pull request 35.

- [x] **T18 — Measure the search on the live app (manual)**
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
    - Run on 2026-11-12, after pull request 36 deployed the line, at 512 MB and 1 instance. From the logs: Normal P95 185 ms at depth 6 every time; Hard P95 1,078 ms at depth 10.1 on average (9 to 12); 12 Hard requests at once all 200, slowest 1,197 ms logged and 3.5 s on the client, depth 8.1. Every request finished within its limit plus 200 ms, so no setting changed.
    - Read through the Log Analytics query API (`AppTraces` and `Properties`, plan 5). Time windows separate the levels; `OperationId` pairs each request with its depth. The script and all queries are in pull requests 36 and 37.
    - `--time-scale` is 0.35: on the owner's machine Hard reaches 10.0 at 300 ms and 10.2 at 400 ms on the same positions, and Normal still reaches depth 6 at 175 ms.
    - For T19, revert all of it: the log call and the `ILogger<AutomatedPlayer>` parameter, the `appsettings.json` entry, the three temporary tests, and `.AddLogging()` in `FunctionServicesTests`. T19's grep also lists the `CompletedDepth` property in `ComputerMove` and `ComputerPlayer`, which are not log calls. `AppSettings_LogLevels_KeepOnlyTheRequestLineAtInformation` could then check that no other category is at Information.

- [x] **T19 — Remove the temporary log line**
  - Refs: spec 6.4 item 6, plan 5, plan 7.3 step 3
  - Depends on: T18
  - Done when:
    - The log line from T18 is removed, and the change is deployed.
    - The logs show only the per-request line, warnings and errors.
  - Verify: the standard test command; then `git grep -n "CompletedDepth" -- "DraughtsGameFunction/*.cs" "DraughtsGameFunctionService/Instance/*.cs"` finds no log call
  - Notes:
    - The four files T18 changed went back to their state before T18 (`d3f31b2`). The grep lists only the `CompletedDepth` property in `ComputerMove` and its uses in `ComputerPlayer`.
    - Also done: `AppSettings_LogLevels_KeepOnlyTheRequestLineAtInformation` fails if any category other than the request line logs below Warning (plan 5).
    - Checked after the deploy on 2026-11-12: 6 requests (Easy, Normal, Hard, the web app's `Version` 3 request, a 400 and GetPotentialMoves) gave 6 Information lines, all the request line, and 2 host health-check warnings from the old instance stopping. No depth line.
    - Evidence and reviews: pull request 38, and this follow-up pull request.

## Phase 4 — Levels

Goal: each level is measurably stronger than the one below it, and the definition of done is met.

- [x] **T20 — Add the self-play tool**
  - Refs: spec 6.1.5 item 9, spec 6.4 item 3, plan 3.1, plan 7.4, test 24
  - Depends on: T16
  - Done when:
    - `DraughtsGameFunctionSelfPlay` is in the solution, and takes the options in plan section 7.4.
    - It follows the game rules in spec section 6.4, item 3, and opens each game with the random moves in plan section 7.4.
    - It prints each pairing's score, and each side moves first in half of the games.
    - Test 24 passes.
  - Verify: the standard test command; then `dotnet run --project DraughtsGameFunctionSelfPlay -c Release -- --games 2 --time-scale 0.1` finishes and prints the scores
  - Notes:
    - The tool plays each level through the same `ComputerPlayer` as the API, so it measures the real levels, including a new search for each move. Tests use fixed-depth searches with no randomness (spec 6.1.5 item 9).
    - Player 1 always moves first, and the levels swap sides each game, so `--games` must be even. A move is one player's turn, as in plan 3.7, so the 50-move draw is 25 turns each. Both draw rules count every move, including the 4 random opening moves. Every move is checked against the legal moves.
    - `SelfPlayGame.Play` takes the number of opening moves (4 by default) and the move limit (200 by default), so tests can keep a built position and reach the limit. The tool always uses the defaults. The runs are not seeded, so each run plays different games.
    - For T21: the tool scales the code defaults in `SearchOptions`, not the app's settings, so a tuned value must change the code default. The Verify run (2 games each, time scale 0.1) took 14 s, and the stronger level won all 4 games. Evidence and reviews: pull request 40.

- [x] **T21 — Tune the levels and meet the strength targets**
  - Refs: spec 2.3, spec 3 goal 6, spec 6.1.5 items 5, 6, spec 6.4 item 3, plan 3.5, plan 7.4, risk 8
  - Depends on: T18, T20
  - Done when:
    - The final `PositionalEvaluator` values are in the code and in plan section 3.5.
    - Hard scores at least 80% against Normal, and Normal at least 90% against Easy, over 200 games for each pairing.
    - The owner has played each level, and is happy with how each one feels.
  - Verify: the command in plan section 7.4, with the `--time-scale` from T18; the scores go in the pull request
  - Notes:
    - Self-play at `--time-scale 0.35` met both targets with the first values (Hard 96.2% against Normal, Normal 100% against Easy), so no value, depth or time limit changed, and the T18 speed check still applies.
    - The owner confirmed on 2026-11-13 that a move in the draw rules is one player's turn. Spec 6.4 item 3 says so.
    - The owner found Hard a little easier than the old one. The new Hard beat the old Hard (V3 at depth 5) 92% over 100 games, and the owner kept Hard as it is. Easy and Normal feel right (2026-11-13).
    - Evidence and reviews: pull request 41.

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
