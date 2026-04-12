using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Linq;

namespace BoardGames;

/// <summary>
/// Контроллер игры в уголки (отрисовка доски, обработка щелчков, вызов алгоритмов ИИ, начало и конец игры)
/// </summary>
public sealed class CornersController : IGameController
{
    public GameKind Kind => GameKind.Corners;
    public int BoardSize => CornersBoard.BOARD_SIZE;

    public string GameDisplayName => "Уголки";

    public int WhitePieceCount => _board.Count(CornersBoard.WHITE);
    public int BlackPieceCount => _board.Count(CornersBoard.BLACK);

    public AiMode Mode { get; set; } = AiMode.AlphaBeta;
    public int AlphaBetaDepth { get; set; } = 4;
    public int MonteCarloSimulations { get; set; } = 60;

    public bool IsGameOver { get; private set; }

    public string HumanPlayerDisplayName => Players.CornersName(_humanColor);

    public string? GameOverMessage { get; private set; }

    public bool HumanVsHuman { get; set; }

    public string CurrentTurnDisplayName => Players.CornersName(_turn);

    /// <summary>
    /// Текущая позиция на доске
    /// </summary>
    private CornersBoard _board = CornersBoard.Initial();

    private int _humanColor; // цвет пользователя
    private int _aiColor; // цвет ИИ
    private int _turn; // игрок, которому принадлежит очередь хода

    private (int row, int col)? _selectedPiece; // выбранная пользователем шашка

    /// <summary>
    /// Возможные продолжения хода для выбранной шашки
    /// </summary>
    private List<CornersBoard.MoveChain> _possibleMoves = new();

    /// <summary>
    /// Прыжковый ход пользователя начат, но не завершён
    /// </summary>
    private bool _jumpContinuationMode;

    /// <summary>
    /// Полный ход текущего игрока, накапливающийся по шагам
    /// </summary>
    private CornersBoard.MoveChain? _executedTurn;

    private CornersBoard.MoveChain? _pendingAiMove; // текущий ход ИИ, для анимации
    private int _pendingAiStepIndex; // текущий шаг в ходе ИИ, для анимации

    public bool IsAiTurn => !HumanVsHuman && !IsGameOver && _turn == _aiColor;

    public void NewGame() // Запуск новой игры, в т.ч. случайный выбор цвета игроков
    {
        IsGameOver = false;
        GameOverMessage = null;

        _board = CornersBoard.Initial();
        _turn = CornersBoard.WHITE;

        _selectedPiece = null;
        _possibleMoves.Clear();
        _jumpContinuationMode = false;
        _executedTurn = null;

        _pendingAiMove = null;

        // _humanColor и _aiColor используются только в режиме игры с ИИ
        _humanColor = Random.Shared.Next(2) == 0 ? CornersBoard.WHITE : CornersBoard.BLACK;
        _aiColor = CornersBoard.Opponent(_humanColor);
    }

    public void Draw(Graphics g, Rectangle rect)  // Отрисовка доски
    {
        int cell = rect.Width / BoardSize;

        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                int x = rect.Left + col * cell;
                int y = rect.Top + row * cell;

                Color squareColor;
                if (CornersBoard.IsWhiteHome(row, col))
                    squareColor = (row + col) % 2 == 0 ? Color.LemonChiffon : Color.Khaki;
                else if (CornersBoard.IsBlackHome(row, col))
                    squareColor = (row + col) % 2 == 0 ? Color.AliceBlue : Color.LightSteelBlue;
                else
                    squareColor = (row + col) % 2 == 0 ? Color.Wheat : Color.BurlyWood;

                using SolidBrush squareBrush = new SolidBrush(squareColor);
                g.FillRectangle(squareBrush, x, y, cell, cell);
            }

        using Pen gridPen = new Pen(Color.SaddleBrown, 1.5f);
        for (int i = 0; i <= BoardSize; i++)
        {
            int x = rect.Left + i * cell;
            int y = rect.Top + i * cell;
            g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
        }

        // внешние рамки домов
        using Pen whiteHomePen = new Pen(Color.Goldenrod, 3);
        using Pen blackHomePen = new Pen(Color.SteelBlue, 3);
        whiteHomePen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
        blackHomePen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;

        g.DrawRectangle(
            whiteHomePen,
            rect.Left + 0.5f,
            rect.Top + (BoardSize - CornersBoard.HOME_SIZE) * cell + 0.5f,
            CornersBoard.HOME_SIZE * cell - 1.0f,
            CornersBoard.HOME_SIZE * cell - 1.0f);

        g.DrawRectangle(
            blackHomePen,
            rect.Left + (BoardSize - CornersBoard.HOME_SIZE) * cell + 0.5f,
            rect.Top + 0.5f,
            CornersBoard.HOME_SIZE * cell - 1.0f,
            CornersBoard.HOME_SIZE * cell - 1.0f);

        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                int piece = _board.Grid[row, col];
                if (piece == CornersBoard.EMPTY)
                    continue;

                int x = rect.Left + col * cell;
                int y = rect.Top + row * cell;

                RectangleF pieceRect = new RectangleF(x + 8, y + 8, cell - 16, cell - 16);
                using SolidBrush pieceBrush = new SolidBrush(piece == CornersBoard.WHITE ? Color.White : Color.Black);
                using Pen outline = new Pen(Color.DimGray, 1.5f);
                g.FillEllipse(pieceBrush, pieceRect);
                g.DrawEllipse(outline, pieceRect);

                if (CornersBoard.IsGoalHome(row, col, piece))
                {
                    using Pen homeRing = new Pen(piece == CornersBoard.WHITE ? Color.Goldenrod : Color.SteelBlue, 2.5f);
                    RectangleF ringRect = new RectangleF(x + 13, y + 13, cell - 26, cell - 26);
                    g.DrawEllipse(homeRing, ringRect);
                }
            }

        if (_selectedPiece is (int selectedRow, int selectedCol))
        {
            float x1 = rect.Left + selectedCol * cell + 1.5f;
            float y1 = rect.Top + selectedRow * cell + 1.5f;
            float w = cell - 3.5f;
            float h = cell - 3.5f;

            using Pen selectionPen = new Pen(_jumpContinuationMode ? Color.DarkOrange : Color.DarkBlue, 3);
            selectionPen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
            g.DrawRectangle(selectionPen, x1, y1, w, h);

            // Отмечаем возможные ходы, в т.ч. саму выбранную шашку - щелчок на ней завершает серию прыжков
            if (_jumpContinuationMode)
            {
                float cx = rect.Left + selectedCol * cell + cell / 2.0f;
                float cy = rect.Top + selectedRow * cell + cell / 2.0f;
                float radius = cell * 0.12f;
                using Pen stopRing = new Pen(Color.OrangeRed, 3);
                g.DrawEllipse(stopRing, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }

            foreach (CornersBoard.MoveChain chain in _possibleMoves)
            {
                if (chain.Steps.Count == 0)
                    continue;

                CornersBoard.MoveStep first = chain.Steps[0];
                float cx = rect.Left + first.C2 * cell + cell / 2.0f;
                float cy = rect.Top + first.R2 * cell + cell / 2.0f;
                float radius = cell * 0.18f;

                using SolidBrush dot = new SolidBrush(Color.FromArgb(150, _jumpContinuationMode ? Color.Orange : Color.Green));
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

        if (!HumanVsHuman && _turn != _humanColor)
            return;

        if (_jumpContinuationMode && _selectedPiece is (int curRow, int curCol))
        {
            // повторный щелчок по текущей шашке завершает цепочку прыжков
            if (row == curRow && col == curCol)
            {
                FinishTurn();
                return;
            }
        }

        // вне режима продолжения прыжка можно выбрать любую свою шашку
        if (!_jumpContinuationMode && _board.Grid[row, col] == _turn)
        {
            _selectedPiece = (row, col);
            _executedTurn = null;

            List<CornersBoard.MoveChain> allMoves = _board.AllMoves(_turn);
            _possibleMoves = allMoves
                .Where(move => move.Steps.Count > 0 && move.Steps[0].R1 == row && move.Steps[0].C1 == col)
                .ToList();
            return;
        }

        if (_selectedPiece is null)
            return;

        List<CornersBoard.MoveChain> matching = _possibleMoves
            .Where(move => move.Steps.Count > 0 && move.Steps[0].R2 == row && move.Steps[0].C2 == col)
            .ToList();

        if (matching.Count == 0)
            return;

        CornersBoard.MoveStep step = matching[0].Steps[0];
        _board.ApplyStep(step);
        AppendExecutedStep(step);

        if (step.IsJump)
        {
            // после прыжка можно либо продолжить цепочку, либо остановиться
            List<CornersBoard.MoveChain> continuations = _board.JumpSequencesFrom(row, col);
            if (continuations.Count > 0)
            {
                _selectedPiece = (row, col);
                _possibleMoves = continuations;
                _jumpContinuationMode = true;
                return;
            }
        }

        FinishTurn();
    }

    public bool BeginAiTurnAnimation()
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        _pendingAiMove = FindBestAiMove();
        _pendingAiStepIndex = 0;

        return _pendingAiMove is not null && _pendingAiMove.Steps.Count > 0;
    }

    public bool HasPendingAiAnimation =>
        _pendingAiMove is not null &&
        _pendingAiStepIndex < _pendingAiMove.Steps.Count;

    public bool ApplyNextAiAnimationStep()
    {
        if (!HasPendingAiAnimation || _pendingAiMove is null)
            return false;

        CornersBoard.MoveStep step = _pendingAiMove.Steps[_pendingAiStepIndex];
        _board.ApplyStep(step);
        AppendExecutedStep(step);
        _pendingAiStepIndex++;

        if (_pendingAiStepIndex >= _pendingAiMove.Steps.Count)
        {
            _pendingAiMove = null;
            _pendingAiStepIndex = 0;
            FinishTurn();
        }

        return true;
    }

    private void AppendExecutedStep(CornersBoard.MoveStep step)
    {
        if (_executedTurn is null)
            _executedTurn = new CornersBoard.MoveChain(new[] { step });
        else
            _executedTurn.Steps.Add(step);
    }

    public bool MakeAiTurn()
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        CornersBoard.MoveChain? move = FindBestAiMove();
        if (move is null || move.Steps.Count == 0)
            return false;

        _board.ApplyChain(move, _turn);
        ResetSelection();

        if (CheckGameOver(_turn))
            return true;

        _turn = CornersBoard.Opponent(_turn);

        return true;
    }

    /// <summary>
    /// Найти лучший ход ИИ (или цепочку), но не применять его, чтобы MainForm могла показать ходы в цепочке по одному
    /// </summary>
    private CornersBoard.MoveChain? FindBestAiMove()
    {

        if (IsGameOver || _turn != _aiColor)
            return null;

        List<CornersBoard.MoveChain> legalMoves = _board.AllMoves(_turn)
            .OrderByDescending(move => _board.MoveOrderingScore(move, _turn))
            .ToList();

        if (Mode == AiMode.AlphaBeta)
        {
            (double score, CornersBoard.MoveChain? move) = AlphaBeta.Search(
                position: _board,
                legalMoves: (pos, side) => pos.AllMoves(side)
                    .OrderByDescending(m => pos.MoveOrderingScore(m, side))
                    .ToList(),
                applyMoveToCopy: (pos, move, side) =>
                {
                    CornersBoard child = pos.Copy();
                    child.ApplyChain(move, side);
                    return child;
                },
                evaluate: (pos, root, side, generatedMoves) => pos.Evaluate(root, side, generatedMoves),
                opponent: CornersBoard.Opponent,
                isTerminal: (pos, side) => pos.IsTerminal() || pos.AllMoves(side).Count == 0,
                canPass: false,
                rootPlayer: _aiColor,
                depth: AlphaBetaDepth,
                alpha: double.NegativeInfinity,
                beta: double.PositiveInfinity,
                maximizingPlayer: true);

            return move;
        }

        return MonteCarlo.BestMove(
            position: _board,
            legalMoves: (pos, side) => pos.AllMoves(side),
            applyMoveToCopy: (pos, move) =>
            {
                CornersBoard child = pos.Copy();
                child.ApplyChain(move, _aiColor);
                return child;
            },
            playoutScore: CornersPlayoutScore,
            player: _aiColor,
            simulations: MonteCarloSimulations);
    }

    /// <summary>
    /// Случайное доигрывание позиции для метода Монте-Карло. Возвращается результат с точки зрения игрока player
    /// </summary>
    private static double CornersPlayoutScore(CornersBoard position, int player, Random rng)
    {
        CornersBoard simulation = position.Copy();
        int side = CornersBoard.Opponent(player);

        while (true)
        {
            if (simulation.HasBuiltGoalHouse(CornersBoard.WHITE) ||
                simulation.HasBuiltGoalHouse(CornersBoard.BLACK) ||
                simulation.BlackMovesPlayed >= CornersBoard.BLACK_MOVE_LIMIT)
            {
                return CornersTerminalScore(simulation, player);
            }

            List<CornersBoard.MoveChain> moves = simulation.AllMoves(side);
            if (moves.Count == 0)
            {
                int winner = CornersBoard.Opponent(side);
                return winner == player ? 1.0 : -1.0;
            }

            CornersBoard.MoveChain randomMove = moves[rng.Next(moves.Count)];
            simulation.ApplyChain(randomMove, side);

            side = CornersBoard.Opponent(side);
        }
    }

    /// <summary>
    /// Вспомогательный метод для оценки позиции в конце игры, применяемый в методе Монте-Карло
    /// </summary>
    private static double CornersTerminalScore(CornersBoard board, int player)
    {
        int opponent = CornersBoard.Opponent(player);

        int myGoal = board.CountInGoalHome(player);
        int oppGoal = board.CountInGoalHome(opponent);

        double goalMargin = (myGoal - oppGoal) / 9.0;

        const double AlphaGoal = 0.20;

        int? winner = board.GetWinner();

        if (winner is null)
            return 0.0;

        double baseScore = winner == player ? 1.0 : -1.0;
        return baseScore + AlphaGoal * goalMargin;
    }

    private void FinishTurn() // завершение хода
    {
        if (_executedTurn is null)
            return;

        _board.UpdateAfterFullMove(_executedTurn, _turn);
        ResetSelection();

        if (CheckGameOver(_turn))
            return;

        _turn = CornersBoard.Opponent(_turn);

    }

    private void ResetSelection()
    {
        _selectedPiece = null;
        _possibleMoves.Clear();
        _jumpContinuationMode = false;
        _executedTurn = null;
    }

    /// <summary>
    /// Проверка завершения игры после хода movedPlayer
    /// </summary>
    private bool CheckGameOver(int movedPlayer) // 
    {

        if (_board.HasBuiltGoalHouse(movedPlayer))
        {
            IsGameOver = true;
            SetWinnerMessage(movedPlayer);
            return true;
        }

        if (_board.BlackMovesPlayed >= CornersBoard.BLACK_MOVE_LIMIT)
        {
            IsGameOver = true;
            SetWinnerMessage(_board.GetWinner());
            return true;
        }

        if (_board.AllMoves(CornersBoard.Opponent(movedPlayer)).Count == 0)
        {
            IsGameOver = true;
            SetWinnerMessage(movedPlayer);
            return true;
        }

        return false;
    }

    private void SetWinnerMessage(int? winner)
    {
        if (winner is null)
        {
            GameOverMessage = "Ничья";
            return;
        }

        if (HumanVsHuman)
        {
            GameOverMessage = winner == CornersBoard.WHITE ? "Победили белые" : "Победили чёрные";
            return;
        }

        GameOverMessage = winner == _humanColor ? "Вы победили!" : "Победил ИИ";
    }

}
