using System;
using System.Collections.Generic;
using System.Text;

namespace BoardGames;

/// <summary>
/// Простейший вариант метода Монте-Карло в общем виде (можно использовать для разных игр)
/// </summary>
public static class MonteCarlo
{
    /// <summary>
    /// Основная функция для поиска лучшего хода
    /// </summary>
    public static TMove? BestMove<TPos, TMove>(
        TPos position, // позиция на доске
        Func<TPos, int, List<TMove>> legalMoves, // функция, выводящая список ходов в данной позиции у данного игрока
        Func<TPos, TMove, TPos> applyMoveToCopy, // функция для применения хода к копии доски
        Func<TPos, int, Random, double> playoutScore, // функция для случайного доигрывания позиции, возвращает оценку
        int player, // игрок
        int simulations) // количество доигрываний для каждого возможного хода
        where TMove : class
    {
        Random rng = Random.Shared;

        List<TMove> moves = legalMoves(position, player);
        if (moves.Count == 0)
            return null;

        if (moves.Count == 1) // если ход ровно один, ничего считать не нужно
            return moves[0];

        TMove bestMove = moves[0];
        double bestScore = double.NegativeInfinity;

        foreach (TMove move in moves)
        {
            double totalScore = 0.0;

            for (int k = 0; k < simulations; k++)
            {
                TPos simulation = applyMoveToCopy(position, move);
                totalScore += playoutScore(simulation, player, rng);
            }

            double averageScore = totalScore / Math.Max(1, simulations);
            if (averageScore > bestScore)
            {
                bestScore = averageScore;
                bestMove = move;
            }
        }

        return bestMove;
    }
}