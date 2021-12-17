using System;
using System.Collections.Generic;
using DraughtsGameFunctionService.Interface;
using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;

namespace DraughtsGameFunctionService.Intstance
{
    public class AutomatedPlayerServiceV2 : IAutomatedPlayerService
    {
        public NextMove GetNextMoveForAutomatedPlayer(GetNextMove getNextMove)
        {
            MinimaxOutcome result = minimax(getNextMove.Board, getNextMove.Depth, true);

            return new NextMove
            {
                CurrentHeight = result.PotentialNextMove.CurrentHeight,
                CurrentWidth = result.PotentialNextMove.CurrentWidth,
                NextHeight = result.PotentialNextMove.NextHeight,
                NextWidth = result.PotentialNextMove.NextWidth,
                Takes = result.PotentialNextMove.Takes
            };
        }

        public MinimaxOutcome minimax(Int64[,] board, Int64 depth, Boolean minOrMax)
        {
            if (depth == 0)
            {
                return new MinimaxOutcome
                {
                    Evaluation = evaluate(board)
                };
            }

            if (minOrMax)
            {
                Int64 maxEval = -1000;
                PotentialNextMove bestMove = null;
                List<PotentialNextMove> player2MovesBoards = GetAvailableBoards(board, 2);
                foreach (PotentialNextMove player2MovesBoard in player2MovesBoards)
                {
                    MinimaxOutcome evaluation = minimax(player2MovesBoard.Board, depth - 1, false);
                    maxEval = Math.Max(maxEval, evaluation.Evaluation);
                    if (maxEval == evaluation.Evaluation)
                    {
                        bestMove = player2MovesBoard;
                    }
                }

                return new MinimaxOutcome
                {
                    PotentialNextMove = bestMove,
                    Evaluation = maxEval
                };
            }
            else
            {
                Int64 minEval = 1000;
                PotentialNextMove bestMove = null;
                List<PotentialNextMove> player1MovesBoards = GetAvailableBoards(board, 1);
                foreach (PotentialNextMove player1MovesBoard in player1MovesBoards)
                {
                    MinimaxOutcome evaluation = minimax(player1MovesBoard.Board, depth - 1, true);
                    minEval = Math.Min(minEval, evaluation.Evaluation);
                    if (minEval == evaluation.Evaluation)
                    {
                        bestMove = player1MovesBoard;
                    }
                }

                return new MinimaxOutcome
                {
                    PotentialNextMove = bestMove,
                    Evaluation = minEval
                };
            }
        }

        private List<PotentialNextMove> GetAvailableBoards(Int64[,] board, Int64 player)
        {
            List<PotentialNextMove> results = new List<PotentialNextMove>();

            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            foreach (NextMove move in moves)
            {
                //Copy board
                Int64[,] moveBoard = (Int64[,])board.Clone();

                //Remove taken pieces form the board
                if (move.Takes.Count > 0)
                {
                    foreach (Piece take in move.Takes)
                    {
                        moveBoard[take.Height, take.Width] = 5;
                    }
                }

                //Add kings
                if (player == 1 && move.NextHeight == 0)
                {
                    moveBoard[move.CurrentHeight, move.CurrentWidth] = 5;
                    moveBoard[move.NextHeight, move.NextWidth] = 3;
                }
                else if (player == 2 && move.NextHeight == 7)
                {
                    moveBoard[move.CurrentHeight, move.CurrentWidth] = 5;
                    moveBoard[move.NextHeight, move.NextWidth] = 4;
                }
                else
                {
                    Int64 tempValue = moveBoard[move.CurrentHeight, move.CurrentWidth];
                    moveBoard[move.CurrentHeight, move.CurrentWidth] = 5;
                    moveBoard[move.NextHeight, move.NextWidth] = tempValue;
                }

                results.Add(new PotentialNextMove
                {
                    Board = moveBoard,
                    CurrentHeight = move.CurrentHeight,
                    CurrentWidth = move.CurrentWidth,
                    NextHeight = move.NextHeight,
                    NextWidth = move.NextWidth,
                    Takes = move.Takes
                });
            }

            return results;
        }

        private Int64 evaluate(Int64[,] board)
        {
            int player1Counter = 0;
            int player2Counter = 0;

            for (int i = 0; i < board.GetLength(0); i++)
            {
                for (int j = 1 - (i % 2); j < board.GetLength(1); j++)
                {
                    if (board[i, j] == 1)
                    {
                        player1Counter++; ;
                    }
                    else if (board[i, j] == 2)
                    {
                        player2Counter++; ;
                    }
                    else if (board[i, j] == 3)
                    {
                        player1Counter += 2;
                    }
                    else if (board[i, j] == 4)
                    {
                        player2Counter += 2;
                    }
                }
            }

            return player2Counter - player1Counter;
        }
    }
}