using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    public static class StarRating
    {
        public const int MaxStars = 3;

        /// <summary>
        /// 1 star: completed. 2 stars: within the designer's par. 3 stars: matches the proven shortest solution without a hint.
        /// Undo is never penalised; only the final move count matters.
        /// </summary>
        public static int Calculate(LevelDefinition level, int moves, bool hintUsed)
        {
            var stars = 1;
            if (moves <= level.ParMoves) stars = 2;
            if (level.OptimalMoves.HasValue && moves <= level.OptimalMoves.Value && !hintUsed) stars = 3;
            return stars;
        }
    }
}
