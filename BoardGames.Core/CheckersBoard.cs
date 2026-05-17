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

    /// <summary>
    /// Количество сделанных подряд ходов только дамками без взятий. Как только достигает DRAW_NUM, объявляется ничья
    /// </summary>
    public int QuietMoves { get; private set; }

    public const int DRAW_NUM = 30; // количество тихих дамочных (полу)ходов для объявления ничьи

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
    /// Является ли этот ряд целевым для фигуры piece (для превращения в дамку)
    /// </summary>
    public static bool IsKingRow(int piece, int row) =>
        piece > 0 && row == 0 || piece < 0 && row == BOARD_SIZE - 1;

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
    /// Копирование доски и антиничейной информации
    /// </summary>
    public CheckersBoard Copy()
    {
        CheckersBoard board = new CheckersBoard();
        Array.Copy(Grid, board.Grid, Grid.Length);
        board.QuietMoves = QuietMoves;
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
    /// Для алгоритмов ИИ надо deduplicate = true: удалять цепочки взятий дамкой с дублирующимся результатом
    /// Для пользовательского хода надо deduplicate = false: все цепочки доступны
    /// </summary>
    public List<MoveChain> AllMoves(int player, bool deduplicate)
    {
        List<MoveChain> captures = CaptureMoves(player, deduplicate); // цепочки взятий

        if (captures.Count > 0)
            return captures;

        List<MoveChain> allSlides = new(); // простые ходы

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (IsPlayersPiece(Grid[row, col], player))
                    allSlides.AddRange(SlidesFrom(row, col));

        return allSlides;
    }

    /// <summary>
    /// Получить все цепочки взятий игрока player
    /// </summary>
    public List<MoveChain> CaptureMoves(int player, bool deduplicate)
    {
        List<MoveChain> allJumps = new();

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (IsPlayersPiece(Grid[row, col], player))
                    allJumps.AddRange(JumpSequencesFrom(row, col, deduplicate));

        return allJumps;
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
    /// Для алгоритмов ИИ надо deduplicate = true: удалять цепочки взятий дамкой с дублирующимся результатом
    /// Для пользовательского хода надо deduplicate = false: все цепочки доступны
    /// </summary>
    public List<MoveChain> JumpSequencesFrom(int row, int col, bool deduplicate)
    {
        int piece = Grid[row, col]; // фигура, выполняющая цепочку
        if (piece == EMPTY)
            return new List<MoveChain>();

        List<MoveStep> path = new List<MoveStep>(); // здесь будет строиться цепочка взятий
        List<MoveChain> result = new(); // сюда будут сохраняться завершённые цепочки
        ulong capturedMask = 0UL; // для хранения клеток с побитыми, но ещё не снятыми с доски фигурами (русские шашки)

        // здесь будут ключи финальных позиций цепочек для дедупликации, если она включена:
        HashSet<CaptureResultKey>? finalPositionKeys = deduplicate ? new HashSet<CaptureResultKey>() : null;

        CollectJumpSequences(row, col, piece, path, capturedMask, result, finalPositionKeys);

        return result;
    }

    /// <summary>
    /// Рекурсивный поиск продолжений уже построенных цепочек из текущей клетки (row,col)
    /// </summary>
    private void CollectJumpSequences(
        int row,
        int col,
        int piece,
        List<MoveStep> path,
        ulong capturedMask,
        List<MoveChain> result,
        HashSet<CaptureResultKey>? finalPositionKeys)
    {
        int player = PieceColor(piece);
        int opponent = Opponent(player);
        bool isKing = IsKing(piece);

        (int dr, int dc)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };
        bool foundContinuation = false;

        if (isKing) // возможные цепочки взятий для дамки
        {
            foreach ((int dr, int dc) in directions)
            {
                bool seenOpponent = false;
                Square captured = default;

                for (int dist = 1; dist < BOARD_SIZE; dist++)
                {
                    int nr = row + dr * dist;
                    int nc = col + dc * dist;

                    if (!InBounds(nr, nc))
                        break;

                    int cell = Grid[nr, nc];

                    if (cell == EMPTY)
                    {
                        if (!seenOpponent) // свободно движемся по диагонали
                            continue;

                        // если seenOpponent== true, значит перепрыгнули через фигуру противника (и она записана в captured)
                        foundContinuation = true;

                        MoveStep step = new MoveStep(row, col, nr, nc, captured);

                        int oldFrom = Grid[row, col]; // запоминаем состояние, чтобы потом вернуться к нему и продолжить поиск
                        int oldTo = Grid[nr, nc];

                        Grid[row, col] = EMPTY; // изменяем доску, обновляем маску, рекурсивно продолжаем построение цепочки
                        Grid[nr, nc] = piece;
                        ulong nextCapturedMask = capturedMask | SquareBit(captured);

                        path.Add(step);
                        CollectJumpSequences(nr, nc, piece, path, nextCapturedMask, result, finalPositionKeys);

                        // откат изменений
                        path.RemoveAt(path.Count - 1);
                        Grid[nr, nc] = oldTo;
                        Grid[row, col] = oldFrom;

                        continue; // в русских шашках можно приземлиться не обязательно на ближайшем поле после побитой фигуры
                    }

                    if (IsPlayersPiece(cell, player)) // перепрыгивать свою фигуру нельзя
                        break;

                    if (IsPlayersPiece(cell, opponent))
                    {
                        Square candidate = new Square(nr, nc);

                        if (IsCaptured(capturedMask, candidate))
                            break; // уже побитая фигура остаётся препятствием и не может быть побита повторно

                        if (seenOpponent)
                            break;

                        seenOpponent = true;
                        captured = candidate;
                        continue;
                    }

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

                Square captured = new Square(nr, nc);
                if (IsCaptured(capturedMask, captured))
                    continue;

                if (PieceColor(Grid[nr, nc]) != opponent || Grid[jr, jc] != EMPTY)
                    continue;

                foundContinuation = true;

                int nextPiece = piece; // кем станет шашка piece после хода - останется шашкой или превратится в дамку
                if (IsKingRow(piece, jr))
                    nextPiece = MakeKing(piece);

                MoveStep step = new MoveStep(row, col, jr, jc, captured);

                int oldFrom = Grid[row, col];
                int oldTo = Grid[jr, jc];

                Grid[row, col] = EMPTY;
                Grid[jr, jc] = nextPiece;
                ulong nextCapturedMask = capturedMask | SquareBit(captured);

                path.Add(step);
                CollectJumpSequences(jr, jc, nextPiece, path, nextCapturedMask, result, finalPositionKeys);

                // откат изменений
                path.RemoveAt(path.Count - 1);
                Grid[jr, jc] = oldTo;
                Grid[row, col] = oldFrom;
            }
        }

        // Если продолжений нет, то текущий path - законченная цепочка взятий, надо добавить её в result
        // Если ход был дамкой (или шашкой, превратившейся в дамку) и включена дедупликация, то проверяем дублирование
        if (!foundContinuation && path.Count > 0)
        {
            if (finalPositionKeys is not null && IsKing(piece))
            {
                CaptureResultKey key = new CaptureResultKey(row,col,capturedMask);
                if (!finalPositionKeys.Add(key))
                    return;
            }

            result.Add(new MoveChain(path));
        }

    }

    /// <summary>
    /// Выполнить на доске один ход (простой или одно взятие из цепочки)
    /// Для анимации в русских шашках нужно removeCaptured=false: побитые фигуры не удаляются сразу с доски
    /// Для их удаления надо будет вызвать RemoveCapturedPieces после окончания цепочки
    /// </summary>
    public void ApplyStep(MoveStep step, bool removeCaptured)
    {
        int piece = Grid[step.R1, step.C1];
        Grid[step.R2, step.C2] = piece;
        Grid[step.R1, step.C1] = EMPTY;

        if (step.Captured is Square captured && removeCaptured)
            Grid[captured.R, captured.C] = EMPTY;

        if (IsKingRow(piece, step.R2))
            Grid[step.R2, step.C2] = MakeKing(piece);
    }

    /// <summary>
    /// Удалить с доски все побитые фигуры. Можно передавать цепочку chain или набор шагов chain.Steps
    /// </summary>
    public void RemoveCapturedPieces(IEnumerable<MoveStep> steps)
    {
        foreach (MoveStep step in steps)
        {
            if (step.Captured is not Square captured)
                continue;

            Grid[captured.R, captured.C] = EMPTY;
        }
    }

    /// <summary>
    /// Удалить с доски все побитые фигуры. Можно передавать цепочку chain или набор шагов chain.Steps
    /// </summary>
    public void RemoveCapturedPieces(MoveChain chain) => RemoveCapturedPieces(chain.Steps);

    /// <summary>
    /// Выполнить на доске полный ход (простой или цепочку взятий)
    /// </summary>
    public void ApplyChain(MoveChain chain)
    {
        if (chain.Steps.Count == 0)
            return;

        MoveStep first = chain.Steps[0];
        int movingPiece = Grid[first.R1, first.C1];

        foreach (MoveStep step in chain.Steps)
            ApplyStep(step, removeCaptured: false);

        RemoveCapturedPieces(chain);
        UpdateQuietCount(first, movingPiece);
    }

    /// <summary>
    /// Проверка наличия доступных ходов, без формирования их полного списка
    /// </summary>
    public bool HasAnyMoves(int player)
    {
        for (int row = 0; row < BOARD_SIZE; row++)
        {
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                int piece = Grid[row, col];

                if (!IsPlayersPiece(piece, player))
                    continue;

                if (HasAnyJumpFrom(row, col)) // есть ли взятия
                    return true;

                if (HasAnySlideFrom(row, col)) // есть ли простые ходы
                    return true;
            }
        }

        return false;
    }

    private bool HasAnySlideFrom(int row, int col) // аналогична SlidesFrom, только без построения списка ходов
    {
        int piece = Grid[row, col];
        if (piece == EMPTY)
            return false;

        bool isKing = IsKing(piece);
        int moveDir = piece > 0 ? -1 : 1;
        (int dr, int dc)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

        foreach ((int dr, int dc) in directions)
        {
            if (!isKing && dr != moveDir)
                continue;

            int nr = row + dr;
            int nc = col + dc;

            if (InBounds(nr, nc) && Grid[nr, nc] == EMPTY)
                return true;
        }

        return false;
    }

    private bool HasAnyJumpFrom(int row, int col) // аналогична CollectJumpSequences, только без построения списка цепочек
    {
        int piece = Grid[row, col];
        if (piece == EMPTY)
            return false;

        int player = PieceColor(piece);
        int opponent = Opponent(player);

        (int dr, int dc)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

        if (IsKing(piece))
        {
            foreach ((int dr, int dc) in directions)
            {
                bool seenOpponent = false;

                for (int dist = 1; dist < BOARD_SIZE; dist++)
                {
                    int nr = row + dr * dist;
                    int nc = col + dc * dist;

                    if (!InBounds(nr, nc))
                        break;

                    int cell = Grid[nr, nc];

                    if (cell == EMPTY)
                    {
                        if (seenOpponent)
                            return true;

                        continue;
                    }

                    if (IsPlayersPiece(cell, player))
                        break;

                    if (IsPlayersPiece(cell, opponent))
                    {
                        if (seenOpponent)
                            break;

                        seenOpponent = true;
                        continue;
                    }

                    break;
                }
            }

            return false;
        }

        foreach ((int dr, int dc) in directions)
        {
            int nr = row + dr;
            int nc = col + dc;
            int jr = row + 2 * dr;
            int jc = col + 2 * dc;

            if (!InBounds(nr, nc) || !InBounds(jr, jc))
                continue;

            if (IsPlayersPiece(Grid[nr, nc], opponent) && Grid[jr, jc] == EMPTY)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Простая функция оценки позиции с точки зрения игрока rootPlayer,
    /// если очередь хода принадлежит стороне sideToMove, с учётом продвижения шашек и мобильности
    /// </summary>
    public double Evaluate(int rootPlayer, int sideToMove, List<MoveChain>? moves = null, double M = 1_000_000)
    {
        if (QuietMoves >= DRAW_NUM)
            return 0.0; // ничья по правилу 15 ходов

        bool hasMoves = moves is not null ? moves.Count > 0 : HasAnyMoves(sideToMove);

        if (!hasMoves)
            return sideToMove == rootPlayer ? -M : +M; // проигрыш, если некуда ходить

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
                    int distanceToKingRow = color == WHITE ? row : BOARD_SIZE - 1 - row;
                    double advancement = (BOARD_SIZE - 1 - distanceToKingRow) * 0.1;

                    // Если шашка в шаге от превращения в дамку
                    if (distanceToKingRow == 1)
                    {
                        int kingRow = color == WHITE ? 0 : BOARD_SIZE - 1;
                        bool canPromote =
                            (InBounds(kingRow, col - 1) && Grid[kingRow, col - 1] == EMPTY) ||
                            (InBounds(kingRow, col + 1) && Grid[kingRow, col + 1] == EMPTY);
                        if (canPromote)
                            advancement += 1.6;
                    }

                    if (color == rootPlayer)
                        advancementScore += advancement;
                    else
                        advancementScore -= advancement;

                }
            }
        }
        return materialScore + advancementScore;
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
    /// Обновить счётчик тихих ходов, movingPiece - тип фишки до начала хода
    /// </summary>
    public void UpdateQuietCount(MoveStep step, int movingPiece)
    {
        if (IsKing(movingPiece) && step.Captured is null)
            QuietMoves++;
        else
            QuietMoves = 0;
    }

    /// <summary>
    /// Текстовый ключ состояния позиции. Нужен MCTS для переиспользования дерева между ходами
    /// </summary>
    public string GetStateKey()
    {
        StringBuilder sb = new StringBuilder(BOARD_SIZE * BOARD_SIZE + 4);

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                sb.Append(CellCode(Grid[row, col]));

        sb.Append('|').Append(QuietMoves);

        return sb.ToString();
    }

    private static char CellCode(int piece) => piece switch
    {
        EMPTY => '.',
        W_MAN => 'w',
        W_KING => 'W',
        B_MAN => 'b',
        B_KING => 'B',
        _ => '?'
    };

    /// <summary>
    /// Для хранения результата цепочки взятий конкретной дамкой на конкретном ходу. 
    /// Хранит координаты финальной клетки и битовую маску побитых клеток. 
    /// Чтобы в алгоритмах ИИ удалять дублирующиеся ходы, приводящие к одному результату (актуально для русских шашек)
    /// </summary>
    private readonly record struct CaptureResultKey(int Row, int Col, ulong CapturedMask);

    /// <summary>
    /// Битовая маска с одним установленным битом, соответствующим заданной клетке доски
    /// </summary>
    private static ulong SquareBit(Square sq) => 1UL << (sq.R * BOARD_SIZE + sq.C);

    /// <summary>
    /// Установлен ли в маске бит, соответствующий клетке sq, т.е. побита ли клетка согласно этой маске
    /// </summary>
    private static bool IsCaptured(ulong mask, Square sq) => (mask & SquareBit(sq)) != 0;

}