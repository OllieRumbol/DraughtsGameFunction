﻿using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;
using DraughtsGameFunctionService.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DraughtsGameFunctionService.Intstance
{
    public class AutomatedPlayerServiceV3 : IAutomatedPlayerService
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

        public Int64 Evaluate(Int64[,] board)
        {
            const Int64 numberOfRows = 7;
            const Int64 kingBonusPoints = 3;

            Int64 player1Counter = 0;
            Int64 player2Counter = 0;

            for (int i = 0; i < board.GetLength(0); i++)
            {
                for (int j = 1 - (i % 2); j < board.GetLength(1); j++)
                {
                    Int64 piece = board[i, j];
                    if (piece == 1)
                    {
                        player1Counter += 5 + (numberOfRows - i);
                    }
                    else if (piece == 2)
                    {
                        player2Counter += 5 + i;
                    }
                    else if (piece == 3)
                    {
                        player1Counter += 5 + numberOfRows + kingBonusPoints;
                    }
                    else if (piece == 4)
                    {
                        player2Counter += 5 + numberOfRows + kingBonusPoints;
                    }
                }
            }

            return player2Counter - player1Counter;
        }

        public MinimaxOutcome minimax(Int64[,] board, Int64 depth, Boolean minOrMax)
        {
            if (depth == 0)
            {
                return new MinimaxOutcome
                {
                    Evaluation = Evaluate(board)
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
    }
}
