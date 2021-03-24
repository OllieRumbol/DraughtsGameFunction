using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class KingTree
    {
        public TreeTake Value;

        public KingTree DownLeft;

        public KingTree DownRight;

        public KingTree UpLeft;

        public KingTree UpRight;

        public KingTree(TreeTake value)
        {
            Value = value;
            UpLeft = null;
            UpRight = null;
            DownLeft = null;
            DownRight = null;
        }

        public static List<List<TreeTake>> KingTreeToArray(KingTree mainTree)
        {
            List<List<TreeTake>> result = new List<List<TreeTake>>();
            iter(mainTree, new List<TreeTake>());

            void iter(KingTree tree, List<TreeTake> list)
            {
                if (tree.DownLeft != null && tree.DownRight != null && tree.UpLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.DownRight, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.DownRight != null && tree.UpLeft != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.DownRight, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.DownRight != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.DownRight, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.UpLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownRight != null && tree.UpLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownRight, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.DownRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.DownRight, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.UpLeft != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownRight != null && tree.UpLeft != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownRight, new List<TreeTake>(list));
                    iter(tree.UpLeft, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.UpLeft != null && tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.UpLeft, new List<TreeTake>(list));
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else if (tree.DownLeft != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownLeft, new List<TreeTake>(list));
                }
                else if (tree.DownRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.DownRight, new List<TreeTake>(list));
                }
                else if (tree.UpLeft != null)
                {
                    list.Add(tree.Value);
                    iter(tree.UpLeft, new List<TreeTake>(list));
                }
                else if (tree.UpRight != null)
                {
                    list.Add(tree.Value);
                    iter(tree.UpRight, new List<TreeTake>(list));
                }
                else
                {
                    list.Add(tree.Value);
                    result.Add(list);
                }
            }
            return result;
        }
     }
}
