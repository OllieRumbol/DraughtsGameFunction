using DraughtsGameFunctionModels.Service;
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
        }

        public static List<List<TreeTake>> TreeToArray(Tree mainTree)
        {
            List<List<TreeTake>> result = new List<List<TreeTake>>();
            iter(mainTree, new List<TreeTake>());

            void iter(Tree tree, List<TreeTake> list)
            {
                list.Add(tree.Value);

                if (tree.Left != null && tree.Right != null)
                {
                    iter(tree.Left, new List<TreeTake>(list));
                    iter(tree.Right, new List<TreeTake>(list));
                }
                else if (tree.Left != null)
                {
                    iter(tree.Left, new List<TreeTake>(list));
                }
                else if (tree.Right != null)
                {
                    iter(tree.Right, new List<TreeTake>(list));
                }
                else
                {
                    result.Add(list);
                }
            }

            return result;
        }
    }
}