using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Linq;

namespace BoardGames;

/// <summary>
/// Контроллер игры в шашки (отрисовка доски, обработка щелчков, вызов алгоритмов ИИ, начало и конец игры)
/// </summary>
public sealed class CheckersController : IGameController
{
    public GameKind Kind => GameKind.Checkers;
    public int BoardSize => CheckersBoard.BOARD_SIZE;

    public string GameDisplayName => "Шашки";

    public int WhitePieceCount => _board.Count(CheckersBoard.WHITE);

    public int BlackPieceCount => _board.Count(CheckersBoard.BLACK);

    public AiMode Mode { get; set; } = AiMode.AlphaBeta;
    public int AlphaBetaDepth { get; set; } = 3;
    public int MonteCarloSimulations { get; set; } = 25;

    public bool IsGameOver { get; private set; }

    /// <summary>
    /// Цвет пользователя для интерфейса
    /// </summary>
    public string HumanPlayerDisplayName => Players.CheckersName(_humanColor);

    public string? GameOverMessage { get; private set; }

    /// <summary>
    /// Текущая позиция на доске
    /// </summary>
    private CheckersBoard _board = CheckersBoard.Initial();

    private int _humanColor; // цвет пользователя
    private int _aiColor; // цвет ИИ
    private int _turn; // игрок, которому принадлежит очередь хода

    /// <summary>
    /// Выбранная пользователем клетка (или null)
    /// </summary>
    private (int row, int col)? _selectedPiece;

    /// <summary>
    /// Список возможных продолжений хода для выбранной фигуры
    /// </summary>
    private List<CheckersBoard.MoveChain> _possibleMoves = new();

    /// <summary>
    /// Обязан ли пользователь продолжать цепочку взятий той же фигурой
    /// </summary>
    private bool _mustContinueJump;

    private CheckersBoard.MoveChain? _pendingAiMove; // текущий ход или цепочка ходов ИИ
    private int _pendingAiStepIndex; // текущий шаг в цепочке ходов ИИ

    public bool IsAiTurn => !IsGameOver && _turn == _aiColor;

    public void NewGame() // Запуск новой игры, в т.ч. случайный выбор цвета игроков
    {
        IsGameOver = false;
        _board = CheckersBoard.Initial();

        _humanColor = Random.Shared.Next(2) == 0 ? CheckersBoard.WHITE : CheckersBoard.BLACK;
        _aiColor = CheckersBoard.Opponent(_humanColor);

        _turn = CheckersBoard.WHITE;

        _selectedPiece = null;
        _possibleMoves.Clear();
        _mustContinueJump = false;
        GameOverMessage = null;

        _pendingAiMove = null; // на всякий случай - сброс анимации ИИ-хода
    }

    public void Draw(Graphics g, Rectangle rect) // Отрисовка доски
    {
        int cell = rect.Width / BoardSize;

        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                int x = rect.Left + col * cell;
                int y = rect.Top + row * cell;

                bool dark = (row + col) % 2 == 1;
                using SolidBrush brush = new SolidBrush(
                    dark ? Color.Peru : Color.PeachPuff);
                g.FillRectangle(brush, x, y, cell, cell);

                int piece = _board.Grid[row, col];
                if (piece != CheckersBoard.EMPTY)
                {
                    bool isWhite = piece > 0;
                    bool isKing = CheckersBoard.IsKing(piece);

                    RectangleF pieceRect = new RectangleF(x + 8, y + 8, cell - 16, cell - 16);
                    using SolidBrush pieceBrush = new SolidBrush(isWhite ? Color.White : Color.Black);
                    using Pen outline = new Pen(Color.Gray, 1);
                    g.FillEllipse(pieceBrush, pieceRect);
                    g.DrawEllipse(outline, pieceRect);

                    if (isKing)
                    {
                        using Pen ringPen = new Pen(isWhite ? Color.DodgerBlue : Color.Gold, 3);
                        RectangleF ringRect = new RectangleF(x + 12, y + 12, cell - 24, cell - 24);
                        g.DrawEllipse(ringPen, ringRect);
                    }
                }
            }

        // Если пользователь выбрал свою фигуру, выделим её и покажем возможные ходы
        if (_selectedPiece is (int selectedRow, int selectedCol))
        {
            float x1 = rect.Left + selectedCol * cell + 1.5f;
            float y1 = rect.Top + selectedRow * cell + 1.5f;
            float x2 = rect.Left + (selectedCol + 1) * cell - 3.5f;
            float y2 = rect.Top + (selectedRow + 1) * cell - 3.5f;

            using Pen selectionPen = new Pen(Color.Blue, 3);
            selectionPen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
            g.DrawRectangle(selectionPen, x1, y1, x2 - x1, y2 - y1);

            foreach (CheckersBoard.MoveChain chain in _possibleMoves)
            {
                if (chain.Steps.Count == 0)
                    continue;

                CheckersBoard.MoveStep firstStep = chain.Steps[0];
                float cx = rect.Left + firstStep.C2 * cell + cell / 2.0f;
                float cy = rect.Top + firstStep.R2 * cell + cell / 2.0f;
                float radius = cell * 0.18f;

                using SolidBrush dot = new SolidBrush(Color.FromArgb(150, Color.Green));
                g.FillEllipse(dot, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }
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

        // Если пользователь не обязан продолжать взятие, то может выбрать другую фигуру
        if (!_mustContinueJump && CheckersBoard.IsPlayersPiece(_board.Grid[row, col], _humanColor))
        {
            _selectedPiece = (row, col);

            List<CheckersBoard.MoveChain> allMoves = _board.AllMoves(_humanColor);
            _possibleMoves = allMoves
                .Where(m => m.Steps.Count > 0 && m.Steps[0].R1 == row && m.Steps[0].C1 == col)
                .ToList();
            return;
        }

        if (_selectedPiece is null)
            return;

        // Ищем цепочки, у которых первый шаг ведёт в выбранную клетку
        List<CheckersBoard.MoveChain> matching = _possibleMoves
            .Where(chain => chain.Steps.Count > 0 && chain.Steps[0].R2 == row && chain.Steps[0].C2 == col)
            .ToList();

        if (matching.Count == 0)
            return;

        CheckersBoard.MoveStep step = matching[0].Steps[0];
        _board.ApplyStep(step);

        // Если это было взятие, проверяем, нужно ли продолжать цепочку
        if (step.Captured.HasValue)
        {
            List<CheckersBoard.MoveChain> continuations = _board.JumpSequencesFrom(row, col);
            if (continuations.Count > 0)
            {
                _selectedPiece = (row, col);
                _possibleMoves = continuations;
                _mustContinueJump = true;
                return;
            }
        }

        _selectedPiece = null;
        _possibleMoves.Clear();
        _mustContinueJump = false;

        _turn = _aiColor;
        CheckGameOver();
    }

    public bool BeginAiTurnAnimation()
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        _pendingAiMove = FindBestAiMove();
        _pendingAiStepIndex = 0;

        return _pendingAiMove is not null;
    }

    public bool HasPendingAiAnimation =>
    _pendingAiMove is not null &&
    _pendingAiStepIndex < _pendingAiMove.Steps.Count;

    public bool ApplyNextAiAnimationStep()
    {
        if (!HasPendingAiAnimation || _pendingAiMove is null)
            return false;

        _board.ApplyStep(_pendingAiMove.Steps[_pendingAiStepIndex]);
        _pendingAiStepIndex++;

        // если это был последний шаг, завершаем ход
        if (_pendingAiStepIndex >= _pendingAiMove.Steps.Count)
        {
            _pendingAiMove = null;
            _pendingAiStepIndex = 0;

            _turn = CheckersBoard.Opponent(_turn);
            CheckGameOver();
        }

        return true;
    }

    /// <summary>
    /// Применить один ход. Используется MainForm для анимации цепочки ходов
    /// </summary>
    public void ApplyAiMoveStep(CheckersBoard.MoveStep step)
    {
        _board.ApplyStep(step);
    }

    /// <summary>
    /// Завершить ход ИИ
    /// </summary>
    public void FinishAiMove()
    {
        _turn = CheckersBoard.Opponent(_turn);
        CheckGameOver();
    }

    public bool MakeAiTurn()
    {
        CheckersBoard.MoveChain? bestMove = FindBestAiMove();
        if (bestMove is null)
            return false;

        _board.ApplyChain(bestMove);
        _turn = CheckersBoard.Opponent(_turn);
        CheckGameOver();
        return true;
    }

    /// <summary>
    /// Найти лучший ход ИИ (или цепочку), но не применять его, чтобы MainForm могла показать ходы в цепочке по одному
    /// </summary>
    public CheckersBoard.MoveChain? FindBestAiMove()
    {
        if (IsGameOver || _turn != _aiColor)
            return null;

        List<CheckersBoard.MoveChain> legalMoves = _board.AllMoves(_turn);
        if (legalMoves.Count == 0)
        {
            CheckGameOver();
            return null;
        }

        if (Mode == AiMode.AlphaBeta)
        {
            (double score, CheckersBoard.MoveChain? move) = AlphaBeta.Search(
                position: _board,
                legalMoves: (pos, side) => pos.AllMoves(side).OrderByDescending(ch => ch.Length).ToList(),
                applyMoveToCopy: (pos, moveChain, side) =>
                {
                    CheckersBoard child = pos.Copy();
                    child.ApplyChain(moveChain);
                    return child;
                },
                evaluate: (pos, root, side, generatedMoves) => pos.Evaluate(root, side, generatedMoves),
                opponent: CheckersBoard.Opponent,
                isTerminal: (pos, side) => pos.AllMoves(side).Count == 0,
                canPass: false,
                rootPlayer: _aiColor,
                depth: AlphaBetaDepth,
                alpha: double.NegativeInfinity,
                beta: double.PositiveInfinity,
                maximizingPlayer: true);

            return move;
        }
        else
        {
            return MonteCarlo.BestMove(
                position: _board,
                legalMoves: (pos, side) => pos.AllMoves(side),
                applyMoveToCopy: (pos, moveChain) =>
                {
                    CheckersBoard child = pos.Copy();
                    child.ApplyChain(moveChain);
                    return child;
                },
                playoutScore: CheckersPlayoutScore,
                player: _aiColor,
                simulations: MonteCarloSimulations);
        }
    }

    /// <summary>
    /// Случайное доигрывание позиции для метода Монте-Карло. Возвращается результат с точки зрения игрока player
    /// </summary>
    private static double CheckersPlayoutScore(CheckersBoard position, int player, Random rng)
    {
        CheckersBoard simulation = position.Copy();
        int side = CheckersBoard.Opponent(player);

        const int playoutLimit = 25;

        for (int t = 0; t < playoutLimit; t++)
        {
            List<CheckersBoard.MoveChain> moves = simulation.AllMoves(side);
            if (moves.Count == 0)
            {
                int winner = CheckersBoard.Opponent(side);
                return winner == player ? 1.0 : -1.0;
            }

            CheckersBoard.MoveChain randomMove = moves[rng.Next(moves.Count)];
            simulation.ApplyChain(randomMove);
            side = CheckersBoard.Opponent(side);
        }

        return 0.0;
    }

    /// <summary>
    /// Проигрывает тот, у кого нет допустимых ходов в свою очередь хода
    /// </summary>
    private void CheckGameOver()
    {
        if (IsGameOver)
            return;

        List<CheckersBoard.MoveChain> moves = _board.AllMoves(_turn);
        if (moves.Count > 0)
            return;

        IsGameOver = true;

        if (_turn == _humanColor)
            GameOverMessage = "Победил ИИ";
        else
            GameOverMessage = "Вы победили!";
    }
}