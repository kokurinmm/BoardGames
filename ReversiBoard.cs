using System;
using System.Collections.Generic;
using System.Text;

namespace BoardGames;

/// <summary>
/// Позиция в игре в шашки и правила игры
/// Класс не связан с формами и может использоваться в т.ч. в играх ИИ между собой
/// </summary>
public sealed class ReversiBoard
{
    public const int BOARD_SIZE = 8;

    // кодировка цвета фишек. Чёрные в реверси ходят первыми, поэтому у них +1
    public const int EMPTY = 0;
    public const int BLACK = 1;
    public const int WHITE = -1;

    // восемь направлений
    private static readonly (int dx, int dy)[] DIRECTIONS =
    {
        (-1, 0), (-1, 1), (0, 1), (1, 1),
        (1, 0), (1, -1), (0, -1), (-1, -1)
    };

    /// <summary>
    /// Содержимое доски (первый индекс — номер строки, второй — номер столбца)
    /// </summary>
    public int[,] Grid { get; } = new int[BOARD_SIZE, BOARD_SIZE];

    public ReversiBoard()
    {
        // Стартовая позиция в реверси
        Grid[3, 3] = WHITE;
        Grid[3, 4] = BLACK;
        Grid[4, 3] = BLACK;
        Grid[4, 4] = WHITE;
    }

    /// <summary>
    /// Противоположная сторона
    /// </summary>
    public static int Opponent(int player) => -player;

    /// <summary>
    /// Копирование доски
    /// </summary>
    public ReversiBoard Copy()
    {
        ReversiBoard board = new ReversiBoard();
        Array.Copy(Grid, board.Grid, Grid.Length);
        return board;
    }

    /// <summary>
    /// Проверка допустимости координат (row, col)
    /// </summary>
    public bool InBounds(int row, int col) =>
        0 <= row && row < BOARD_SIZE && 0 <= col && col < BOARD_SIZE;

    /// <summary>
    /// Чья фишка стоит (если стоит) на заданном поле
    /// </summary>
    public int GetPiece(int row, int col) => Grid[row, col];

    /// <summary>
    /// Поместить фишку данного цвета на заданное поле
    /// </summary>
    public void SetPiece(int row, int col, int color) => Grid[row, col] = color;

    /// <summary>
    /// Количество фишек данного цвета
    /// </summary>
    public int Count(int color)
    {
        int sum = 0;
        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == color)
                    sum++;
        return sum;
    }

    /// <summary>
    /// Структура для представления хода в реверси
    /// </summary>
    public readonly record struct Move(int X, int Y);

    /// <summary>
    /// Найти допустимые ходы данного игрока и какие фишки перевернутся при этих ходах
    /// </summary>
    public Dictionary<Move, List<(int x, int y)>> ValidMoves(int player)
    {
        Dictionary<Move, List<(int x, int y)>> moves = new();

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                if (Grid[row, col] != EMPTY)
                    continue;

                List<(int x, int y)> flips = FlipsForMove(row, col, player);
                if (flips.Count > 0)
                    moves[new Move(row, col)] = flips;
            }

        return moves;
    }

    /// <summary>
    /// Определить, какие фишки будут перевёрнуты, если игрок player сходит в клетку (row, col)
    /// </summary>
    private List<(int x, int y)> FlipsForMove(int row, int col, int player)
    {
        List<(int x, int y)> flips = new();

        if (Grid[row, col] != EMPTY)
            return flips;

        int opponent = Opponent(player);

        foreach ((int dx, int dy) in DIRECTIONS)
        {
            int nr = row + dx;
            int nc = col + dy;
            List<(int x, int y)> temp = new();

            while (InBounds(nr, nc) && Grid[nr, nc] == opponent)
            {
                temp.Add((nr, nc));
                nr += dx;
                nc += dy;
            }

            if (InBounds(nr, nc) && Grid[nr, nc] == player && temp.Count > 0)
                flips.AddRange(temp);
        }

        return flips;
    }

    /// <summary>
    /// Применить ход игрока player в клетку (row, col)
    /// </summary>
    public void ApplyMove(int row, int col, int player)
    {
        List<(int x, int y)> flips = FlipsForMove(row, col, player);
        Grid[row, col] = player;

        foreach ((int fx, int fy) in flips)
            Grid[fx, fy] = player;
    }

    // Есть ли у игрока доступные ходы
    public bool HasAnyMoves(int player) => ValidMoves(player).Count > 0;

    // Проверка окончания игры (когда ни у одного из игроков нет доступных ходов)
    public bool IsTerminal() => !HasAnyMoves(BLACK) && !HasAnyMoves(WHITE);

    // Матрица для оценки позиции (чем больше число, тем выгоднее занимать это поле)
    private static readonly int[,] POSITIONAL_MATRIX =
    {
        {100, -20, 10,  5,  5, 10, -20, 100},
        {-20, -50, -2, -2, -2, -2, -50, -20},
        {10,  -2, -1, -1, -1, -1,  -2,  10},
        {5,   -2, -1, -1, -1, -1,  -2,   5},
        {5,   -2, -1, -1, -1, -1,  -2,   5},
        {10,  -2, -1, -1, -1, -1,  -2,  10},
        {-20, -50, -2, -2, -2, -2, -50, -20},
        {100, -20, 10,  5,  5, 10, -20, 100},
    };

    private static readonly double[] W_POS = { 1.0, 1.0, 1.0 }; // насколько важны позиции на разных стадиях игры
    private static readonly double[] W_MOB = { 5.0, 4.0, 1.0 }; // насколько важна мобильность на разных стадиях игры
    private static readonly double[] W_PIECE = { 0.0, 1.0, 10.0 }; // насколько важно количество фишек на разных стадиях игры

    private int PhaseIndex() // стадия игры
    {
        int empties = Count(EMPTY);
        if (empties >= 50) return 0;
        if (empties >= 20) return 1;
        return 2;
    }

    /// <summary>
    /// Простая функция оценки позиции с точки зрения игрока player
    /// </summary>
    public double Evaluate(int player)
    {
        int phase = PhaseIndex();
        int opponent = Opponent(player);

        double pieceScore = Count(player) - Count(opponent);

        // Если игра закончена, её итог важнее всего остального
        if (IsTerminal())
            return 100.0 * pieceScore;

        double positionalScore = 0.0;
        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                int piece = Grid[row, col];
                if (piece == player)
                    positionalScore += POSITIONAL_MATRIX[row, col];
                else if (piece == opponent)
                    positionalScore -= POSITIONAL_MATRIX[row, col];
            }

        double mobilityScore = ValidMoves(player).Count - ValidMoves(opponent).Count;

        double total =
            W_POS[phase] * positionalScore +
            W_MOB[phase] * mobilityScore +
            W_PIECE[phase] * pieceScore;

        return total;
    }
}