#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The Normal evaluation: material only, a man 1 and a king 2 (spec section 6.1.5, item 5, and plan section 3.5).
/// </summary>
public sealed class MaterialEvaluator : IEvaluator
{
    private const int ManValue = 1;
    private const int KingValue = 2;

    public bool UsesQuiescence => false;

    public int Evaluate(int[] board, int player)
    {
        var score = 0;

        foreach (var value in board)
        {
            var pieceValue = Board.IsKing(value) ? KingValue : ManValue;

            if (Board.BelongsTo(value, player))
            {
                score += pieceValue;
            }
            else if (Board.BelongsTo(value, 3 - player))
            {
                score -= pieceValue;
            }
        }

        return score;
    }
}
