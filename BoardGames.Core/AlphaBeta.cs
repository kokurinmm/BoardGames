using System;
using System.Collections.Generic;
using System.Text;

namespace BoardGames;

/// <summary>
/// Алгоритм альфа-бета отсечения в общем виде (можно использовать для разных игр)
/// </summary>
public static class AlphaBeta
{
    /// <summary>
    /// Основная функция для оценки позиции и поиска лучшего хода
    /// Использует функции для конкретных игр, передаваемые в качестве аргументов
    /// Игроки кодируются целым числом (обычно +1 - кто ходит первым, -1 - кто ходит вторым)
    /// </summary>
    public static (double score, TMove? bestMove) Search<TPos, TMove>(
        TPos position, // позиция на доске
        Func<TPos, int, List<TMove>> legalMoves, // функция, выводящая список ходов в данной позиции у данного игрока 
        Func<TPos, TMove, int, TPos> applyMoveToCopy, // функция для применения хода к копии доски
        Func<TPos, int, int, List<TMove>?, double> evaluate, // базовая функция для оценки позиции
        Func<int, int> opponent, // функция для нахождения кода противоположной стороны (обычно меняет знак у +1 или -1)
        Func<TPos, int, bool> isTerminal, // конец игры, если ход данного игрока (наличие ходов здесь можно не проверять)
        bool canPass, // допускаются ли правилами игры пропуски хода, когда нет допустимых ходов
        int rootPlayer, // игрок, с точки зрения которого ведётся оценка позиции
        int depth, // глубина поиска
        double alpha,
        double beta,
        bool maximizingPlayer, // если True, то текущий ход принадлежит root_player
        Func<TPos, int, List<TMove>, List<TMove>?>? forcingMoves = null) // вынужденные ходы (проверяются до конца) или null
        where TMove : class
    {
        int sideToMove = maximizingPlayer ? rootPlayer : opponent(rootPlayer);
        List<TMove> moves = legalMoves(position, sideToMove);

        if (isTerminal(position, sideToMove) || (!canPass && moves.Count == 0))
        {
            double score = evaluate(position, rootPlayer, sideToMove, moves);
            if (score > 0)
                score += depth; // если выигрыш, то лучше поскорее
            else if (score<0)
                score -= depth; // если поражение, то лучше не сразу
            return (score, null);
        }

        if (depth == 0)
        {
            List<TMove>? forcings = forcingMoves?.Invoke(position, sideToMove, moves);

            if (forcings is null || forcings.Count == 0) // если нет вынужденных ходов (взятий), обязательных для проверки
                return (evaluate(position, rootPlayer, sideToMove, moves), null); // то оцениваем позицию

            moves = forcings; // если же вынужденные ходы есть, продолжаем углубляться, пока они не закончатся
        }

        if (moves.Count == 0 && canPass)
        {
            var (scoreAfterPass, _) = Search(
                position,
                legalMoves,
                applyMoveToCopy,
                evaluate,
                opponent,
                isTerminal,
                canPass,
                rootPlayer,
                depth > 0 ? depth - 1 : 0, // depth=0 при просчёте вынужденных ходов, оставляем 0, иначе уменьшаем на 1
                alpha,
                beta,
                !maximizingPlayer,
                forcingMoves);

            return (scoreAfterPass, null);
        }

        TMove? bestMove = null;

        if (maximizingPlayer)
        {
            double bestValue = double.NegativeInfinity;

            foreach (TMove move in moves)
            {
                TPos child = applyMoveToCopy(position, move, sideToMove);

                var (childValue, _) = Search(
                    child,
                    legalMoves,
                    applyMoveToCopy,
                    evaluate,
                    opponent,
                    isTerminal,
                    canPass,
                    rootPlayer,
                    depth > 0 ? depth - 1 : 0,
                    alpha,
                    beta,
                    false,
                    forcingMoves);

                if (bestMove is null || childValue > bestValue)
                {
                    bestValue = childValue;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, childValue);
                if (beta <= alpha)
                    break;
            }

            return (bestValue, bestMove);
        }
        else
        {
            double bestValue = double.PositiveInfinity;

            foreach (TMove move in moves)
            {
                TPos child = applyMoveToCopy(position, move, sideToMove);

                var (childValue, _) = Search(
                    child,
                    legalMoves,
                    applyMoveToCopy,
                    evaluate,
                    opponent,
                    isTerminal,
                    canPass,
                    rootPlayer,
                    depth > 0 ? depth - 1 : 0,
                    alpha,
                    beta,
                    true,
                    forcingMoves);

                if (bestMove is null || childValue < bestValue)
                {
                    bestValue = childValue;
                    bestMove = move;
                }

                beta = Math.Min(beta, childValue);
                if (beta <= alpha)
                    break;
            }

            return (bestValue, bestMove);
        }
    }
}
