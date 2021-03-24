using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class Tree
    {
        public TreeTake Value;

        public Tree Left;

        public Tree Right;

        public Tree(TreeTake value)
        {
            Value = value;
            Left = null;
            Right = null;
        }

        public static List<List<TreeTake>> TreeToArray(Tree mainTree)
        {
            List<List<TreeTake>> result = new List<List<TreeTake>>();
            iter(mainTree, new List<TreeTake>());

            void iter(Tree tree, List<TreeTake> list)
            {
                if (tree.Left != null && tree.Right != null)
                {
                    list.Add(tree.Value);
                    iter(tree.Left, new List<TreeTake>(list));
                    iter(tree.Right, new List<TreeTake>(list));
                }
                else if (tree.Left != null)
                {
                    list.Add(tree.Value);
                    iter(tree.Left, new List<TreeTake>(list));
                }
                else if (tree.Right != null)
                {
                    list.Add(tree.Value);
                    iter(tree.Right, new List<TreeTake>(list));
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
