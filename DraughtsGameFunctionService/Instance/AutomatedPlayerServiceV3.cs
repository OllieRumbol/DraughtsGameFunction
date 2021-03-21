using DraughtsGameAPIModels;
using DraughtsGameAPIModels.Service;
using DraughtsGameAPIService.Helpers;
using DraughtsGameAPIService.Instance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DraughtsGameAPIService.Interface
{
    public class AutomatedPlayerServiceV3 : IAutomatedPlayerService
    {
        public NextMove GetNextMoveForAutomatedPlayer(GetNextMove getNextMove)
        {
            MinimaxOutcome result = minimax(getNextMove.Board, getNextMove.Depth, true, null);

            return new NextMove
            {
                CurrentHeight = result.PotentialNextMove.CurrentHeight,
                CurrentWidth = result.PotentialNextMove.CurrentWidth,
                NextHeight = result.PotentialNextMove.NextHeight,
                NextWidth = result.PotentialNextMove.NextWidth,
                Takes = result.PotentialNextMove.Takes
            };
        }

        private int evaluate(int[,] board, List<NextMove> moves)
        {
            //TODO: the list of moves is calculated before a piece is moved there it lacks the full moves information however this is due to efficiency problems  
            //Depth 4: Before: 315ms, After: 65ms
            //Depth 6: Before: 21.8s After: 3.36s
            //Depth 8: Before: 40m 8s, After: 5m 34s
            //Depth 10: 

            int player1Counter = 0;
            int player2Counter = 0;

            List<Take> takenPieces = getPiecesTaken(moves);

            const int numberOfRows = 7;
            const int kingBonusPoints = 2;

            for (int i = 0; i < board.GetLength(0); i++)
            {
                for (int j = 1 - (i % 2); j < board.GetLength(1); j++)
                {
                    int piece = board[i, j];
                    if(piece == 1)
                    {
                        if (canPieceBeTaken(takenPieces, i, j))
                        {
                            player1Counter = player1Counter + 4 + (numberOfRows - i);
                        }
                        else
                        {
                            player1Counter = player1Counter + 6 + (numberOfRows - i);
                        }
                    }
                    else if (piece == 2)
                    {
                        if (canPieceBeTaken(takenPieces, i, j))
                        {
                            player2Counter = player2Counter + 4 + i;
                        }
                        else
                        {
                            player2Counter = player2Counter + 6 + i;
                        }
                    }
                    else if (piece == 3)
                    {
                        if (canPieceBeTaken(takenPieces, i, j))
                        {
                            player1Counter = player1Counter + 2 + numberOfRows + kingBonusPoints;
                        }
                        else
                        {
                            player1Counter = player1Counter + 8 + numberOfRows + kingBonusPoints;
                        }
                    }
                    else if (piece == 4)
                    {
                        if (canPieceBeTaken(takenPieces, i, j))
                        {
                            player2Counter = player2Counter + 2 + numberOfRows + kingBonusPoints;
                        }
                        else
                        {
                            player2Counter = player2Counter + 8 + numberOfRows + kingBonusPoints;
                        }
                    }
                }
            }

            return player2Counter - player1Counter;
        }

        private List<Take> getPiecesTaken(List<NextMove> moves)
        {
            List<Take> result = new List<Take>();

            foreach(NextMove move in moves)
            {
                if (move.Takes != null)
                {
                    result.AddRange(move.Takes);
                }
            }

            return result;
        }

        private bool canPieceBeTaken(List<Take> takes, int height, int width)
        {
            return takes.Any(t => t.Height == height && t.Width == width);
        }

        public MinimaxOutcome minimax(int[,] board, int depth, bool minOrMax, List<NextMove> moves)
        {
            if (depth == 0)
            {
                return new MinimaxOutcome
                {
                    Evaluation = evaluate(board, moves)
                };
            }

            if (minOrMax)
            {
                int maxEval = -1000;
                PotentialNextMove bestMove = null;
                List<PotentialNextMove> player2MovesBoards = GetAvailableBoards(board, 2);
                foreach (PotentialNextMove player2MovesBoard in player2MovesBoards)
                {
                    MinimaxOutcome evaluation = minimax(player2MovesBoard.Board, depth - 1, false, player2MovesBoard.Moves);
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
                int minEval = 1000;
                PotentialNextMove bestMove = null;
                List<PotentialNextMove> player1MovesBoards = GetAvailableBoards(board, 1);
                foreach (PotentialNextMove player1MovesBoard in player1MovesBoards)
                {
                    MinimaxOutcome evaluation = minimax(player1MovesBoard.Board, depth - 1, true, player1MovesBoard.Moves);
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

        private List<PotentialNextMove> GetAvailableBoards(int[,] board, int player)
        {
            List<PotentialNextMove> results = new List<PotentialNextMove>();

            //Get all possible moves for a board, this is used it 2 ways 
            //1. This method will use the information to create a board for every possible move
            //2. This list of possible moves is used by the evaluation method to not redo work is massively improve efficiency

            List<NextMove> moves = FindMove.FindAvailableMoves(board);

            foreach (NextMove move in moves.Where(m =>m.Piece == player))
            {
                //Copy board
                int[,] moveBoard = (int[,])board.Clone();

                //Remove taken pieces form the board
                if (move.Takes != null)
                {
                    foreach (Take take in move.Takes)
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
                    int tempValue = moveBoard[move.CurrentHeight, move.CurrentWidth];
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
                    Takes = move.Takes,
                    Moves = moves
                });
            }

            return results;
        }
    }
}
