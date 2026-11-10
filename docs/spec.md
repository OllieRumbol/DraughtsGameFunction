# Draughts Game Function — Specification

> **What this document is for:** It answers *what are we building, and why?* It is agreed before any design or code, and it is the source of truth for requirements.

Status: Approved
Last updated: 2026-11-06

---

## 1. Purpose

Draughts Game Function is a small, free HTTP API for the Draughts Game web app. It does two jobs:

1. It plays as the computer opponent, at three difficulty levels.
2. It tells the web app which squares a player can move to, so the web app can highlight them.

This work moves the API to a supported platform in the UK, adds automated build, test and deployment, fixes known defects, and makes the computer opponent faster and its difficulty levels clearly different.

---

## 2. Background

### 2.1 House rules

The owner built the game in memory of their grandad, who died in 2020. The game uses the rules they played together. These rules are fixed. Do not change them to standard English draughts.

1. Captures are optional. A player may make a normal move when a capture is available.
2. A multi-jump may stop after any jump. Most players continue, but they do not have to.
3. Men move and capture diagonally forward only. Kings move and capture one square diagonally in any direction. There are no "flying" kings. During a capture chain, a king may land back on the square where the chain started, because it has left that square. The owner confirmed this on 2026-11-04.
4. A man can capture only men. It cannot capture a king. A king can capture both men and kings.
5. A man that reaches the far row becomes a king.
6. **The end of the game.** After every move, the web app checks both players:
   * If neither player can move, the game is a tie.
   * If only one player can move, that player wins.

   A player with no pieces left has no moves, so losing every piece is covered by this rule. So is a move that leaves the moving player's own pieces with no moves: that player loses at once. A player can also give up in the web app. There is no other draw rule.

### 2.2 Board encoding

The board is 8 rows by 8 columns. `board[row][column]`, with row 0 at the top.

| Value | Meaning |
| --- | --- |
| 0 | Light square. Never used in play. |
| 1 | Player 1 man. Player 1 starts at the bottom (rows 5 to 7) and moves up, towards row 0. |
| 2 | Player 2 man. Player 2 starts at the top (rows 0 to 2) and moves down, towards row 7. |
| 3 | Player 1 king |
| 4 | Player 2 king |
| 5 | Empty dark square |
| 6 | Empty dark square that the web app has highlighted as a possible move |

Dark squares are at odd columns on even rows, and at even columns on odd rows.

Starting board:

```json
[[0,2,0,2,0,2,0,2],
 [2,0,2,0,2,0,2,0],
 [0,2,0,2,0,2,0,2],
 [5,0,5,0,5,0,5,0],
 [0,5,0,5,0,5,0,5],
 [1,0,1,0,1,0,1,0],
 [0,1,0,1,0,1,0,1],
 [1,0,1,0,1,0,1,0]]
```

### 2.3 Players and difficulty levels

The human is player 1. The computer is always player 2. The human chooses who moves first in the web app's settings. The API does not need to know who moved first, because each request carries the current board.

| Level | Intended feel |
| --- | --- |
| Easy | Plays at random, but takes a capture when one is available. For new and young players. |
| Normal | Plays sensibly, but with a simpler view of the position than Hard, and less time to think. Varies its play between games. |
| Hard | Plays the strongest move it can find in its time limit, with the fullest view of the position. |

Today the web app sends the level as a number in the `Version` field: 1 is Easy, 2 is Normal and 3 is Hard.

### 2.4 Platform deadline

Microsoft ends support for the in-process Azure Functions model on 10 November 2026. Source: [Migrate C# apps from the in-process model to the isolated worker model](https://learn.microsoft.com/azure/azure-functions/migrate-dotnet-to-isolated-model). After that date the app may continue to run, but it gets no security fixes or support. The API also targets .NET 6, which is already out of support. Microsoft recommends .NET 10 (LTS, supported until 14 November 2028) on the isolated worker model.

### 2.5 Measured performance

Measured on 2026-11-02, from the starting board (section 2.2), with the current Hard level:

| Depth | Local search time (Release build) | Exceptions thrown during the search |
| --- | --- | --- |
| 4 | 20 ms | 2,722 |
| 5 | 98 ms | 21,069 |
| 6 | 736 ms | 177,083 |
| 7 | 4,614 ms | 1,458,975 |

These local times are on .NET 10. On 2026-11-04, the same search on the same machine took 388 ms at depth 5 on .NET 6 and 118 ms on .NET 10, so the move to .NET 10 alone makes it about 3 times faster.

The live API, on .NET 6, took 1.56 s and then 0.98 s for two calls in a row at depth 5. That time includes the network. The web app uses depth 4 for Normal and depth 5 for Hard. Each extra depth costs about 7 times more. Positions in the middle of a game, with kings, have more moves than the starting board and are slower.

### 2.6 Current hosting and cost

Checked in the Azure portal on 2026-11-02:

* The function app and its hosting plan are in South Central US. Its storage account is in East US. Microsoft recommends that the storage account is in the same region as the function app.
* The subscription is an "Azure Plan" (pay-as-you-go) subscription.
* The subscription cost so far in November 2026 is £0.01, with a forecast of £0.10 for the month.
* Azure resources cannot be moved between regions. A function app in a new region is a new app with a new name and a new URL. Source: [Relocate your function app to another Azure region](https://learn.microsoft.com/azure/azure-resource-manager/management/relocation/relocation-functions).
* Pay-as-you-go subscriptions cannot have a hard spending limit. Budgets send alerts, but do not stop resources by themselves. Source: [Azure spending limit](https://learn.microsoft.com/azure/cost-management-billing/manage/spending-limit).

---

## 3. Goals and non-goals

### Goals

1. **Supported platform in the UK.** The API runs on .NET 10, on a Functions execution model that Microsoft supports, with every resource in the UK South region. This goal comes first. It is live before 10 November 2026. The only change it needs in the web app is the new API URL.
2. **Automated delivery.** Every pull request is built and tested automatically. Every merge to `master` is built, tested and deployed automatically. A failed test stops the deployment.
3. **Locked-down cost.** Hosting costs about the same as today (section 2.6), and the limits in section 6.4 stop a fault or a flood of requests from running up a bill.
4. **Correct moves.** Move generation follows the house rules in section 2.1, and every known defect in section 4.1 is fixed and covered by a regression test.
5. **Steady response times.** The computer opponent responds within the limits in section 6.4, whatever the position.
6. **Clearly different difficulty levels.** Each level is measurably stronger than the one below it, as section 6.4 sets out.

### Non-goals

* Changes to the web app, other than the new API URL. The web app work is separate and later. It includes sending `Difficulty` in place of `Version` (section 6.1.1).
* Changes to the rules in section 2.1.
* The computer playing as player 1 through the API.
* Game-over and draw detection in the API. The web app does this.
* Accounts, saved games, game history or any other stored data.
* Opening books or endgame databases.
* Paid Azure services or plan upgrades, other than the approved storage cost in section 6.4.

---

## 4. Current state of the code

| Project / Area | Purpose | State |
| --- | --- | --- |
| `DraughtsGameFunction` | The two HTTP functions | .NET 6, in-process model, `Microsoft.NET.Sdk.Functions` 4.0.1 |
| `DraughtsGameFunctionModels` | Request and response models | .NET Standard 2.0. References the Functions SDK, but does not need it. |
| `DraughtsGameFunctionService` | Move generation and the three opponents | .NET Standard 2.0. References the Functions SDK, but does not need it. Normal and Hard are copies of the same minimax code, with different scoring. No pruning. |
| `DraughtsGameFunctionTests` | MSTest unit tests | .NET 6, MSTest 2.2.7. A few tests, mainly for the starting board. |
| `.github/workflows` | CI/CD | Empty. No automated build, test or deployment. |

### 4.1 Defects and gaps

1. **A king can lose a capture.**
   `DraughtsGameFunctionService/Helpers/KingTree.cs:75` and `:85` test the same pair of branches (down-left and up-right). No line tests down-right with up-right. When a king can capture only down-right and up-right, the up-right capture is dropped. A probe confirmed this: a player 2 king at (3,2), with player 1 men at (4,3) and (2,3), is offered (5,4) but not (1,4).
2. **No legal move gives an unclear error.**
   `DraughtsGameFunctionService/Instance/AutomatedPlayerServiceV2.cs:18` (and V3) reads the best move without checking that one exists. When the computer has no legal move, or when `Depth` is 0, the API returns a null reference error.
3. **Depth has no limit.**
   `AutomatedPlayerServiceV2.cs:14` (and V3) searches to whatever depth the request sends. One request at a large depth can run for minutes and use the free allowance.
4. **The `Player` field means different things at different levels.**
   Easy plays as the player in the request. Normal and Hard ignore it and always play as player 2 (`AutomatedPlayerServiceV2.cs:14`, and V3).
5. **The board edge is detected by catching exceptions.**
   `DraughtsGameFunctionService/Helpers/CheckMove.cs:16`, `:66` and similar. Every check past the board edge throws and catches an exception. This is the largest single cost in the search (section 2.5).
6. **Normal and Hard always choose the same move from the same position.**
   `AutomatedPlayerServiceV2.cs:45` (and V3) keeps the last of several equal moves. The same opening gives the same game every time.
7. **Duplicated search code.** V2 and V3 contain the same minimax and board-update code.
8. **Misspelled namespace.** `DraughtsGameFunctionService.Intstance`.
9. **A king chain that goes round in a loop crashes the process.**
   `DraughtsGameFunctionService/Helpers/CheckMove.cs:145` (and the other directions) blocks only the immediate reverse jump. A probe confirmed the result: a player 2 king at (7,4), with player 1 men at (6,3), (4,1), (4,3), (2,1) and (2,3), ends in a stack overflow, which the code cannot catch. An anonymous request can cause it, and so can the search.
10. **The API stops a king landing back on its start square.**
    The king stays on the board while the code works out a chain, so the square does not count as empty. A probe confirmed this: a king at (1,2), with opponent men at (2,3), (4,3), (4,1) and (2,1), stops after 3 jumps. The web app lets a human's king make the 4th jump (section 2.1, item 3).

---

## 5. External interfaces

The only known client is the Draughts Game web app at `https://ollierumbol.github.io/draughtsgame`.

Current base URL: `https://draughtsgamefunction20210321121511.azurewebsites.net/api`. The new app in UK South has a new base URL, which is set when the app is created (section 2.6). The routes stay the same.

The examples below were captured from the live API on 2026-11-02, with `BOARD` set to the starting board in section 2.2.

### 5.1 Get the computer's next move

```
POST /api/GetNextMove
```

Request (as the web app sends it today):

```json
{ "Version": 2, "Board": BOARD, "Depth": 4, "Player": 2 }
```

Response, HTTP 200:

```json
{
  "nextMove": {
    "currentHeight": 2,
    "currentWidth": 7,
    "nextHeight": 3,
    "nextWidth": 6,
    "takes": []
  },
  "successful": true,
  "errorMessage": null
}
```

Response for an unknown `Version`, HTTP 400:

```json
{ "nextMove": null, "successful": false, "errorMessage": "No version in body of request" }
```

Notes:
* The request uses PascalCase names. The response uses camelCase names. Height is the row and width is the column.
* `takes` lists the squares of the captured pieces, as `{ "height": n, "width": n }`.
* The web app always sends `Player: 2`.
* The web app shows a general error message to the player when `successful` is false. It does not show `errorMessage`.

### 5.2 Get the squares a player can move to

```
POST /api/GetPotentialMoves
```

Request:

```json
{ "Board": BOARD, "TipFor": 1 }
```

Response, HTTP 200:

```json
{
  "potentialMoves": [
    { "height": 4, "width": 1 },
    { "height": 4, "width": 3 },
    { "height": 4, "width": 5 },
    { "height": 4, "width": 7 }
  ],
  "successful": true,
  "errorMessage": null
}
```

### 5.3 Authentication and CORS

* Both endpoints are anonymous. There is no key or sign-in.
* The live app allows cross-origin requests from `https://ollierumbol.github.io` only. This was checked with a CORS preflight request on 2026-11-02.

---

## 6. Functional specification

### 6.1 Behaviour

#### 6.1.1 API contract

1. THE SYSTEM SHALL keep the routes, HTTP methods, response fields and field casing shown in section 5.
2. Request field names SHALL be matched regardless of letter case. The web app sends PascalCase.
3. WHEN a request is valid, THE SYSTEM SHALL return HTTP 200 with `successful: true`.
4. WHEN a request is not valid, or no move can be returned, THE SYSTEM SHALL return HTTP 400 with `successful: false` and an `errorMessage` that names the problem.
5. The GetNextMove request changes in three stages, so that the web app keeps working at every point:

   | Stage | When | Request fields |
   | --- | --- | --- |
   | 1 | From the move to the new platform | Unchanged: `Version`, `Board`, `Depth`, `Player`. Only the URL changes. Move-generation fixes (section 4.1) may change which moves are returned before stage 2. |
   | 2 | From the release of the new search | `Difficulty` and `Board`. `Version` is still accepted when `Difficulty` is missing. `Depth` and `Player` are no longer read. A request that includes them still succeeds, and they have no effect. |
   | 3 | After the web app sends `Difficulty` | `Difficulty` and `Board` only. `Version` is removed. This stage is later work (section 3, non-goals). |

6. From stage 2, the target request is:

   ```json
   { "Difficulty": "hard", "Board": BOARD }
   ```

7. `Difficulty` accepts the names `easy`, `normal` and `hard`, in any letter case. It does not accept numbers.
8. While `Version` is accepted, it accepts the numbers 1, 2 and 3 only, with the meanings in section 2.3. Its value is used only when `Difficulty` is missing. If both are present, `Difficulty` is used. A `Version` that is not a number is rejected, even when `Difficulty` is present, because the body cannot be read.
9. The GetPotentialMoves request does not change.

#### 6.1.2 Request validation

A request is not valid when any of these is true. Each rule starts at the stage shown (section 6.1.1, item 5). The web app sends only valid requests, so no rule changes the game.

| # | Rule | From |
| --- | --- | --- |
| 1 | The body is not JSON, is the JSON value `null`, or has no `Board`. | Stage 1 |
| 2 | `Board` is not 8 rows of 8 values. | Stage 1. Today the board type cannot hold rows of different lengths, so such a board is already rejected. This rule keeps that behaviour. |
| 3 | The body is larger than 10 KB (section 6.4, item 7). | Stage 1 |
| 4 | `Board` contains a value other than 0 to 6. | Stage 2 at the latest. It may start earlier, with the move-generation fixes. |
| 5 | For GetPotentialMoves: `TipFor` is missing, or is not 1 or 2. | Stage 2 at the latest. It may start earlier, with the move-generation fixes. Until then, a missing `TipFor` returns an empty list, as it does today. |
| 6 | For GetNextMove: neither `Difficulty` nor `Version` is present, or the one that is used has a value not allowed by section 6.1.1. The message lists the allowed values, for example `Difficulty must be one of: easy, normal, hard`. | Stage 2. In stage 1, an unknown `Version` is rejected as it is today (section 5.1). |
| 7 | For GetNextMove with `Version` 2 or 3: `Depth` is missing or less than 1. | From the move-generation fixes until stage 2, when `Depth` is no longer read. Added on 2026-11-10, with the owner's approval: a search of depth 0 has no move to return (section 4.1, defect 2). |

The API does not check where pieces stand. For example, a piece on a light square is not rejected.

#### 6.1.3 Legal moves

THE SYSTEM SHALL generate the legal moves for a player using the house rules in section 2.1:

1. Squares with value 5 or 6 are empty. A piece can move or land only on an empty dark square.
2. A man moves one square diagonally forward. A king moves one square diagonally in any direction.
3. A capture jumps diagonally over an adjacent opponent piece onto the empty square behind it. Men capture forward only, and capture only men. Kings capture in all four directions, and capture men and kings (section 2.1, item 4).
4. A capture chain may continue with more jumps from the landing square. Each point where the chain can stop is a separate legal move. So a chain of three jumps gives three legal moves: after the first, second and third jump.
5. A piece cannot be captured twice in the same chain.
6. Normal moves are legal even when a capture is available.
7. When a man reaches the far row, it becomes a king and the move ends.
8. During a capture chain, the square where the king started counts as empty, so the king may land on it (section 2.1, item 3). A move can therefore end on the square where it began.
9. Two chains with the same start, the same end and the same captured pieces are one legal move.

#### 6.1.4 Move tips (GetPotentialMoves)

1. THE SYSTEM SHALL return every square that a piece of the `TipFor` player can finish a legal move on (section 6.1.3), including every stop point of a capture chain, except as item 4 says.
2. Each square SHALL appear once.
3. WHEN the player has no legal move, THE SYSTEM SHALL return an empty list with `successful: true`.
4. THE SYSTEM SHALL NOT return a square where a piece stands. So a king's own square is not a tip, even when a chain can end there. The web app writes its highlight over every square in the list (section 2.2, value 6), and that would remove the king from the board.

#### 6.1.5 Computer move (GetNextMove)

Items 1 and 2 apply from stage 1. Item 3 applies once defect 2 (section 4.1) is fixed. Items 4 to 9 describe the new search, and apply from stage 2 (section 6.1.1, item 5). Until then, the current search runs, and uses `Depth` as it does today.

1. THE SYSTEM SHALL return one legal move (section 6.1.3). From stage 2, it is always for player 2. In stage 1, Easy still plays as the player in the request (section 4.1, defect 4).
2. `takes` SHALL list every piece captured by that move.
3. WHEN player 2 has no legal move, THE SYSTEM SHALL return HTTP 400 with an `errorMessage` that says so.
4. **Easy:** THE SYSTEM SHALL choose a random capture when one or more captures are available, and a random move otherwise. It does not search ahead.
5. **Normal:** THE SYSTEM SHALL search ahead to a maximum depth of 6, and for at most the Normal time limit (section 6.4), whichever comes first. It SHALL choose the best move by the Normal evaluation. The Normal evaluation counts material only. The maximum depth is set on the server, and can be tuned without a change to the web app.
6. **Hard:** THE SYSTEM SHALL search ahead for at most the Hard time limit (section 6.4), with no maximum depth, and SHALL choose the best move by the Hard evaluation. The Hard evaluation SHALL take account of more features of the position than material.
7. **Normal and Hard:**
   * WHEN several moves share the top score exactly, THE SYSTEM SHALL choose one of them at random. It SHALL never choose a move with a lower score.
   * From the starting board, 20 Normal requests SHALL return at least 2 different moves (section 6.2, example 16).
   * WHEN player 2 has only one legal move, THE SYSTEM SHALL return it at once, without searching.
   * WHEN the search finds a forced win, THE SYSTEM SHALL stop searching and play it. It SHALL prefer the fastest win. WHEN every move loses, it SHALL prefer the slowest loss.
   * Wins, losses and ties in the search SHALL follow the end-of-game rule in section 2.1, item 6.
8. THE SYSTEM SHALL never search past the time limit for the difficulty, whatever the position or the request.
9. For testing, the search SHALL also be able to run to a fixed depth with no time limit and no randomness, so that the same position always gives the same move. It SHALL also be able to play as player 1, for the computer-against-computer games in section 6.4. Neither option is offered through the API.

### 6.2 Worked examples

`BOARD` is the starting board in section 2.2. Examples 2, 6, 7 and 10 to 16 apply from stage 2 (section 6.1.1). Examples 3, 5 and 19 to 21 apply once defects 1, 2, 9 and 10 (section 4.1) are fixed. The others apply from stage 1.

| # | Input | Expected result | Notes |
| --- | --- | --- | --- |
| 1 | GetPotentialMoves, `BOARD`, `TipFor: 1` | 4 squares: (4,1), (4,3), (4,5), (4,7) | Same as the live API today |
| 2 | GetNextMove, `BOARD`, `Difficulty: "normal"` | HTTP 200, one legal player 2 move from row 2 to row 3 | |
| 3 | Player 2 king at (3,2), player 1 men at (4,3) and (2,3), every other dark square empty. Legal moves for player 2. | 4 moves: (2,1) and (4,1) with no captures, (5,4) capturing (4,3), and (1,4) capturing (2,3) | Today (1,4) is missing (section 4.1, defect 1) |
| 4 | Player 1 man at (6,1), player 2 men at (5,2) and (3,4), every other dark square empty. Legal moves for player 1. | 3 moves: (5,0) with no capture, (4,3) capturing (5,2), and (2,5) capturing (5,2) and (3,4) | Checked against the current code. The chain may stop after the first jump. |
| 5 | GetNextMove on a board where player 2 has no legal move | HTTP 400, `successful: false`, message says player 2 has no legal moves | Today: null reference error |
| 6 | GetNextMove with `Version: 9` and no `Difficulty` | HTTP 400, message names the allowed values | |
| 7 | The web app's current request (section 5.1), with Version 3 and `Depth: 50` | HTTP 200 within the Hard time limit (section 6.4) | `Depth` has no effect |
| 8 | GetNextMove with `Board` of 7 rows | HTTP 400, message names the board size | |
| 9 | GetPotentialMoves on a board that contains value 6 | Same result as the board with those 6 values replaced by 5 | |
| 10 | GetNextMove with `Difficulty: "HARD"` | HTTP 200 | Letter case is ignored |
| 11 | GetNextMove with `Difficulty: 3` | HTTP 400, message lists `easy, normal, hard` | Numbers are not accepted |
| 12 | GetNextMove with `Difficulty: "extreme"` | HTTP 400, message lists `easy, normal, hard` | |
| 13 | GetNextMove with `Difficulty: "easy"` and `Version: 3` | HTTP 200, an Easy move | `Difficulty` is used |
| 14 | GetNextMove with neither `Difficulty` nor `Version` | HTTP 400, message says the difficulty is missing | |
| 15 | Normal or Hard, on a board where player 2 has exactly one legal move | That move, returned without a search | |
| 16 | 20 GetNextMove requests with `BOARD` at Normal, with the usual time limit and randomness | At least 2 different moves | Not in the fixed-depth test mode |
| 17 | Player 1 man at (6,1), player 2 king at (5,2), every other dark square empty. Legal moves for player 1. | 1 move: (5,0) with no capture | A man cannot capture a king. Checked against the current code. |
| 18 | Player 1 king at (6,1), player 2 king at (5,2), every other dark square empty. Legal moves for player 1. | 4 moves: (5,0), (7,0) and (7,2) with no captures, and (4,3) capturing (5,2) | A king can capture a king. Checked against the current code. |
| 19 | Player 2 king at (1,2), player 1 men at (2,3), (4,3), (4,1) and (2,1), every other dark square empty. Legal moves for player 2. | 9 moves: (0,1) and (0,3) with no captures; (3,4) capturing (2,3); (5,2) capturing (2,3), (4,3); (3,0) capturing (2,3), (4,3), (4,1); (3,0) capturing (2,1); (5,2) capturing (2,1), (4,1); (3,4) capturing (2,1), (4,1), (4,3); and (1,2) capturing all four | The chain may end where it began, and both directions give that one move. Worked out by a short script. |
| 20 | Player 2 king at (7,4), player 1 men at (6,3), (4,1), (4,3), (2,1) and (2,3), every other dark square empty. Legal moves for player 2. | 9 moves, with no crash and no piece captured twice: (6,5) with no capture, then 8 capture moves. The longest is (5,2) capturing all five. | Today this crashes the process (section 4.1, defect 9). Worked out by a short script. |
| 21 | GetPotentialMoves on the board in example 19, `TipFor: 2` | 5 squares: (0,1), (0,3), (3,0), (3,4), (5,2). Not (1,2). | Section 6.1.4, item 4 |

### 6.3 Outcomes

| Outcome | Meaning |
| --- | --- |
| HTTP 200, `successful: true` | The result is in `nextMove` or `potentialMoves`. |
| HTTP 400, `successful: false` | The request was not valid, or the computer has no legal move. `errorMessage` says which. |
| HTTP 500, `successful: false` | An unexpected fault. `errorMessage` is a general message, with no internal detail. In stage 1 this includes a fault in the current search, such as a missing `Depth`. Today those return HTTP 400. The web app shows the same general message for both. |

### 6.4 Non-functional requirements

1. **Response time.** From stage 2 (section 6.1.1, item 5), the search time SHALL be at most:
   * Normal: 500 ms
   * Hard: 1,000 ms

   Easy does not search, so it has no time limit. The limits are set on the server, and can be tuned without a change to the web app.

   **How the limits are checked on the live app.** The logs record the time from when the function starts until it returns, which takes a little longer than the search. They leave out any wait before the function starts, and writing the response, which is short. So the check allows a margin of 200 ms, and ignores single slow requests:
   * Send one warm-up request. Then send 30 requests for each of Normal and Hard, from several different positions.
   * At least 95% of the requests SHALL finish within the limit plus 200 ms: 700 ms for Normal and 1,200 ms for Hard. The times are read from the logs.
   * Cold starts do not count.

   The rule that a search never runs past its limit (section 6.1.5, item 8) is also covered by automated tests.
2. **Cold start.** The first request after the app has been idle may be slower. This is accepted.
3. **Strength.** From stage 2, in automated games of computer against computer:
   * Hard SHALL score at least 80% against Normal.
   * Normal SHALL score at least 90% against Easy.

   Rules for these games:
   * A win scores 1. A draw scores 0.5 to each side.
   * The game ends by the rule in section 2.1, item 6.
   * So that a game always ends, a game is also a draw after 50 moves in a row with no capture, or after 200 moves in total. This rule applies to these test games only, not to the API or the web app.
   * Each game starts with a few random opening moves, so the games are not all the same.
   * Each side moves first in half of the games.
4. **Location.** Every Azure resource that has a region SHALL be in the UK South region. Budgets and the action group are global, because Azure offers no UK South option for them. They hold alert settings, not game data. The owner approved this on 2026-11-05.
5. **Cost.**
   * Hosting SHALL stay within the Azure Functions free grant. The only expected charge is the storage account that Azure Functions needs, at about £0.10 a month. The owner approved this cost on 2026-11-02.
   * THE SYSTEM SHALL never run more than one instance at a time, so that a flood of requests cannot multiply the cost.
   * No part of the hosting SHALL be billed while the app is idle, other than storage.
   * An email alert SHALL be sent when the actual or forecast monthly cost reaches £1.
   * The function app SHALL be stopped automatically when the monthly cost reaches £3. After an automatic stop, the app stays stopped until the owner starts it again. While it is stopped, the web app shows its general error message.
6. **Logging.** THE SYSTEM SHALL record, for each request that gets a response, the route, the status code and the duration, and SHALL record warnings and errors. It SHALL NOT log each move or the search detail. Log storage SHALL be capped at 0.1 GB a day. If the cap is reached, logging stops for the rest of that day, and the API keeps working.
7. **Security.**
   * The endpoints stay anonymous. Cross-origin access stays limited to the web app's origin (section 5.3).
   * No deployment password, publish profile or other long-lived credential is stored in the repository or in GitHub secrets.
   * A package with a known vulnerability stops the build.
   * The request size is limited to 10 KB. A request with the starting board is about 200 bytes.
8. **Supported versions.** All packages are on versions that are supported on the date of the change.

### 6.5 Edge cases

1. The board contains value 6 squares (section 6.2, example 9).
2. Player 2 has no pieces, or pieces but no legal move (section 6.2, example 5).
3. Only kings are left, which can lead to long games. The search must still respect the time limit.
4. Several moves share the top score. Normal and Hard choose one of them at random (section 6.1.5, item 7). In the fixed-depth test mode, the choice is not random (section 6.1.5, item 9).
5. From stage 2, `Depth` or `Player` is sent with any value, including a missing, negative or very large value. It has no effect (section 6.1.1, item 5).
6. A capture chain where the man reaches the far row (section 6.1.3, item 7).
7. A king capture chain that loops, or that ends on its starting square (section 6.1.3, items 5 and 8, and section 6.2, examples 19 and 20).
8. Several requests at the same time. Requests share no state, so each one is independent. They share one instance (section 6.4, item 5), and each search still stops at its time limit.
9. A move that leaves player 2's own pieces with no moves. The search treats it as a loss (section 2.1, item 6).

---

## 7. Open questions

None. All questions were resolved with the owner on 2026-11-02.

---

## 8. Assumptions

1. The web app is the only client of the API.
2. Traffic stays at the level of a hobby game, well below the free grant. One instance is enough to serve it (section 6.4, item 5).
3. The web app does not depend on the wording of `errorMessage` (section 5.1, notes).
4. The web app clears highlighted squares (value 6) before it asks for the computer's move. The API still treats 6 as empty, in case it does not.
5. The repository's default branch, `master`, is the branch that deploys.

---

## 9. Definition of done

* The live API runs on .NET 10, on a supported Functions execution model, with every resource in UK South.
* The live web app uses the new API URL, and plays a full game at each difficulty.
* The old resource group in the US is deleted.
* A pull request runs the build and all tests automatically, and shows the result on the pull request.
* A merge to `master` deploys automatically, and a failing test stops the deployment.
* No deployment credential is stored in the repository or in GitHub secrets.
* The one-instance limit, the £1 alert and the £3 automatic stop are in place.
* Every defect in section 4.1 is fixed, and defects 1 to 4, 9 and 10 each have a regression test.
* Every worked example in section 6.2 passes as an automated test.
* The response-time limits in section 6.4, item 1 are met on the live app, by the check that item describes.
* The strength targets in section 6.4, item 3 are met by an automated computer-against-computer run.
* All hosting is within the cost limits in section 6.4, item 5.
