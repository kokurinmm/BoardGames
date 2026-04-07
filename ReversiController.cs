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
    public int MonteCarloSimulations { get; set; } = 60;

    public bool IsGameOver { get; private set; }

    /// <summary>
    /// Цвет пользователя для интерфейса
    /// </summary>
    public string HumanPlayerDisplayName => Players.ReversiName(_humanColor);

    public string? GameOverMessage { get; private set; }

    /// <summary>
    /// Текущая позиция на доске
    /// </summary>
    private ReversiBoard _board = new();

    private int _humanColor; // цвет пользователя
    private int _aiColor; // цвет ИИ
    private int _turn; // игрок, которому принадлежит очередь хода

    private ReversiBoard.Move? _pendingAiMove; // текущий ход ИИ
    private bool _hasPendingAiMove; // выполнен ли уже найденный ход

    public bool IsAiTurn => !IsGameOver && _turn == _aiColor;

    public void NewGame() // запуск новой игры, в т.ч. случайный выбор цвета игроков
    {
        IsGameOver = false;
        _board = new ReversiBoard();

        _turn = ReversiBoard.BLACK; // в реверси первый ход принадлежит чёрным

        _humanColor = Random.Shared.Next(2) == 0 ? ReversiBoard.BLACK : ReversiBoard.WHITE;
        _aiColor = ReversiBoard.Opponent(_humanColor);

        GameOverMessage = null;

        _pendingAiMove = null; // на всякий случай - сброс анимации ИИ-хода
        _hasPendingAiMove = false;

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
    }

    /// <summary>
    /// Обработчик щелчка по клетке доски
    /// </summary>
    public void HandleCellClick(int row, int col)
    {
        if (IsGameOver)
            return;

        if (_turn != _humanColor)
            return;

        var legalMoves = _board.ValidMoves(_turn);
        ReversiBoard.Move move = new ReversiBoard.Move(row, col);
        if (!legalMoves.ContainsKey(move))
            return;

        _board.ApplyMove(row, col, _turn);
        _turn = ReversiBoard.Opponent(_turn);

        ResolveTurn();
    }

    public bool BeginAiTurnAnimation()
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        var legalMoves = _board.ValidMoves(_turn);

        if (legalMoves.Count == 0)
        {
            _turn = ReversiBoard.Opponent(_turn);
            ResolveTurn();
            return true;
        }

        ReversiBoard.Move? bestMove = FindBestAiMove(legalMoves);
        if (bestMove is null)
            return false;

        _pendingAiMove = bestMove.Value;
        _hasPendingAiMove = true;
        return true;
    }

    public bool HasPendingAiAnimation => _hasPendingAiMove;

    public bool ApplyNextAiAnimationStep()
    {
        if (!_hasPendingAiMove || _pendingAiMove is null)
            return false;

        _board.ApplyMove(_pendingAiMove.Value.X, _pendingAiMove.Value.Y, _turn);
        _pendingAiMove = null;
        _hasPendingAiMove = false;

        _turn = ReversiBoard.Opponent(_turn);
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

        if (legalMoves.Count == 0)
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
        else
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
                playoutScore: ReversiPlayoutScore,
                player: _aiColor,
                simulations: MonteCarloSimulations);

            if (bestMoveRef is not null)
                bestMove = new ReversiBoard.Move(bestMoveRef.X, bestMoveRef.Y);
        }

        return bestMove;
    }

    public bool MakeAiTurn()
    {
        if (IsGameOver)
            return false;

        if (_turn != _aiColor)
            return false;

        var legalMoves = _board.ValidMoves(_turn);
        if (legalMoves.Count == 0)
        {
            _turn = ReversiBoard.Opponent(_turn);
            ResolveTurn();
            return true;
        }

        ReversiBoard.Move? bestMove = FindBestAiMove(legalMoves);
        if (bestMove is null)
            return false;

        _board.ApplyMove(bestMove.Value.X, bestMove.Value.Y, _turn);
        _turn = ReversiBoard.Opponent(_turn);
        ResolveTurn();
        return true;
    }

    /// <summary>
    /// Выяснить, чей дальше ход (или конец игры) после хода или паса
    /// </summary>
    private void ResolveTurn()
    {
        if (_board.HasAnyMoves(_turn)) // если у текущего игрока есть допустимые ходы, он и должен ходить
            return;

        int opponent = ReversiBoard.Opponent(_turn);

        if (_board.HasAnyMoves(opponent)) // если ходов нет, а у противника есть, то ходит противник
        {
            _turn = opponent;
            return;
        }

        IsGameOver = true; // если допустимых ходов нет ни у кого, игра закончена
        SetGameOverMessage();
    }

    private void SetGameOverMessage()
    {
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
    /// Случайное доигрывание позиции для метода Монте-Карло. Возвращается результат с точки зрения игрока player
    /// </summary>
    private static double ReversiPlayoutScore(ReversiBoard position, int player, Random rng)
    {
        ReversiBoard simulation = position.Copy();
        int side = ReversiBoard.Opponent(player);

        const double AlphaMargin = 0.2;

        while (!simulation.IsTerminal())
        {
            var legal = simulation.ValidMoves(side).Keys.ToList();
            if (legal.Count > 0)
            {
                ReversiBoard.Move choice = legal[rng.Next(legal.Count)];
                simulation.ApplyMove(choice.X, choice.Y, side);
            }

            // Если ходов нет, право хода всё равно переходит сопернику
            side = ReversiBoard.Opponent(side);
        }

        int diff = simulation.Count(player) - simulation.Count(ReversiBoard.Opponent(player));
        double margin = AlphaMargin * diff / 64.0;
        return diff > 0 ? (1.0 + margin) : diff == 0 ? (0.5 + margin) : (0.0 + margin);
    }

    /// <summary>
    /// Тип хода для алгоритмов ИИ
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