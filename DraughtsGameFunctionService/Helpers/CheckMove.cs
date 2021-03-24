using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class CheckMove
    {
        public static bool CheckMoveUpLeft(int[,] board, int height, int width)
        {
            try
            {
                if (board[height - 1, width - 1] == 5 || board[height - 1, width - 1] == 6)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool CheckMoveUpRight(int[,] board, int height, int width)
        {
            try
            {
                if (board[height - 1, width + 1] == 5 || board[height - 1, width + 1] == 6)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool CheckMoveDownLeft(int[,] board, int height, int width)
        {
            try
            {
                if (board[height + 1, width - 1] == 5 || board[height + 1, width - 1] == 6)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static bool CheckMoveDownRight(int[,] board, int height, int width)
        {
            try
            {
                if (board[height + 1, width + 1] == 5 || board[height + 1, width + 1] == 6)
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public static Tree CheckTakeDown(int[,] board, int i, int j, int[] playerToTake, Tree tree)
        {
            try
            {
                //left
                if (Array.IndexOf(playerToTake, board[i + 1, j - 1]) > -1)
                {
                    if (board[i + 2, j - 2] == 5 || board[i + 2, j - 2] == 6)
                    {
                        tree.Left = new Tree(new TreeTake
                        {
                            TakeHeight = i + 1,
                            TakeWidth = j - 1,
                            NextHeight = i + 2,
                            NextWidth = j - 2
                        });
                        CheckTakeDown(board, i + 2, j - 2, playerToTake, tree.Left);
                    }
                }
            }
            catch { }

            try
            {
                //Right
                if (Array.IndexOf(playerToTake, board[i + 1, j + 1]) > -1)
                {
                    if (board[i + 2, j + 2] == 5 || board[i + 2, j + 2] == 6)
                    {
                        tree.Right = new Tree(new TreeTake
                        {
                            TakeHeight = i + 1,
                            TakeWidth = j + 1,
                            NextHeight = i + 2,
                            NextWidth = j + 2
                        });
                        CheckTakeDown(board, i + 2, j + 2, playerToTake, tree.Right);
                    }
                }
            }
            catch { }

            return tree;
        }

        public static Tree CheckTakeUp(int[,] board, int i, int j, int[] playerToTake, Tree tree)
        {
            try
            {
                //left
                if (Array.IndexOf(playerToTake, board[i - 1, j - 1]) > -1)
                {
                    if (board[i - 2, j - 2] == 5 || board[i - 2, j - 2] == 6)
                    {
                        tree.Left = new Tree(new TreeTake
                        {
                            TakeHeight = i - 1,
                            TakeWidth = j - 1,
                            NextHeight = i - 2,
                            NextWidth = j - 2
                        });
                        CheckTakeUp(board, i - 2, j - 2, playerToTake, tree.Left);
                    }
                }
            }
            catch { }

            try
            {
                //Right
                if (Array.IndexOf(playerToTake, board[i - 1, j + 1]) > -1)
                {
                    if (board[i - 2, j + 2] == 5 || board[i - 2, j + 2] == 6)
                    {
                        tree.Right = new Tree(new TreeTake
                        {
                            TakeHeight = i - 1,
                            TakeWidth = j + 1,
                            NextHeight = i - 2,
                            NextWidth = j + 2
                        });
                        CheckTakeUp(board, i - 2, j + 2, playerToTake, tree.Right);
                    }
                }
            }
            catch { }

            return tree;
        }

        public static KingTree CheckKingTake(int[,] board, int preHeight, int preWidth, int height, int width, int[] playerToTake, KingTree tree)
        {
            try
            {
                //Down left
                if (Array.IndexOf(playerToTake, board[height + 1, width - 1]) > -1)
                {
                    if (board[height + 2, width - 2] == 5 || board[height + 2, width - 2] == 6)
                    {
                        if (preHeight != height + 2 || preWidth != width - 2)
                        {
                            tree.DownLeft = new KingTree(new TreeTake
                            {
                                TakeHeight = height + 1,
                                TakeWidth = width - 1,
                                NextHeight = height + 2,
                                NextWidth = width - 2
                            });
                            CheckKingTake(board, height, width, height + 2, width - 2, playerToTake, tree.DownLeft);
                        }
                    }
                }
            }
            catch { }

            try
            {
                //Down Right
                if (Array.IndexOf(playerToTake, board[height + 1, width + 1]) > -1)
                {
                    if (board[height + 2, width + 2] == 5 || board[height + 2, width + 2] == 6)
                    {
                        if (preHeight != height + 2 || preWidth != width + 2)
                        {
                            tree.DownRight = new KingTree(new TreeTake
                            {
                                TakeHeight = height + 1,
                                TakeWidth = width + 1,
                                NextHeight = height + 2,
                                NextWidth = width + 2
                            });
                            CheckKingTake(board, height, width, height + 2, width + 2, playerToTake, tree.DownRight);
                        }
                    }
                }
            }
            catch { }

            try
            {
                //Up left
                if (Array.IndexOf(playerToTake, board[height - 1, width - 1]) > -1)
                {
                    if (board[height - 2, width - 2] == 5 || board[height - 2, width - 2] == 6)
                    {
                        if (preHeight != height - 2 || preWidth != width - 2)
                        {
                            tree.UpLeft = new KingTree(new TreeTake
                            {
                                TakeHeight = height - 1,
                                TakeWidth = width - 1,
                                NextHeight = height - 2,
                                NextWidth = width - 2
                            });
                            CheckKingTake(board, height, width, height - 2, width - 2, playerToTake, tree.UpLeft);
                        }
                    }
                }
            }
            catch { }

            try
            {
                //Up Right
                if (Array.IndexOf(playerToTake, board[height - 1, width + 1]) > -1)
                {
                    if (board[height - 2, width + 2] == 5 || board[height - 2, width + 2] == 6)
                    {
                        if (preHeight != height - 2 || preWidth != width + 2)
                        {
                            tree.UpRight = new KingTree(new TreeTake
                            {
                                TakeHeight = height - 1,
                                TakeWidth = width + 1,
                                NextHeight = height - 2,
                                NextWidth = width + 2
                            });
                            CheckKingTake(board, height, width, height - 2, width + 2, playerToTake, tree.UpRight);
                        }
                    }
                }
            }
            catch { }

            return tree;
        }
    }
}
