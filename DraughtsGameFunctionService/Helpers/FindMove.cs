﻿using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class FindMove
    {
        public static List<NextMove> FindAvailableMoves(int[,] board, int player)
        {
            List<NextMove> results = new List<NextMove>();
            for (int i = 0; i < board.GetLength(0); i++)
            {
                // Skip over pieces that cant be player
                for (int j = 1 - (i % 2); j < board.GetLength(1); j += 2)
                {
                    int piece = board[i, j];
                    if (piece == 1 && player == 1)
                    {
                        if (CheckMove.CheckMoveUpLeft(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i - 1,
                                NextWidth = j - 1,
                                Takes = new List<Piece>()
                            });
                        }
                        if (CheckMove.CheckMoveUpRight(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i - 1,
                                NextWidth = j + 1,
                                Takes = new List<Piece>()
                            });
                        }

                        Tree resultTree = CheckMove.CheckTakeUp(board, i, j, new int[] { 2 }, new Tree(new TreeTake
                        {
                            CurrentHeight = i,
                            CurrentWidth = j,
                        }));
                        if (resultTree.Left != null || resultTree.Right != null)
                        {
                            List<List<TreeTake>> treeArray = Tree.TreeToArray(resultTree);
                            results.AddRange(ProcessTreeArray(treeArray));
                        }
                    }
                    else if (piece == 2 && player == 2)
                    {
                        if (CheckMove.CheckMoveDownLeft(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i + 1,
                                NextWidth = j - 1,
                                Takes = new List<Piece>()
                            });
                        }
                        if (CheckMove.CheckMoveDownRight(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i + 1,
                                NextWidth = j + 1,
                                Takes = new List<Piece>()
                            });
                        }

                        Tree resultTree = CheckMove.CheckTakeDown(board, i, j, new int[] { 1 }, new Tree(new TreeTake
                        {
                            CurrentHeight = i,
                            CurrentWidth = j,
                        }));
                        if (resultTree.Left != null || resultTree.Right != null)
                        {
                            List<List<TreeTake>> treeArray = Tree.TreeToArray(resultTree);
                            results.AddRange(ProcessTreeArray(treeArray));
                        }
                    }
                    else if ((piece == 3 && player == 1) || (piece == 4 && player == 2))
                    {
                        if (CheckMove.CheckMoveUpLeft(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i - 1,
                                NextWidth = j - 1,
                                Takes = new List<Piece>()
                            });
                        }
                        if (CheckMove.CheckMoveUpRight(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i - 1,
                                NextWidth = j + 1,
                                Takes = new List<Piece>()
                            });
                        }
                        if (CheckMove.CheckMoveDownLeft(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i + 1,
                                NextWidth = j - 1,
                                Takes = new List<Piece>()
                            });
                        }
                        if (CheckMove.CheckMoveDownRight(board, i, j))
                        {
                            results.Add(new NextMove
                            {
                                CurrentHeight = i,
                                CurrentWidth = j,
                                NextHeight = i + 1,
                                NextWidth = j + 1,
                                Takes = new List<Piece>()
                            });
                        }

                        int[] countersToTake = piece == 3 ? new int[] { 2, 4 } : new int[] { 1, 3 };
                        KingTree resultTree = CheckMove.CheckKingTake(board, i, j, i, j, countersToTake, new KingTree(new TreeTake
                        {
                            CurrentHeight = i,
                            CurrentWidth = j,
                        }));
                        if (resultTree.DownLeft != null || resultTree.DownRight != null || resultTree.UpLeft != null || resultTree.UpRight != null)
                        {
                            List<List<TreeTake>> treeArray = KingTree.KingTreeToArray(resultTree);
                            results.AddRange(ProcessTreeArray(treeArray));
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
                    NextHeight = take.NextHeight,
                    NextWidth = take.NextWidth,
                    Takes = takes,
                });
            }

            return results;
        }
    }
}
