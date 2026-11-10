#nullable enable

using System;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The Hard evaluation, with the first values in plan section 3.5 (spec section 6.1.5, item 6). T21 tunes the values
/// with the self-play tool. Each piece scores:
/// <list type="bullet">
/// <item>100 for a man and 200 for a king. A king is worth more than in standard draughts, because men cannot capture
/// kings (spec section 2.1, item 4).</item>
/// <item>For a man, 3 for each row it has moved up from its own back row, or 6 while it stays on the back row and
/// guards it against the opponent's men becoming kings.</item>
/// <item>2 on one of the four centre squares: rows 3 and 4, columns 2 to 5.</item>
/// </list>
/// The player who is ahead in material also gets 2 for each piece missing from the starting 24, so an even trade
/// raises their score.
/// Hard also follows captures past the depth limit (plan section 3.7).
/// </summary>
public sealed class PositionalEvaluator : IEvaluator
{
    private const int ManValue = 100;
    private const int KingValue = 200;
    private const int AdvancementPerRow = 3;
    private const int BackRowGuard = 6;
    private const int CentreBonus = 2;
    private const int TradeBonusPerMissingPiece = 2;
    private const int StartingPieceCount = 24;

    public bool UsesQuiescence => true;

    public int Evaluate(int[] board, int player)
    {
        var material = 0;
        var position = 0;
        var pieceCount = 0;

        for (var square = 0; square < Board.SquareCount; square++)
        {
            var value = board[square];
            var sign = Board.BelongsTo(value, player) ? 1 : Board.BelongsTo(value, 3 - player) ? -1 : 0;
            if (sign == 0)
            {
                continue;
            }

            pieceCount++;
            material += sign * (Board.IsKing(value) ? KingValue : ManValue);
            position += sign * PositionScore(square, value);
        }

        return material + position + TradeBonus(material, pieceCount);
    }

    private static int PositionScore(int square, int value)
    {
        var score = IsCentre(square) ? CentreBonus : 0;

        if (Board.IsKing(value))
        {
            return score;
        }

        // A man's own back row is the row where the opponent's men become kings.
        var owner = Board.BelongsTo(value, 1) ? 1 : 2;
        var rowsAdvanced = Math.Abs(Board.Row(square) - Board.FarRow(3 - owner));

        return score + (rowsAdvanced == 0 ? BackRowGuard : rowsAdvanced * AdvancementPerRow);
    }

    private static bool IsCentre(int square) =>
        Board.Row(square) is 3 or 4 && Board.Column(square) is >= 2 and <= 5;

    // Positive when the player is ahead, negative when the opponent is ahead, and 0 when material is level.
    private static int TradeBonus(int material, int pieceCount)
    {
        var missingPieces = Math.Max(0, StartingPieceCount - pieceCount);

        return Math.Sign(material) * TradeBonusPerMissingPiece * missingPieces;
    }
}
