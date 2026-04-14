using System;
using System.Collections.Generic;
using System.Text;

namespace BoardGames;

/// <summary>
/// Позиция в игре в шашки и правила игры
/// Класс не связан с формами и может использоваться в т.ч. в играх ИИ между собой
/// </summary>
public sealed class CheckersBoard
{
    public const int BOARD_SIZE = 8;

    // кодировка фигур
    public const int EMPTY = 0;
    public const int W_MAN = 1;
    public const int W_KING = 2;
    public const int B_MAN = -1;
    public const int B_KING = -2;

    // обозначения игроков
    public const int WHITE = 1;
    public const int BLACK = -1;

    /// <summary>
    /// Содержимое доски (первый индекс - номер строки, второй - номер столбца)
    /// </summary>
    public int[,] Grid { get; } = new int[BOARD_SIZE, BOARD_SIZE];

    public CheckersBoard()
    {
    }

    /// <summary>
    /// Противоположная сторона
    /// </summary>
    public static int Opponent(int player) => -player;

    /// <summary>
    /// Цвет фигуры по её числовому коду
    /// </summary>
    public static int PieceColor(int piece) => piece > 0 ? WHITE : piece < 0 ? BLACK : 0;

    /// <summary>
    /// Является ли данная фигура дамкой
    /// </summary>
    public static bool IsKing(int piece) => Math.Abs(piece) == 2;

    /// <summary>
    /// Превратить фигуру в дамку
    /// </summary>
    public static int MakeKing(int piece) => piece > 0 ? W_KING : piece < 0 ? B_KING : EMPTY;

    /// <summary>
    /// Принадлежит ли фигура игроку player
    /// </summary>
    public static bool IsPlayersPiece(int piece, int player) => piece != EMPTY && PieceColor(piece) == player;

    /// <summary>
    /// Создание стартовой позиции
    /// </summary>
    public static CheckersBoard Initial()
    {
        CheckersBoard board = new CheckersBoard();

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                if ((row + col) % 2 == 0) // игра идёт только по тёмным клеткам
                    continue;

                if (row < 3)
                    board.Grid[row, col] = B_MAN;
                else if (row > 4)
                    board.Grid[row, col] = W_MAN;
            }

        return board;
    }

    /// <summary>
    /// Копирование доски
    /// </summary>
    public CheckersBoard Copy()
    {
        CheckersBoard board = new CheckersBoard();
        Array.Copy(Grid, board.Grid, Grid.Length);
        return board;
    }

    /// <summary>
    /// Проверка допустимости координат (row, col)
    /// </summary>
    private static bool InBounds(int row, int col) =>
        row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE;

    /// <summary>
    /// Клетка доски
    /// </summary>
    public readonly record struct Square(int R, int C);

    /// <summary>
    /// Один ход (Captured хранит координаты побитой фигуры; если взятий нет, то Captured=null)
    /// </summary>
    public readonly record struct MoveStep(int R1, int C1, int R2, int C2, Square? Captured);

    /// <summary>
    /// Полный ход (или один простой ход без взятия, или цепочка взятий)
    /// </summary>
    public sealed class MoveChain
    {
        public List<MoveStep> Steps { get; }

        public MoveChain(IEnumerable<MoveStep> steps)
        {
            Steps = new List<MoveStep>(steps);
        }

        public int Length => Steps.Count;
    }

    /// <summary>
    /// Получить все допустимые ходы игрока (или только простые ходы, или только цепочки взятий)
    /// </summary>
    public List<MoveChain> AllMoves(int player)
    {
        List<MoveChain> allJumps = new(); // цепочки взятий
        List<MoveChain> allSlides = new(); // простые ходы

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                int piece = Grid[row, col];
                if (!IsPlayersPiece(piece, player))
                    continue;

                List<MoveChain> jumps = JumpSequencesFrom(row, col);
                if (jumps.Count > 0)
                {
                    allJumps.AddRange(jumps);
                }
                else if (allJumps.Count == 0)
                {
                    allSlides.AddRange(SlidesFrom(row, col));
                }
            }

        return allJumps.Count > 0 ? allJumps : allSlides;
    }

    /// <summary>
    /// Все простые ходы (без взятия) для фигуры на клетке (row, col)
    /// </summary>
    public List<MoveChain> SlidesFrom(int row, int col)
    {
        int piece = Grid[row, col];
        if (piece == EMPTY)
            return new List<MoveChain>();

        bool isKing = IsKing(piece);
        int moveDir = piece > 0 ? -1 : 1; // белые шашки идут вверх, чёрные вниз
        (int dr, int dc)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

        List<MoveChain> result = new();

        foreach ((int dr, int dc) in directions)
        {
            if (!isKing && dr != moveDir)
                continue;

            int nr = row + dr;
            int nc = col + dc;

            if (isKing) // возможные ходы дамки
            {
                while (InBounds(nr, nc) && Grid[nr, nc] == EMPTY)
                {
                    result.Add(new MoveChain(new[] { new MoveStep(row, col, nr, nc, null) }));
                    nr += dr;
                    nc += dc;
                }
            }
            else // возможные ходы обычной шашки
            {
                if (InBounds(nr, nc) && Grid[nr, nc] == EMPTY)
                    result.Add(new MoveChain(new[] { new MoveStep(row, col, nr, nc, null) }));
            }
        }

        return result;
    }

    /// <summary>
    /// Все цепочки взятий для фигуры на клетке (row, col)
    /// currentSequence хранит уже пройденную часть цепочки.
    /// </summary>
    public List<MoveChain> JumpSequencesFrom(int row, int col, List<MoveStep>? currentSequence = null)
    {
        currentSequence ??= new List<MoveStep>(); // работает если currentSequence = null

        int piece = Grid[row, col];
        if (piece == EMPTY)
            return new List<MoveChain>();

        int player = PieceColor(piece);
        int opponent = Opponent(player);
        bool isKing = IsKing(piece);

        (int dr, int dc)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };
        List<MoveChain> allChains = new();

        if (isKing) // возможные цепочки взятий для дамки
        {
            foreach ((int dr, int dc) in directions)
            {
                for (int dist = 1; dist < BOARD_SIZE; dist++)
                {
                    int nr = row + dr * dist;
                    int nc = col + dc * dist;
                    if (!InBounds(nr, nc))
                        break;

                    int cell = Grid[nr, nc];

                    if (IsPlayersPiece(cell, player))
                        break;

                    if (PieceColor(cell) == opponent)
                    {
                        int jr = nr + dr;
                        int jc = nc + dc;

                        if (InBounds(jr, jc) && Grid[jr, jc] == EMPTY)
                        {
                            CheckersBoard temp = Copy();
                            temp.Grid[jr, jc] = piece;
                            temp.Grid[row, col] = EMPTY;
                            temp.Grid[nr, nc] = EMPTY;

                            MoveStep step = new MoveStep(row, col, jr, jc, new Square(nr, nc));
                            List<MoveStep> nextSequence = new(currentSequence) { step };

                            List<MoveChain> sub = temp.JumpSequencesFrom(jr, jc, nextSequence);
                            if (sub.Count > 0)
                                allChains.AddRange(sub);
                            else
                                allChains.Add(new MoveChain(nextSequence));
                        }

                        break; // после первой вражеской фигуры дальше уже не идём
                    }

                    if (cell != EMPTY)
                        break;
                }
            }
        }
        else // возможные цепочки взятий обычной шашки
        {
            foreach ((int dr, int dc) in directions)
            {
                int nr = row + dr;
                int nc = col + dc;
                int jr = row + 2 * dr;
                int jc = col + 2 * dc;

                if (!InBounds(nr, nc) || !InBounds(jr, jc))
                    continue;

                if (PieceColor(Grid[nr, nc]) == opponent && Grid[jr, jc] == EMPTY)
                {
                    CheckersBoard temp = Copy();
                    int finalPiece = piece;

                    if ((player == WHITE && jr == 0) || (player == BLACK && jr == BOARD_SIZE - 1))
                        finalPiece = MakeKing(finalPiece); // превращение в дамку с возможным продолжением цепочки взятий

                    temp.Grid[jr, jc] = finalPiece;
                    temp.Grid[row, col] = EMPTY;
                    temp.Grid[nr, nc] = EMPTY;

                    MoveStep step = new MoveStep(row, col, jr, jc, new Square(nr, nc));
                    List<MoveStep> nextSequence = new(currentSequence) { step };

                    List<MoveChain> sub = temp.JumpSequencesFrom(jr, jc, nextSequence);
                    if (sub.Count > 0)
                        allChains.AddRange(sub);
                    else
                        allChains.Add(new MoveChain(nextSequence));
                }
            }
        }

        return allChains;
    }

    /// <summary>
    /// Выполнить на доске один ход (простой или одно взятие из цепочки)
    /// </summary>
    public void ApplyStep(MoveStep step)
    {
        int piece = Grid[step.R1, step.C1];
        Grid[step.R2, step.C2] = piece;
        Grid[step.R1, step.C1] = EMPTY;

        if (step.Captured is Square captured)
            Grid[captured.R, captured.C] = EMPTY;

        if (piece > 0 && step.R2 == 0)
            Grid[step.R2, step.C2] = W_KING;
        else if (piece < 0 && step.R2 == BOARD_SIZE - 1)
            Grid[step.R2, step.C2] = B_KING;
    }

    /// <summary>
    /// Выполнить на доске полный ход (простой или цепочку взятий)
    /// </summary>
    public void ApplyChain(MoveChain chain)
    {
        foreach (MoveStep step in chain.Steps)
            ApplyStep(step);
    }

    /// <summary>
    /// Простая функция оценки позиции с точки зрения игрока rootPlayer,
    /// если очередь хода принадлежит стороне sideToMove, с учётом продвижения шашек и мобильности
    /// </summary>
    public double Evaluate(int rootPlayer, int sideToMove, List<MoveChain>? moves = null, double M = 1_000_000)
    {
        moves ??= AllMoves(sideToMove);

        if (moves.Count == 0)
            return sideToMove == rootPlayer ? -M : +M; // проигрыш, если некуда ходить

        int opponent = Opponent(rootPlayer);

        double materialScore = 0.0;
        double advancementScore = 0.0;

        for (int row = 0; row < BOARD_SIZE; row++)
        {
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                int piece = Grid[row, col];
                if (piece == EMPTY)
                    continue;

                int color = PieceColor(piece);

                double value = IsKing(piece) ? 5.0 : 1.0; // дамка стоит 5 шашек
                if (color == rootPlayer)
                    materialScore += value;
                else
                    materialScore -= value;

                if (!IsKing(piece))
                {
                    // Оценка продвижения: белые идут вверх, чёрные вниз
                    double advancement =
                        color == WHITE
                            ? (BOARD_SIZE - 1 - row) * 0.10
                            : row * 0.10;

                    if (color == rootPlayer)
                        advancementScore += advancement;
                    else
                        advancementScore -= advancement;
                }
            }
        }

        // Оценка мобильности
        int CountUniqueFirstSteps(List<MoveChain> chains)
        {
            HashSet<(int r1, int c1, int r2, int c2)> uniqueSteps = new();

            foreach (MoveChain chain in chains)
            {
                if (chain.Steps.Count == 0)
                    continue;

                MoveStep first = chain.Steps[0];
                uniqueSteps.Add((first.R1, first.C1, first.R2, first.C2));
            }

            return uniqueSteps.Count;
        }

        int myMobility;
        int oppMobility;

        if (rootPlayer == sideToMove)
            myMobility = CountUniqueFirstSteps(moves);
        else
            myMobility = CountUniqueFirstSteps(AllMoves(rootPlayer));

        if (opponent == sideToMove)
            oppMobility = CountUniqueFirstSteps(moves);
        else
            oppMobility = CountUniqueFirstSteps(AllMoves(opponent));

        double mobilityScore = myMobility - oppMobility;

        return materialScore
             + advancementScore
             + 0.03 * mobilityScore;
    }

    /// <summary>
    /// Количество фигур данного цвета для заголовка формы, дамки считаются наравне с обычными шашками
    /// </summary>
    public int Count(int color)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (PieceColor(Grid[row, col]) == color)
                    total++;

        return total;
    }

    /// <summary>
    /// Текстовый ключ состояния позиции. Нужен MCTS для переиспользования дерева между ходами
    /// </summary>
    public string GetStateKey()
    {
        StringBuilder sb = new StringBuilder(BOARD_SIZE * BOARD_SIZE * 2);

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                sb.Append(Grid[row, col]).Append(',');

        return sb.ToString();
    }

}