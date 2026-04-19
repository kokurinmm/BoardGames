using System;
using System.Collections.Generic;
using System.Text;

namespace BoardGames;

/// <summary>
/// Контроллер игры в реверси (отрисовка доски, обработка щелчков, вызов алгоритмов ИИ, начало и конец игры)
/// </summary>
public sealed class ReversiController : IGameController
{
    public GameKind Kind => GameKind.Reversi;
    public int BoardSize => ReversiBoard.BOARD_SIZE;

    public string GameDisplayName => "Реверси";

    public int WhitePieceCount => _board.Count(ReversiBoard.WHITE);

    public int BlackPieceCount => _board.Count(ReversiBoard.BLACK);

    public AiMode Mode { get; set; } = AiMode.AlphaBeta;
    public int AlphaBetaDepth { get; set; } = 4;
    public int MaxDepth { get; set; } = 7;
    public int MonteCarloSimulations { get; set; } = 60;
    public int MctsTimeLimitMs { get; set; } = 750;

    public bool IsGameOver { get; private set; }

    public string HumanPlayerDisplayName => Players.ReversiName(_humanColor);

    public string? GameOverMessage { get; private set; }

    public bool HumanVsHuman { get; set; }

    public string CurrentTurnDisplayName => Players.ReversiName(_turn);

    /// <summary>
    /// Текущая позиция на доске
    /// </summary>
    private ReversiBoard _board = new();

    private int _humanColor; // цвет пользователя
    private int _aiColor; // цвет ИИ
    private int _turn; // игрок, которому принадлежит очередь хода

    private (int row, int col)? _lastAiSquare; // клетка, куда сходил ИИ

    private ReversiBoard.Move? _pendingAiMove; // текущий ход ИИ
    private bool _hasPendingAiMove; // выполнен ли уже найденный ход

    private readonly MctsSession<ReversiBoard, MoveRef> _mcts; // сеанс MCTS

    public bool IsAiTurn => !HumanVsHuman && !IsGameOver && _turn == _aiColor;

    public ReversiController()
    {
        _mcts = new MctsSession<ReversiBoard, MoveRef>(
            legalMoves: (pos, side) => pos.ValidMoves(side).Keys.Select(m => new MoveRef(m.X, m.Y)).ToList(),
            applyMoveToCopy: (pos, move, side) =>
            {
                ReversiBoard child = pos.Copy();
                child.ApplyMove(move.X, move.Y, side);
                return child;
            },
            rolloutScore: ReversiMctsRolloutResult,
            opponent: ReversiBoard.Opponent,
            isTerminal: (pos, side) => pos.IsTerminal(),
            positionKey: pos => pos.GetStateKey(),
            canPass: true,
            explorationConstant: Math.Sqrt(2.0));
    }

    public void NewGame() // запуск новой игры, в т.ч. случайный выбор цвета игроков
    {
        IsGameOver = false;
        _board = new ReversiBoard();

        _turn = ReversiBoard.BLACK; // в реверси первый ход принадлежит чёрным

        GameOverMessage = null;

        _lastAiSquare = null;

        _pendingAiMove = null;
        _hasPendingAiMove = false;

        // Для игры с ИИ - случайный выбор цветов игроков
        _humanColor = Random.Shared.Next(2) == 0 ? ReversiBoard.BLACK : ReversiBoard.WHITE;
        _aiColor = ReversiBoard.Opponent(_humanColor);
        _mcts.Reset(); // перезапуск сеанса MCTS

    }

    public void Draw(Graphics g, Rectangle rect) // Отрисовка доски
    {
        int cell = rect.Width / BoardSize;

        using SolidBrush background = new SolidBrush(Color.DarkGreen);
        g.FillRectangle(background, rect);

        using Pen gridPen = new Pen(Color.Black, 2);
        for (int i = 0; i <= BoardSize; i++)
        {
            int x = rect.Left + i * cell;
            int y = rect.Top + i * cell;
            g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
        }

        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                int piece = _board.GetPiece(row, col);
                if (piece == ReversiBoard.EMPTY)
                    continue;

                float cx = rect.Left + col * cell + cell / 2.0f;
                float cy = rect.Top + row * cell + cell / 2.0f;
                float radius = cell * 0.42f;

                using SolidBrush brush = new SolidBrush(piece == ReversiBoard.BLACK ? Color.Black : Color.White);
                using Pen outline = new Pen(Color.Black, 1);

                g.FillEllipse(brush, cx - radius, cy - radius, 2 * radius, 2 * radius);
                if (piece == ReversiBoard.WHITE)
                    g.DrawEllipse(outline, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }

        // Подсветка допустимых ходов текущего игрока
        var legalMoves = _board.ValidMoves(_turn);
        foreach (ReversiBoard.Move move in legalMoves.Keys)
        {
            float cx = rect.Left + move.Y * cell + cell / 2.0f;
            float cy = rect.Top + move.X * cell + cell / 2.0f;
            float radius = cell * 0.12f;

            using SolidBrush dot = new SolidBrush(Color.Yellow);
            g.FillEllipse(dot, cx - radius, cy - radius, 2 * radius, 2 * radius);
        }

        // Подсветка последнего хода ИИ
        if (_lastAiSquare is (int aiRow, int aiCol))
        {
            float x1 = rect.Left + aiCol * cell + 1.5f;
            float y1 = rect.Top + aiRow * cell + 1.5f;
            float w = cell - 3.5f;
            float h = cell - 3.5f;

            using Pen aiMovePen = new Pen(Color.OrangeRed, 3);
            aiMovePen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
            g.DrawRectangle(aiMovePen, x1, y1, w, h);
        }

    }

    /// <summary>
    /// Обработчик щелчка по клетке доски
    /// </summary>
    public void HandleCellClick(int row, int col)
    {
        if (IsGameOver)
            return;

        if (!HumanVsHuman && _turn != _humanColor)
            return;

        _lastAiSquare = null; // сразу снимаем подсветку последнего хода ИИ

        var legalMoves = _board.ValidMoves(_turn);
        ReversiBoard.Move move = new ReversiBoard.Move(row, col);
        if (!legalMoves.ContainsKey(move))
            return;

        _board.ApplyMove(row, col, _turn);

        ResolveTurn();
    }

    public bool BeginAiTurnAnimation() // Подготовить ход ИИ (в реверси нет анимированных ходов из нескольких шагов)
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        var legalMoves = _board.ValidMoves(_turn);

        ReversiBoard.Move? bestMove = FindBestAiMove(legalMoves); // вызываем даже если ходов нет и это пас - для MCTS

        if (legalMoves.Count == 0)
        {
            ResolveTurn();
            return true;
        }

        if (bestMove is null)
            return false;

        _pendingAiMove = bestMove.Value;
        _hasPendingAiMove = true;
        return true;
    }

    public bool HasPendingAiAnimation => _hasPendingAiMove;

    public bool ApplyNextAiAnimationStep() // Отобразить на доске подготовленный ход ИИ
    {
        if (!_hasPendingAiMove || _pendingAiMove is null)
            return false;

        _lastAiSquare = (_pendingAiMove.Value.X, _pendingAiMove.Value.Y);
        _board.ApplyMove(_pendingAiMove.Value.X, _pendingAiMove.Value.Y, _turn);
        _pendingAiMove = null;
        _hasPendingAiMove = false;

        ResolveTurn();

        return true;
    }

    /// <summary>
    /// Найти лучший ход ИИ
    /// </summary>
    private ReversiBoard.Move? FindBestAiMove(
        Dictionary<ReversiBoard.Move, List<(int x, int y)>>? legalMoves = null)
    {
        legalMoves ??= _board.ValidMoves(_turn);

        if (legalMoves.Count == 0 && Mode != AiMode.Mcts)
            return null;

        ReversiBoard.Move? bestMove = null;

        if (Mode == AiMode.AlphaBeta)
        {
            (double score, MoveRef? moveRef) = AlphaBeta.Search(
                position: _board,
                legalMoves: (pos, side) => pos.ValidMoves(side).Keys.Select(m => new MoveRef(m.X, m.Y)).ToList(),
                applyMoveToCopy: (pos, move, side) =>
                {
                    ReversiBoard child = pos.Copy();
                    child.ApplyMove(move.X, move.Y, side);
                    return child;
                },
                evaluate: (pos, root, side, generatedMoves) => pos.Evaluate(root),
                opponent: ReversiBoard.Opponent,
                isTerminal: (pos, side) => pos.IsTerminal(),
                canPass: true,
                rootPlayer: _aiColor,
                depth: AlphaBetaDepth,
                alpha: double.NegativeInfinity,
                beta: double.PositiveInfinity,
                maximizingPlayer: true);

            if (moveRef is not null)
                bestMove = new ReversiBoard.Move(moveRef.X, moveRef.Y);
        }
        else if (Mode == AiMode.MonteCarlo)
        {
            MoveRef? bestMoveRef = MonteCarlo.BestMove(
                position: _board,
                legalMoves: (pos, side) => pos.ValidMoves(side).Keys.Select(m => new MoveRef(m.X, m.Y)).ToList(),
                applyMoveToCopy: (pos, move) =>
                {
                    ReversiBoard child = pos.Copy();
                    child.ApplyMove(move.X, move.Y, _aiColor);
                    return child;
                },
                playoutScore: (pos, player, rng) =>
                    ReversiMctsRolloutResult(pos, player, ReversiBoard.Opponent(player), rng),
                player: _aiColor,
                simulations: MonteCarloSimulations);

            if (bestMoveRef is not null)
                bestMove = new ReversiBoard.Move(bestMoveRef.X, bestMoveRef.Y);
        }
        else
        {
            MoveRef? bestMoveRef = _mcts.SearchBestMove(_board, _turn, _aiColor, MctsTimeLimitMs);
            if (bestMoveRef is not null)
                bestMove = new ReversiBoard.Move(bestMoveRef.X, bestMoveRef.Y);
        }

        return bestMove;
    }

    public bool MakeAiTurn() // не используется, но может пригодиться для игр ИИ друг с другом
    {
        if (IsGameOver)
            return false;

        if (_turn != _aiColor)
            return false;

        var legalMoves = _board.ValidMoves(_turn);

        ReversiBoard.Move? bestMove = FindBestAiMove(legalMoves); // вызываем даже если ходов нет и это пас - для MCTS

        if (legalMoves.Count == 0)
        {
            ResolveTurn();
            return true;
        }

        if (bestMove is null)
            return false;

        _board.ApplyMove(bestMove.Value.X, bestMove.Value.Y, _turn);
        ResolveTurn();
        return true;
    }

    /// <summary>
    /// Выяснить, чей дальше ход (или конец игры) после хода или паса
    /// </summary>
    private void ResolveTurn()
    {
        _turn = ReversiBoard.Opponent(_turn); // ход сделан, передаём ход противнику

        if (_board.HasAnyMoves(_turn)) // если у него есть допустимые ходы, он и должен ходить
            return;

        int opponent = ReversiBoard.Opponent(_turn);

        if (_board.HasAnyMoves(opponent)) // у текущего игрока ходов нет, но у противника есть
        {
            if (!HumanVsHuman && _turn == _aiColor)
                return; // если нет ходов у ИИ, оставляем очередь хода у ИИ, чтобы он сделал пас - важно для MCTS

            _turn = opponent; // если нет ходов у человека, то передаём ход противнику
            return;
        }

        IsGameOver = true; // если допустимых ходов нет ни у кого, игра закончена
        SetGameOverMessage();
    }

    private void SetGameOverMessage()
    {
        if (HumanVsHuman)
        {
            int whiteCount = _board.Count(ReversiBoard.WHITE);
            int blackCount = _board.Count(ReversiBoard.BLACK);

            if (whiteCount > blackCount)
                GameOverMessage = "Победили белые";
            else if (whiteCount < blackCount)
                GameOverMessage = "Победили чёрные";
            else
                GameOverMessage = "Ничья";

            return;
        }

        int humanCount = _board.Count(_humanColor);
        int aiCount = _board.Count(_aiColor);

        if (humanCount > aiCount)
            GameOverMessage = "Вы победили!";
        else if (humanCount < aiCount)
            GameOverMessage = "Победил ИИ";
        else
            GameOverMessage = "Ничья";
    }

    /// <summary>
    /// Случайное доигрывание позиции для Monte Carlo (sideToMove - чья очередь хода вначале)
    /// Возвращается результат с точки зрения игрока player, от 0 до 1
    /// Разница между количеством фишек учитывается с небольшим коэффициентом, главное - факт победы
    /// </summary>
    private static double ReversiMctsRolloutResult(ReversiBoard position, int player, int sideToMove, Random rng)
    {
        ReversiBoard simulation = position.Copy();
        int side = sideToMove;
        int passes = 0;

        const double alpha = 0.1;

        while (passes < 2) // два паса подряд - конец игры. Работает быстрее чем IsTerminal
        {
            Dictionary<ReversiBoard.Move, List<(int x, int y)>> legal = simulation.ValidMoves(side);

            if (legal.Count == 0)
            {
                passes++;
                side = ReversiBoard.Opponent(side);
                continue;
            }

            passes = 0;

            List<ReversiBoard.Move> moves = legal.Keys.ToList();
            ReversiBoard.Move choice = ChooseBiasedRolloutMove(moves, rng);
            simulation.ApplyMove(choice.X, choice.Y, side);

            // Если ходов нет, право хода всё равно переходит сопернику
            side = ReversiBoard.Opponent(side);
        }

        int diff = simulation.Count(player) - simulation.Count(ReversiBoard.Opponent(player));
        double margin = alpha * diff / 64.0;

        double value =
            diff > 0 ? 1.0 - alpha + margin : // от 1-alpha до 1
            diff == 0 ? 0.5 :
            alpha + margin; // от 0 до alpha, здесь margin < 0

        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// Является ли ход угловым, для предпочтений в случайных доигрываниях в MCTS
    /// </summary>
    private static bool IsCornerMove(ReversiBoard.Move move)
    {
        int last = ReversiBoard.BOARD_SIZE - 1;

        return
            (move.X == 0 && move.Y == 0) ||
            (move.X == 0 && move.Y == last) ||
            (move.X == last && move.Y == 0) ||
            (move.X == last && move.Y == last);
    }

    /// <summary>
    /// Выбрать ход для случайного доигрывания партии в MCTS, с предпочтением угловых ходов если они есть
    /// </summary>
    private static ReversiBoard.Move ChooseBiasedRolloutMove(List<ReversiBoard.Move> moves, Random rng)
    {
        if (moves.Count == 1)
            return moves[0];

        List<ReversiBoard.Move>? cornerMoves = null;

        foreach (ReversiBoard.Move move in moves)
        {
            if (!IsCornerMove(move))
                continue;

            cornerMoves ??= new List<ReversiBoard.Move>();
            cornerMoves.Add(move);
        }

        if (cornerMoves is not null &&
            cornerMoves.Count > 0 &&
            rng.NextDouble() < 0.5) // с какой вероятностью выбираем угловой ход, если он доступен
        {
            return cornerMoves[rng.Next(cornerMoves.Count)];
        }

        return moves[rng.Next(moves.Count)];
    }


    /// <summary>
    /// Для AlphaBeta в данной реализации нужен именно класс
    /// </summary>
    private sealed class MoveRef
    {
        public int X { get; }
        public int Y { get; }

        public MoveRef(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}