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

    public AiMode Mode { get; set; } = AiMode.AlphaBeta;
    public int AlphaBetaDepth { get; set; } = 3;
    public int MonteCarloSimulations { get; set; } = 25;

    public bool IsGameOver { get; private set; }

    /// <summary>
    /// Цвет пользователя для интерфейса
    /// </summary>
    public string HumanPlayerDisplayName => Players.CheckersName(_humanColor);

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

    public bool MakeAiTurn()
    {
        if (IsGameOver)
            return false;

        if (_turn != _aiColor)
            return false;

        List<CheckersBoard.MoveChain> moves = _board.AllMoves(_aiColor);
        if (moves.Count == 0)
        {
            CheckGameOver();
            return false;
        }

        CheckersBoard.MoveChain? bestMove;

        if (Mode == AiMode.AlphaBeta)
        {
            // Для ускорения поиска упорядочиваем ходы по длине цепочки: сначала обрабатываем длинные взятия
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
                isTerminal: (pos, side) => pos.AllMoves(side).Count == 0, // игра закончена, если игроку некуда ходить
                canPass: false,
                rootPlayer: _aiColor,
                depth: AlphaBetaDepth,
                alpha: double.NegativeInfinity,
                beta: double.PositiveInfinity,
                maximizingPlayer: true);

            bestMove = move;
        }
        else
        {
            bestMove = MonteCarlo.BestMove(
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

        if (bestMove is null)
        {
            CheckGameOver();
            return false;
        }

        _board.ApplyChain(bestMove);
        _turn = _humanColor;
        CheckGameOver();
        return true;
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
    }
}