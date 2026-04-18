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
    public const int HOME_EXIT_LIMIT = 40; // за 40 ходов игроки обязаны вывести все фишки из своих домов

    private static readonly (int dr, int dc)[] DIRECTIONS =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1)
    }; // ходы только по горизонтали и вертикали

    /// <summary>
    /// Содержимое доски (первый индекс - номер строки, второй - номер столбца)
    /// </summary>
    public int[,] Grid { get; } = new int[BOARD_SIZE, BOARD_SIZE];

    public int WhiteMovesPlayed { get; private set; } // количество сделанных ходов белыми и чёрными, для дополнительных правил
    public int BlackMovesPlayed { get; private set; }

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
        player == WHITE ? DistanceToWhiteGoal[row, col] : DistanceToBlackGoal[row, col];

    /// <summary>
    /// Получить все допустимые ходы игрока, с учётом антиничейных ограничений
    /// </summary>
    public List<MoveChain> AllMoves(int player)
    {
        List<MoveChain> moves = new();

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
            {
                if (!IsPlayersPiece(Grid[row, col], player))
                    continue;

                moves.AddRange(SlidesFrom(row, col, player));
                moves.AddRange(JumpSequencesFrom(row, col, player));
            }

        return moves;
    }


    /// <summary>
    /// Список простых шагов из клетки (row,col) для игрока player, с учётом антиничейных ограничений
    /// </summary>
    public List<MoveChain> SlidesFrom(int row, int col, int player)
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

            if (!IsAllowed(player, row, col, nr, nc))
                continue;

            result.Add(new MoveChain(new[] { new MoveStep(row, col, nr, nc) }));
        }

        return result;
    }

    /// <summary>
    /// Найти все цепочки прыжков из клетки (row, col) для игрока player, с учётом антиничейных ограничений
    /// Если это продолжение цепочки currentSequence, начало указано в (origStartRow,origStartCol) - важно для ограничений
    /// Остановка после любого прыжка разрешена, нельзя повторно посещать visitedSquares
    /// Для каждой конечной клетки сохраняется только одна цепочка ходов
    /// </summary>
    public List<MoveChain> JumpSequencesFrom(
        int row,
        int col,
        int player,
        int? origStartRow = null,
        int? origStartCol = null,
        List<MoveStep>? currentSequence = null,
        HashSet<Square>? visitedSquares = null)
    {
        int piece = Grid[row, col];
        if (piece == EMPTY)
            return new List<MoveChain>();

        int startRow = origStartRow ?? row;
        int startCol = origStartCol ?? col;

        List<MoveStep> path =
        currentSequence is null
            ? new List<MoveStep>()
            : new List<MoveStep>(currentSequence);

        HashSet<Square> visited =
            visitedSquares is null
                ? new HashSet<Square> { new Square(row, col) }
                : new HashSet<Square>(visitedSquares);

        Dictionary<Square, MoveChain> squareChainDict = new();

        CollectJumpSequences(
            row,
            col,
            player,
            startRow,
            startCol,
            piece,
            path,
            visited,
            squareChainDict);

        return squareChainDict.Values.ToList();
    }

    private void CollectJumpSequences(
        int row,
        int col,
        int player,
        int startRow,
        int startCol,
        int piece,
        List<MoveStep> path,
        HashSet<Square> visited,
        Dictionary<Square, MoveChain> squareChainDict)
    { 
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

            if (visited.Contains(landing))
                continue;

            if (!IsAllowed(player, startRow, startCol, landRow, landCol))
                continue;

            MoveStep step = new MoveStep(row, col, landRow, landCol);

            // применяем шаг к текущему пути, сохраняем цепочку (с учётом того, что в ней важны лишь старт и финиш),
            // затем временно применяем шаг к доске и продолжаем рекурсивно

            path.Add(step);
            visited.Add(landing);
            SaveChain(squareChainDict, new MoveChain(path));

            Grid[row, col] = EMPTY;
            Grid[landRow, landCol] = piece;

            CollectJumpSequences(
                landRow,
                landCol,
                player,
                startRow,
                startCol,
                piece,
                path,
                visited,
                squareChainDict);

            // откат изменений
            Grid[landRow, landCol] = EMPTY;
            Grid[row, col] = piece;
            visited.Remove(landing);
            path.RemoveAt(path.Count - 1);

        }

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
    public void UpdateAfterFullMove(MoveChain chain, int player)
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

            if (!MirrorBroken && _lastWhiteMove is not null && !IsMirror(chain))
                MirrorBroken = true;
        }
    }

    /// <summary>
    /// Наступил ли конец игры (если белые уже заняли целевой дом, у чёрных есть ещё один ход на это)
    /// </summary>
    public bool IsTerminal()
    {
        return
            (WhiteMovesPlayed == BlackMovesPlayed && (HasBuiltGoalHouse(WHITE) || HasBuiltGoalHouse(BLACK))) ||
            GetDeadlineWinner() is not null ||
            BlackMovesPlayed >= BLACK_MOVE_LIMIT;
    }

    /// <summary>
    /// Выиграл ли игрок, полностью заняв дом соперника
    /// </summary>
    public bool HasBuiltGoalHouse(int player) => CountInGoalHome(player) == PIECES_PER_PLAYER;

    /// <summary>
    /// Сколько фишек игрока находится в доме соперника
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
    /// Сколько фишек игрока ещё осталось в его собственном доме
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

    public int Count(int player) => CountInGoalHome(player); // в уголках счёт - это сколько фишек у цели, а не сколько всего

    /// <summary>
    /// Кто победил
    /// </summary>
    public int? GetWinner()
    {
        if (WhiteMovesPlayed == BlackMovesPlayed) // проверка после хода чёрных
        {
            bool whiteGoal = HasBuiltGoalHouse(WHITE);
            bool blackGoal = HasBuiltGoalHouse(BLACK);

            if (whiteGoal && blackGoal)
                return null;

            if (whiteGoal)
                return WHITE;

            if (blackGoal)
                return BLACK;
        }
        int? DeadlineWinner = GetDeadlineWinner();
        if (DeadlineWinner is not null)
        {
            if (DeadlineWinner == EMPTY)
                return null;
            else
                return DeadlineWinner;
        }

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
    /// Нарушил ли игрок правило "за 40 ходов вывести все фишки из своего дома"
    /// </summary>
    public bool FailedHomeExitDeadline(int player)
    {
        int movesPlayed = player == WHITE ? WhiteMovesPlayed : BlackMovesPlayed;
        return movesPlayed >= HOME_EXIT_LIMIT && CountInOwnHome(player) > 0;
    }

    /// <summary>
    /// Игрок, победивший из-за того, что соперник не успел вывести фишки из своего дома за 40 ходов
    /// Если оба не успели, возвращается EMPTY - это ничья
    /// Если игра не закончилась из-за дедлайна по выводу фишек, возвращается null
    /// </summary>
    /// <returns></returns>
    public int? GetDeadlineWinner()
    {
        if (BlackMovesPlayed < HOME_EXIT_LIMIT)
            return null;

        bool whiteFailed = CountInOwnHome(WHITE) > 0;
        bool blackFailed = CountInOwnHome(BLACK) > 0;

        if (whiteFailed && blackFailed)
            return EMPTY;

        if (whiteFailed)
            return BLACK;

        if (blackFailed)
            return WHITE;

        return null;
    }

    /// <summary>
    /// Допустим ли ход для игрока player из (startRow, startCol) в (finishRow, finishCol)
    /// Если недопустим, то цепочка прыжков, начатая в (startRow, startCol), не может касаться (finishRow, finishCol)
    /// Учитываются ограничение на 11-й зеркальный ход чёрных и запрет возврата в свой дом после 40-го хода
    /// </summary>
    public bool IsAllowed(int player, int startRow, int startCol, int finishRow, int finishCol)
    {
        if (BlackMovesPlayed >= HOME_EXIT_LIMIT && IsOwnHome(finishRow, finishCol, player))
            return false;

        if (player == BLACK &&
            GetMirrorRestriction(out Square requiredStart, out Square forbiddenLanding) &&
            startRow == requiredStart.R && startCol == requiredStart.C &&
            finishRow == forbiddenLanding.R && finishCol == forbiddenLanding.C)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Сохраняет в словарь не более одной цепочки для каждой конечной клетки.
    /// Если уже есть цепочка, ведущая в ту же клетку, оставляем более короткую (незачем хранить длинные).
    /// Для уголков, в отличие от шашек, неважно, какими конкретно прыжками фишка попала в конечную позицию.
    /// </summary>
    private static void SaveChain(Dictionary<Square, MoveChain> map, MoveChain chain)
    {
        if (chain.Steps.Count == 0)
            return;

        MoveStep last = chain.Steps[^1];
        Square landing = new Square(last.R2, last.C2);

        if (!map.TryGetValue(landing, out MoveChain? existing) || chain.Steps.Count < existing.Steps.Count)
            map[landing] = chain;
    }

    /// <summary>
    /// Повторяет ли ход чёрных зеркально соответствующий ход белых
    /// (смотрим только на начало и конец хода, промежуточная цепочка неважна)
    /// </summary>
    private bool IsMirror(MoveChain blackMove)
    {
        if (_lastWhiteMove is null)
            return false;

        if (_lastWhiteMove.Steps.Count == 0 || blackMove.Steps.Count == 0)
            return false;

        MoveStep whiteFirst = _lastWhiteMove.Steps[0];
        MoveStep whiteLast = _lastWhiteMove.Steps[^1];

        MoveStep blackFirst = blackMove.Steps[0];
        MoveStep blackLast = blackMove.Steps[^1];

        Square mirroredWhiteStart = MirrorByCenter(new Square(whiteFirst.R1, whiteFirst.C1));
        Square mirroredWhiteEnd = MirrorByCenter(new Square(whiteLast.R2, whiteLast.C2));

        return
            blackFirst.R1 == mirroredWhiteStart.R &&
            blackFirst.C1 == mirroredWhiteStart.C &&
            blackLast.R2 == mirroredWhiteEnd.R &&
            blackLast.C2 == mirroredWhiteEnd.C;
    }

    /// <summary>
    /// Если должно сработать ограничение на 11-й зеркальный ход чёрных,
    /// возвращает запрещаемые начало и конец для хода чёрных
    /// </summary>
    public bool GetMirrorRestriction(out Square requiredStart, out Square forbiddenLanding)
    {
        requiredStart = default;
        forbiddenLanding = default;

        if (MirrorBroken)
            return false;

        if (BlackMovesPlayed != MIRROR_LIMIT)
            return false;

        if (_lastWhiteMove is null || _lastWhiteMove.Steps.Count == 0)
            return false;

        MoveStep whiteFirst = _lastWhiteMove.Steps[0];
        MoveStep whiteLast = _lastWhiteMove.Steps[^1];

        requiredStart = MirrorByCenter(new Square(whiteFirst.R1, whiteFirst.C1));
        forbiddenLanding = MirrorByCenter(new Square(whiteLast.R2, whiteLast.C2));

        return true;
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

        if (IsTerminal())
        {
            int? winner = GetWinner();

            if (winner is null)
                return 0.0;

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

        double phase = Math.Min(1.0, BlackMovesPlayed / 40.0);

        double wGoal = 8.0 + 24.0 * phase;
        double wOwnHome = 32.0;
        double wDistance = 8.0;
        double wLagging = 16.0;
        double wPositional = 1.0;

        double score = 0.0;

        score += wGoal * (myGoal - oppGoal);
        score += wOwnHome * (oppOwnHome - myOwnHome);
        score += wDistance * (oppDistance - myDistance);
        score += wLagging * (oppLagging - myLagging);
        score += wPositional * (myPositional - oppPositional);

        return score;
    }

    /// <summary>
    /// Сумма расстояний всех фишек игрока до цели
    /// </summary>
    public int SumDistanceToGoal(int player)
    {
        int total = 0;

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                if (Grid[row, col] == player)
                    total += DistanceToGoal(player, row, col);

        return total;
    }

    // Оценка хода, от -1 до 1, для MCTS
    public double NormalizedMoveDeltaDist(int player, MoveChain move)
    {
        if (move is null || move.Steps.Count == 0)
            return 0.0;

        MoveStep first = move.Steps[0];
        MoveStep last = move.Steps[^1];

        int before = DistanceToGoal(player, first.R1, first.C1);
        int after = DistanceToGoal(player, last.R2, last.C2);

        return Math.Clamp((before - after) / 10.0, -1.0, 1.0);
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
    /// Расстояние до цели для самой отстающей фишки
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

    /// <summary>
    /// Текстовый ключ состояния позиции. Нужен MCTS для переиспользования дерева между ходами
    /// Включает служебную информацию, необходимую для антиничейных правил
    /// </summary>
    public string GetStateKey()
    {
        StringBuilder sb = new StringBuilder(BOARD_SIZE * BOARD_SIZE + 128);

        for (int row = 0; row < BOARD_SIZE; row++)
            for (int col = 0; col < BOARD_SIZE; col++)
                sb.Append(CellCode(Grid[row, col]));

        sb.Append('|').Append(WhiteMovesPlayed)
          .Append('|').Append(BlackMovesPlayed)
          .Append('|').Append(MirrorBroken ? '1' : '0');

        if (_lastWhiteMove is null || MirrorBroken)
        {
            sb.Append('|').Append('0');
        }
        else
        {
            sb.Append('|').Append(_lastWhiteMove.Steps.Count);
            foreach (MoveStep step in _lastWhiteMove.Steps)
            {
                sb.Append(';')
                  .Append(step.R1).Append(',')
                  .Append(step.C1).Append(',')
                  .Append(step.R2).Append(',')
                  .Append(step.C2);
            }
        }

        return sb.ToString();
    }

    private static char CellCode(int piece) => piece switch
    {
        EMPTY => '.',
        WHITE => 'W',
        BLACK => 'B',
        _ => '?'
    };

}