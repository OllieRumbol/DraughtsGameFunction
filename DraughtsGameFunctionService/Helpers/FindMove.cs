using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class FindMove
    {
        private static readonly List<Piece> EmptyList = new List<Piece>();

        public static List<NextMove> FindAvailableMoves(Int64[,] board, Int64 player)
        {
            List<NextMove> results = new List<NextMove>();
            for (Int64 row = 0; row < board.GetLength(0); row++)
            {
                // Skip over pieces that cant be played
                for (Int64 column = 1 - (row % 2); column < board.GetLength(1); column += 2)
                {
                    Int64 piece = board[row, column];

                    if(piece == 5)
                    {
                        continue;
                    }
                    else if (piece == 1 && player == 1)
                    {
                        Boolean canMoveUpAndLeft = CheckMove.CheckMoveUpLeft(board, row, column);
                        Boolean canMoveUpAndRight = CheckMove.CheckMoveUpRight(board, row, column);
                        if (canMoveUpAndLeft)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row - 1,
                                NextWidth = column - 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveUpAndRight)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row - 1,
                                NextWidth = column + 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveUpAndLeft == false || canMoveUpAndRight == false)
                        {
                            Tree resultTree = CheckMove.CheckTakeUp(board, row, column, new Int64[] { 2 }, new Tree(new TreeTake
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                            }));
                            if (resultTree.Left != null || resultTree.Right != null)
                            {
                                List<List<TreeTake>> treeArray = Tree.TreeToArray(resultTree);
                                results.AddRange(ProcessTreeArray(treeArray));
                            }
                        }
                    }
                    else if (piece == 2 && player == 2)
                    {
                        Boolean canMoveDownAndLeft = CheckMove.CheckMoveDownLeft(board, row, column);
                        Boolean canMoveDownAndRight = CheckMove.CheckMoveDownRight(board, row, column);

                        if (canMoveDownAndLeft)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row + 1,
                                NextWidth = column - 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveDownAndRight)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row + 1,
                                NextWidth = column + 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveDownAndLeft == false || canMoveDownAndRight == false)
                        {
                            Tree resultTree = CheckMove.CheckTakeDown(board, row, column, new Int64[] { 1 }, new Tree(new TreeTake
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                            }));
                            if (resultTree.Left != null || resultTree.Right != null)
                            {
                                List<List<TreeTake>> treeArray = Tree.TreeToArray(resultTree);
                                results.AddRange(ProcessTreeArray(treeArray));
                            }
                        }
                    }
                    else if ((piece == 3 && player == 1) || (piece == 4 && player == 2))
                    {
                        Boolean canMoveUpAndLeft = CheckMove.CheckMoveUpLeft(board, row, column);
                        Boolean canMoveUpAndRight = CheckMove.CheckMoveUpRight(board, row, column);
                        Boolean canMoveDownAndLeft = CheckMove.CheckMoveDownLeft(board, row, column);
                        Boolean canMoveDownAndRight = CheckMove.CheckMoveDownRight(board, row, column);

                        if (canMoveUpAndLeft)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row - 1,
                                NextWidth = column - 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveUpAndRight)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row - 1,
                                NextWidth = column + 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveDownAndLeft)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row + 1,
                                NextWidth = column - 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveDownAndRight)
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                                NextHeight = row + 1,
                                NextWidth = column + 1,
                                Takes = EmptyList
                            });
                        }
                        if (canMoveUpAndLeft == false || canMoveUpAndRight == false || canMoveDownAndLeft == false || canMoveDownAndRight == false)
                        {
                            Int64[] countersToTake = piece == 3 ? new Int64[] { 2, 4 } : new Int64[] { 1, 3 };
                            KingTree resultTree = CheckMove.CheckKingTake(board, row, column, row, column, countersToTake, new KingTree(new TreeTake
                            {
                                CurrentHeight = row,
                                CurrentWidth = column,
                            }));
                            if (resultTree.DownLeft != null || resultTree.DownRight != null || resultTree.UpLeft != null || resultTree.UpRight != null)
                            {
                                List<List<TreeTake>> treeArray = KingTree.KingTreeToArray(resultTree);
                                results.AddRange(ProcessTreeArray(treeArray));
                            }
                        }
                    }
                }
            }

            return results;
        }

        public static List<NextMove> ProcessTreeArray(List<List<TreeTake>> treeArray)
        {
            List<NextMove> takeMoves = new List<NextMove>();

            foreach(List<TreeTake> takes in treeArray)
            {
                takeMoves.AddRange(ProcessTakeMove(takes));
            }

            return takeMoves;
        }

        public static List<NextMove> ProcessTakeMove(List<TreeTake> takeMoves)
        {
            List<NextMove> results = new List<NextMove>();

            List<Piece> takes = new List<Piece>();

            foreach(TreeTake take in takeMoves.Skip(1))
            {
                takes.Add(new Piece
                {
                    Height = take.TakeHeight,
                    Width = take.TakeWidth
                });

                results.Add(new NextMove
                {
                    CurrentHeight = takeMoves.First().CurrentHeight,
                    CurrentWidth = takeMoves.First().CurrentWidth,
                    NextHeight = take.CurrentHeight,
                    NextWidth = take.CurrentWidth,
                    Takes = new List<Piece>(takes)
                });
            }

            return results;
        }
    }
}