using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace BoardGames;

/// <summary>
/// Позиция в игре в уголки и правила игры
/// Класс не связан с формами и может использоваться в т.ч. в играх ИИ между собой
/// </summary>
public sealed class CornersBoard
{
    public const int BOARD_SIZE = 8;
    public const int HOME_SIZE = 3; // размер квадратного дома
    public const int PIECES_PER_PLAYER = HOME_SIZE * HOME_SIZE;

    // Цвет фишек
    public const int EMPTY = 0;
    public const int WHITE = 1;
    public const int BLACK = -1;

    public const int BLACK_MOVE_LIMIT = 80; // на 80-м ходе чёрных партия принудительно завершается
    public const int MIRROR_LIMIT = 10; // чёрным разрешено копировать первые 10 ходов белых

    private static readonly (int dr, int dc)[] DIRECTIONS =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1)
    }; // ходы только по горизонтали

    /// <summary>
    /// Содержимое доски (первый индекс - номер строки, второй - номер столбца)
    /// </summary>
    public int[,] Grid { get; } = new int[BOARD_SIZE, BOARD_SIZE];

    public int WhiteMovesPlayed { get; private set; } // количество сделанных ходов белыми и чёрными, для дополнительных правил
    public int BlackMovesPlayed { get; private set; }

    public int MirrorCount { get; private set; } // сколько раз чёрные в точности повторили ход белых

    public bool MirrorBroken { get; private set; } // зеркальное поведение чёрных нарушено

    private MoveChain? _lastWhiteMove; // последний ход белых - для проверки зеркального повторения чёрными

    /// <summary>
    /// Клетка доски
    /// </summary>
    public readonly record struct Square(int R, int C);

    /// <summary>
    /// Один шаг внутри полного хода (простой ход или прыжок)
    /// </summary>
    public readonly record struct MoveStep(int R1, int C1, int R2, int C2)
    {
        public bool IsJump => Math.Abs(R2 - R1) + Math.Abs(C2 - C1) == 2;
    }

    /// <summary>
    /// Полный ход: простой ход или цепочка прыжков
    /// </summary>
    public sealed class MoveChain
    {
        public List<MoveStep> Steps { get; }

        public MoveChain(IEnumerable<MoveStep> steps)
        {
            Steps = new List<MoveStep>(steps);
        }

        public bool IsJumpChain => Steps.Count > 0 && Steps[0].IsJump;

        public MoveChain Clone() => new MoveChain(Steps);
    }

    private static readonly int[,] WhiteKeyPositionMatrix =
    {
        {  0,  0,  0,  0,  0,  0,  0,  0 }, // 8
        {  0,  0,  0,  0,  0,  0,  0,  0 }, // 7
        {  0, +1, +3, +3, +3,  0,  0,  0 }, // 6
        {  0, +1, +1, +3, +3, +3,  0,  0 }, // 5
        { -2, -2, +1, +1, +3, +3,  0,  0 }, // 4
        { -2, -2, -2, +1, +1, +3,  0,  0 }, // 3
        { -2, -2, -2, -2, +1, +1,  0,  0 }, // 2
        { -2, -2, -2, -2,  0,  0,  0,  0 }  // 1
    }; // позиционная матрица для белых (для оценки позиции); расстояние до цели оценивается отдельно

    private static readonly Square[] WhiteHomeSquares = BuildWhiteHomeSquares(); // клетки белого дома
    private static readonly Square[] BlackHomeSquares = BuildBlackHomeSquares(); // клетки чёрного дома

    private static readonly int[,] DistanceToWhiteGoal = BuildGoalDistanceTable(WHITE); // расстояния до цели белых
    private static readonly int[,] DistanceToBlackGoal = BuildGoalDistanceTable(BLACK); // расстояния до цели чёрных

    private static Square[] BuildWhiteHomeSquares()
    {
        List<Square> result = new();
        for (int row = BOARD_SIZE - HOME_SIZE; row < BOARD_SIZE; row++)
            for (int col = 0; col < HOME_SIZE; col++)
                result.Add(new Square(row, col));
        return result.ToArray();
    }

    private static Square[] BuildBlackHomeSquares()
    {
        List<Square> result = new();
        for (int row = 0; row < HOME_SIZE; row++)
            for (int col = BOARD_SIZE - HOME_SIZE; col < BOARD_SIZE; col++)
                result.Add(new Square(row, col));
        return result.ToArray();
    }

    private static int[,] BuildGoalDistanceTable(int player)
    {
        Square[] targetSquares = GoalSquares(player);
        int[,] table = new int[BOARD_SIZE, BOARD_SIZE];

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                table[row, col] = targetSquares.Min(target => ManhattanDistance(row, col, target.R, target.C));

        return table;
    }

    public CornersBoard()
    {
    }

    /// <summary>
    /// Создать стартовую позицию
    /// </summary>
    public static CornersBoard Initial()
    {
        CornersBoard board = new CornersBoard();

        foreach (Square sq in WhiteHomeSquares)
            board.Grid[sq.R, sq.C] = WHITE;

        foreach (Square sq in BlackHomeSquares)
            board.Grid[sq.R, sq.C] = BLACK;

        return board;
    }

    /// <summary>
    /// Скопировать позицию со всей информацией
    /// </summary>
    public CornersBoard Copy()
    {
        CornersBoard board = new CornersBoard();
        Array.Copy(Grid, board.Grid, Grid.Length);

        board.WhiteMovesPlayed = WhiteMovesPlayed;
        board.BlackMovesPlayed = BlackMovesPlayed;
        board.MirrorCount = MirrorCount;
        board.MirrorBroken = MirrorBroken;
        board._lastWhiteMove = _lastWhiteMove?.Clone();

        return board;
    }

    /// <summary>
    /// Противоположная сторона
    /// </summary>
    public static int Opponent(int player) => -player;

    /// <summary>
    /// Проверка допустимости координат (row, col)
    /// </summary>
    public static bool InBounds(int row, int col) =>
        row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE;

    /// <summary>
    /// Принадлежит ли фигура игроку player
    /// </summary>
    public static bool IsPlayersPiece(int piece, int player) => piece == player;

    /// <summary>
    /// Клетка, симметричная данной относительно центра
    /// </summary>
    public static Square MirrorByCenter(Square sq) => new Square(BOARD_SIZE - 1 - sq.R, BOARD_SIZE - 1 - sq.C);

    /// <summary>
    /// Принадлежит ли клетка дому белых
    /// </summary>
    public static bool IsWhiteHome(int row, int col) => row >= BOARD_SIZE - HOME_SIZE && col < HOME_SIZE;

    /// <summary>
    /// Принадлежит ли клетка дому чёрных
    /// </summary>
    public static bool IsBlackHome(int row, int col) => row < HOME_SIZE && col >= BOARD_SIZE - HOME_SIZE;

    /// <summary>
    /// Принадлежит ли клетка дому игрока player
    /// </summary>
    public static bool IsOwnHome(int row, int col, int player) => player == WHITE ? IsWhiteHome(row, col) : IsBlackHome(row, col);

    /// <summary>
    /// Принадлежит ли клетка цели игрока player
    /// </summary>
    public static bool IsGoalHome(int row, int col, int player) => player == WHITE ? IsBlackHome(row, col) : IsWhiteHome(row, col);

    /// <summary>
    /// Клетки цели игрока player
    /// </summary>
    public static Square[] GoalSquares(int player) => player == WHITE ? BlackHomeSquares : WhiteHomeSquares;

    /// <summary>
    /// Манхеттенское расстояние (по горизонтали плюс по вертикали)
    /// </summary>
    private static int ManhattanDistance(int r1, int c1, int r2, int c2) => Math.Abs(r1 - r2) + Math.Abs(c1 - c2);

    /// <summary>
    /// Расстояние от клетки до цели
    /// </summary>
    public int DistanceToGoal(int player, int row, int col) =>
        player == WHITE ? DistanceToBlackGoal[row, col] : DistanceToWhiteGoal[row, col];

    /// <summary>
    /// Получить все допустимые ходы игрока
    /// </summary>
    public List<MoveChain> AllMoves(int player)
    {
        List<MoveChain> moves = new();

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                if (!IsPlayersPiece(Grid[row, col], player))
                    continue;

                moves.AddRange(SlidesFrom(row, col));
                moves.AddRange(JumpSequencesFrom(row, col));
            }

        if (player == BLACK)
            ApplyMirrorRestriction(moves);

        return moves;
    }


    /// <summary>
    /// Список простых шагов из заданной клетки на соседнюю пустую клетку
    /// </summary>
    public List<MoveChain> SlidesFrom(int row, int col)
    {
        List<MoveChain> result = new();

        if (!InBounds(row, col) || Grid[row, col] == EMPTY)
            return result;

        foreach ((int dr, int dc) in DIRECTIONS)
        {
            int nr = row + dr;
            int nc = col + dc;
            if (!InBounds(nr, nc) || Grid[nr, nc] != EMPTY)
                continue;

            result.Add(new MoveChain(new[] { new MoveStep(row, col, nr, nc) }));
        }

        return result;
    }

    /// <summary>
    /// Найти все цепочки прыжков для шашки из клетки (row, col).
    /// Остановка после любого прыжка разрешена, прыжки не должны быть зацикленными
    /// </summary>
    public List<MoveChain> JumpSequencesFrom(
        int row,
        int col,
        List<MoveStep>? currentSequence = null,
        HashSet<Square>? visitedLandings = null)
    {
        currentSequence ??= new List<MoveStep>();
        visitedLandings ??= new HashSet<Square> { new Square(row, col) };

        List<MoveChain> allChains = new();

        foreach ((int dr, int dc) in DIRECTIONS)
        {
            int overRow = row + dr;
            int overCol = col + dc;
            int landRow = row + 2 * dr;
            int landCol = col + 2 * dc;

            if (!InBounds(overRow, overCol) || !InBounds(landRow, landCol))
                continue;

            if (Grid[overRow, overCol] == EMPTY || Grid[landRow, landCol] != EMPTY)
                continue;

            Square landing = new Square(landRow, landCol);
            if (visitedLandings.Contains(landing))
                continue; // зацикливание запрещено

            CornersBoard temp = Copy();
            int piece = temp.Grid[row, col];
            temp.Grid[row, col] = EMPTY;
            temp.Grid[landRow, landCol] = piece;

            MoveStep step = new MoveStep(row, col, landRow, landCol);
            List<MoveStep> nextSequence = new(currentSequence);
            nextSequence.Add(step);

            // Остановить цепочку прыжков можно в любой момент
            allChains.Add(new MoveChain(nextSequence));

            HashSet<Square> nextVisited = new(visitedLandings) { landing };
            allChains.AddRange(temp.JumpSequencesFrom(landRow, landCol, nextSequence, nextVisited));
        }

        return allChains;
    }

    /// <summary>
    /// Применить один шаг к позиции на доске
    /// </summary>
    public void ApplyStep(MoveStep step)
    {
        int piece = Grid[step.R1, step.C1];
        Grid[step.R1, step.C1] = EMPTY;
        Grid[step.R2, step.C2] = piece;
    }

    /// <summary>
    /// Применить полный ход и обновить информацию, нужную для дополнительных антиничейных правил
    /// </summary>
    public void ApplyChain(MoveChain chain, int player)
    {
        foreach (MoveStep step in chain.Steps)
            ApplyStep(step);

        UpdateAfterFullMove(chain, player);
    }

    /// <summary>
    /// Обновить информацию для антиничейных правил после полного хода
    /// </summary>
    private void UpdateAfterFullMove(MoveChain chain, int player)
    {
        if (player == WHITE)
        {
            WhiteMovesPlayed++;

            if (!MirrorBroken && WhiteMovesPlayed <= MIRROR_LIMIT + 1)
                _lastWhiteMove = chain.Clone();
        }
        else
        {
            BlackMovesPlayed++;

            if (!MirrorBroken && _lastWhiteMove is not null)
            {
                if (IsMirror(chain))
                    MirrorCount++;
                else
                    MirrorBroken = true;
            }
        }
    }

    /// <summary>
    /// Наступил ли конец игры
    /// </summary>
    public bool IsTerminal() =>
        HasBuiltGoalHouse(WHITE) ||
        HasBuiltGoalHouse(BLACK) ||
        BlackMovesPlayed >= BLACK_MOVE_LIMIT;

    /// <summary>
    /// Выиграл ли игрок, полностью заняв дом соперника
    /// </summary>
    public bool HasBuiltGoalHouse(int player) => CountInGoalHome(player) == PIECES_PER_PLAYER;

    /// <summary>
    /// Сколько шашек игрока находится в доме соперника
    /// </summary>
    public int CountInGoalHome(int player)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == player && IsGoalHome(row, col, player))
                    total++;

        return total;
    }

    /// <summary>
    /// Сколько шашек игрока ещё осталось в его собственном доме
    /// </summary>
    public int CountInOwnHome(int player)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == player && IsOwnHome(row, col, player))
                    total++;

        return total;
    }

    public int Count(int player) => PIECES_PER_PLAYER; // количество фишек у каждого игрока всегда одинаково

    /// <summary>
    /// Кто победил
    /// </summary>
    public int? GetWinner()
    {
        if (HasBuiltGoalHouse(WHITE))
            return WHITE;

        if (HasBuiltGoalHouse(BLACK))
            return BLACK;

        if (BlackMovesPlayed >= BLACK_MOVE_LIMIT)
        {
            int whiteInGoal = CountInGoalHome(WHITE);
            int blackInGoal = CountInGoalHome(BLACK);

            if (whiteInGoal > blackInGoal)
                return WHITE;
            if (blackInGoal > whiteInGoal)
                return BLACK;
            return null;
        }

        return null;
    }

    /// <summary>
    /// Повторяет ли ход чёрных зеркально соответствующий ход белых
    /// </summary>
    private bool IsMirror(MoveChain blackMove)
    {
        if (_lastWhiteMove is null)
            return false;

        if (blackMove.Steps.Count != _lastWhiteMove.Steps.Count)
            return false;

        for (int i = 0; i < _lastWhiteMove.Steps.Count; i++)
        {
            MoveStep w = _lastWhiteMove.Steps[i];
            MoveStep b = blackMove.Steps[i];

            Square wFrom = MirrorByCenter(new Square(w.R1, w.C1));
            Square wTo = MirrorByCenter(new Square(w.R2, w.C2));

            if (b.R1 != wFrom.R || b.C1 != wFrom.C || b.R2 != wTo.R || b.C2 != wTo.C)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Применить дебютное правило: 11-е зеркальное повторение хода белых недоступно для чёрных
    /// </summary>
    private void ApplyMirrorRestriction(List<MoveChain> blackMoves)
    {
        if (MirrorBroken)
            return;

        if (MirrorCount != MIRROR_LIMIT)
            return;

        if (BlackMovesPlayed != MIRROR_LIMIT)
            return;

        blackMoves.RemoveAll(IsMirror);
    }

    /// <summary>
    /// Насколько ход перспективен, для упорядочивания ходов для алгоритма альфа-бета отсечения
    /// </summary>
    public double MoveOrderingScore(MoveChain move, int player)
    {
        if (move.Steps.Count == 0)
            return double.NegativeInfinity;

        MoveStep first = move.Steps[0];
        MoveStep last = move.Steps[^1];

        int progress = DistanceToGoal(player, first.R1, first.C1) - DistanceToGoal(player, last.R2, last.C2);

        return progress;
    }

    /// <summary>
    /// Базовая функция оценки позиции с точки зрения игрока rootPlayer, если очередь хода принадлежит игроку sideToMove
    /// </summary>
    public double Evaluate(int rootPlayer, int sideToMove, List<MoveChain>? generatedMoves = null, double M = 1_000_000)
    {
        generatedMoves ??= AllMoves(sideToMove);

        if (HasBuiltGoalHouse(rootPlayer))
            return +M;

        if (HasBuiltGoalHouse(Opponent(rootPlayer)))
            return -M;

        if (BlackMovesPlayed >= BLACK_MOVE_LIMIT)
        {
            int whiteInGoal = CountInGoalHome(WHITE);
            int blackInGoal = CountInGoalHome(BLACK);

            if (whiteInGoal == blackInGoal)
                return 0.0;

            int winner = whiteInGoal > blackInGoal ? WHITE : BLACK;
            return winner == rootPlayer ? +M : -M;
        }

        // Если некуда ходить - проигрыш (вряд ли понадобится)
        if (generatedMoves.Count == 0)
            return sideToMove == rootPlayer ? -M : +M;

        int opponent = Opponent(rootPlayer);

        int myGoal = CountInGoalHome(rootPlayer);
        int oppGoal = CountInGoalHome(opponent);

        int myOwnHome = CountInOwnHome(rootPlayer);
        int oppOwnHome = CountInOwnHome(opponent);

        int myDistance = SumDistanceToGoal(rootPlayer);
        int oppDistance = SumDistanceToGoal(opponent);

        int myLagging = FarthestPieceDistanceToGoal(rootPlayer);
        int oppLagging = FarthestPieceDistanceToGoal(opponent);

        int myPositional = PositionalMatrixScore(rootPlayer);
        int oppPositional = PositionalMatrixScore(opponent);

        const double wGoal = 120.0;
        const double wOwnHome = 12.0;
        const double wDistance = 4.0;
        const double wLagging = 8.0;
        const double wPositional = 1.0;

        double score = 0.0;

        score += wGoal * (myGoal - oppGoal);
        score += wOwnHome * (oppOwnHome - myOwnHome);
        score += wDistance * (oppDistance - myDistance);
        score += wLagging * (oppLagging - myLagging);
        score += wPositional * (myPositional - oppPositional);

        return score;
    }

    /// <summary>
    /// Сумма расстояний всех шашек игрока до цели
    /// </summary>
    private int SumDistanceToGoal(int player)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == player)
                    total += DistanceToGoal(player, row, col);

        return total;
    }

    /// <summary>
    /// Значение позиционной матрицы для данной клетки применительно к игроку player
    /// </summary>
    private static int PositionalCellValue(int player, int row, int col)
    {
        if (player == WHITE)
            return WhiteKeyPositionMatrix[row, col];

        return WhiteKeyPositionMatrix[BOARD_SIZE - 1 - row, BOARD_SIZE - 1 - col];
    }

    /// <summary>
    /// Суммарный позиционный бонус по карте ключевых позиций (достижение цели оценивается отдельно)
    /// </summary>
    private int PositionalMatrixScore(int player)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                if (Grid[row, col] != player)
                    continue;

                if (IsGoalHome(row, col, player))
                    continue;

                total += PositionalCellValue(player, row, col);
            }

        return total;
    }

    /// <summary>
    /// Расстояние до цели для самой отстающей шашки
    /// </summary>
    private int FarthestPieceDistanceToGoal(int player)
    {
        int farthest = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == player)
                    farthest = Math.Max(farthest, DistanceToGoal(player, row, col));

        return farthest;
    }

}