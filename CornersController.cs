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
    public int MaxDepth { get; set; } = 6;
    public int MonteCarloSimulations { get; set; } = 60;
    public int MctsTimeLimitMs { get; set; } = 750;

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

    private (int Row, int Col)? _currentMoveOrigin; // исходная клетка текущего полного хода

    private readonly HashSet<CornersBoard.Square> _visitedSquares = new(); // множество уже посещённых клеток в текущем ходе

    private CornersBoard.MoveChain? _pendingAiMove; // текущий ход ИИ, для анимации
    private int _pendingAiStepIndex; // текущий шаг в ходе ИИ, для анимации

    private readonly MctsSession<CornersBoard, CornersBoard.MoveChain> _mcts; // сеанс MCTS

    public bool IsAiTurn => !HumanVsHuman && !IsGameOver && _turn == _aiColor;

    public CornersController()
    {
        _mcts = new MctsSession<CornersBoard, CornersBoard.MoveChain>(
            legalMoves: (pos, side) => pos.AllMoves(side),
            applyMoveToCopy: (pos, move, side) =>
            {
                CornersBoard child = pos.Copy();
                child.ApplyChain(move, side);
                return child;
            },
            rolloutScore: CornersMctsRolloutResult,
            opponent: CornersBoard.Opponent,
            isTerminal: (pos, side) => pos.IsTerminal(),
            positionKey: pos => pos.GetStateKey(),
            canPass: false,
            explorationConstant: Math.Sqrt(2.0));
    }

    public void NewGame() // Запуск новой игры, в т.ч. случайный выбор цвета игроков
    {
        IsGameOver = false;
        GameOverMessage = null;

        _board = CornersBoard.Initial();
        _turn = CornersBoard.WHITE;

        _selectedPiece = null;
        _possibleMoves.Clear();
        _jumpContinuationMode = false;
        ResetExecutedTurn();

        _pendingAiMove = null;

        // _humanColor и _aiColor используются только в режиме игры с ИИ
        _humanColor = Random.Shared.Next(2) == 0 ? CornersBoard.WHITE : CornersBoard.BLACK;
        _aiColor = CornersBoard.Opponent(_humanColor);
        _mcts.Reset(); // перезапуск сеанса MCTS
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

            using Pen selectionPen = new Pen(Color.DarkBlue, 3);
            selectionPen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
            g.DrawRectangle(selectionPen, x1, y1, w, h);

            // Если есть варианты, отмечаем возможные ходы и саму выбранную шашку - щелчок на ней завершает серию прыжков
            if (_jumpContinuationMode && _possibleMoves.Count > 0)
            {
                float cx = rect.Left + selectedCol * cell + cell / 2.0f;
                float cy = rect.Top + selectedRow * cell + cell / 2.0f;
                float radius = cell * 0.18f;

                using SolidBrush dot = new SolidBrush(Color.FromArgb(150, Color.LimeGreen));
                g.FillEllipse(dot, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }

            foreach (CornersBoard.MoveChain chain in _possibleMoves)
            {
                if (chain.Steps.Count == 0)
                    continue;

                CornersBoard.MoveStep first = chain.Steps[0];
                float cx = rect.Left + first.C2 * cell + cell / 2.0f;
                float cy = rect.Top + first.R2 * cell + cell / 2.0f;
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
            ResetExecutedTurn();

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
            // На уже посещённые клетки нельзя возвращаться в течение того же самого прыжка
            // Если допустимых продолжений нет, ход завершается

            (int startRow, int startCol) = MoveOrigin();

            List<CornersBoard.MoveChain> continuations =
                _board.JumpSequencesFrom(
                    row,
                    col,
                    _turn,
                    startRow,
                    startCol,
                    currentSequence: null,
                    visitedSquares: _visitedSquares);

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

    /// <summary>
    /// Исходная клетка текущего полного хода пользователя
    /// </summary>
    private (int Row, int Col) MoveOrigin()
    {
        if (_currentMoveOrigin is (int row, int col))
            return (row, col);

        if (_selectedPiece is (int selectedRow, int selectedCol))
            return (selectedRow, selectedCol); // если ход ещё не начался, но фишка выбрана

        throw new InvalidOperationException("Не удалось определить исходную клетку текущего хода");
    }

    /// <summary>
    /// Инициализирует полный ход _executedTurn, запоминает стартовую клетку _currentMoveOrigin,
    /// поддерживает множество посещённых клеток _visitedSquares
    /// </summary>
    private void AppendExecutedStep(CornersBoard.MoveStep step)
    {
        _executedTurn ??= new CornersBoard.MoveChain(Array.Empty<CornersBoard.MoveStep>());

        if (_executedTurn.Steps.Count == 0)
        {
            _currentMoveOrigin = (step.R1, step.C1);
            _visitedSquares.Clear();
            _visitedSquares.Add(new CornersBoard.Square(step.R1, step.C1));
        }

        _executedTurn.Steps.Add(step);
        _visitedSquares.Add(new CornersBoard.Square(step.R2, step.C2));
    }

    /// <summary>
    /// Очистка описания полного хода _executedTurn , _currentMoveOrigin , _visitedSquares 
    /// </summary>
    private void ResetExecutedTurn()
    {
        _executedTurn = null;
        _currentMoveOrigin = null;
        _visitedSquares.Clear();
    }

    public bool MakeAiTurn() // не используется, но может пригодиться для игр ИИ друг с другом
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
                isTerminal: (pos, side) => pos.IsTerminal(),
                canPass: false,
                rootPlayer: _aiColor,
                depth: AlphaBetaDepth,
                alpha: double.NegativeInfinity,
                beta: double.PositiveInfinity,
                maximizingPlayer: true);

            return move;
        }
        else if (Mode == AiMode.MonteCarlo)
        {
            return MonteCarlo.BestMove(
                position: _board,
                legalMoves: (pos, side) => pos.AllMoves(side),
                applyMoveToCopy: (pos, move) =>
                {
                    CornersBoard child = pos.Copy();
                    child.ApplyChain(move, _aiColor);
                    return child;
                },
                playoutScore: (pos, player, rng) =>
                CornersMctsRolloutResult(pos, player, CornersBoard.Opponent(player), rng),
                player: _aiColor,
                simulations: MonteCarloSimulations);
        }
        else
            return _mcts.SearchBestMove(_board, _turn, _aiColor, MctsTimeLimitMs);
    }


    /// <summary>
    /// Случайное доигрывание позиции для Monte Carlo (sideToMove - чья очередь хода вначале)
    /// Возвращается результат с точки зрения игрока player, от 0 до 1
    /// </summary>
    private static double CornersMctsRolloutResult(CornersBoard position, int player, int sideToMove, Random rng)
    {
        CornersBoard simulation = position.Copy();
        int side = sideToMove;

        const double alpha = 0.2;
        int? winner = null;

        for (int ply = 0; ply < 15 ; ply++) // для Уголков ограничиваем доигрывание 15 ходами
        {
            if (simulation.IsTerminal())
            {
                winner = simulation.GetWinner();
                break;
            }

            List<CornersBoard.MoveChain> moves = simulation.AllMoves(side);

            if (moves.Count > 0)
            {
                CornersBoard.MoveChain randomMove = ChooseBiasedRolloutMove(simulation, side, moves, rng);
                simulation.ApplyChain(randomMove, side);
                side = CornersBoard.Opponent(side);
            }
            else
            {
                winner = CornersBoard.Opponent(side);
                break;
            }
        }

        int myDist = simulation.SumDistanceToGoal(player);
        int oppDist = simulation.SumDistanceToGoal(CornersBoard.Opponent(player));
        double margin = alpha * (oppDist - myDist) / (myDist + oppDist + 1.0);

        if (winner is null)
            return 0.5 + margin; // около 0.5 при ничье, обычно от 1-a до 1 при победе, обычно от 0 до a при поражении
        else
            return winner == player ? 1.0 - alpha + margin : alpha + margin ;

    }

    /// <summary>
    /// Выбор хода для Rollout: чем сильнее ход уменьшает расстояние до цели, тем больше вес, но все ходы возможны
    /// </summary>
    private static CornersBoard.MoveChain ChooseBiasedRolloutMove(
        CornersBoard position,
        int side,
        List<CornersBoard.MoveChain> moves,
        Random rng)
    {
        if (moves.Count == 1)
            return moves[0];

        double[] weights = new double[moves.Count];
        double totalWeight = 0.0;

        for (int i = 0; i < moves.Count; i++)
        {
            double h = position.NormalizedMoveDeltaDist(side, moves[i]); // [-1, 1]
            weights[i] = 0.5 * (h + 1.0);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0.0)
            return moves[rng.Next(moves.Count)];

        int index = ChooseWeightedIndex(weights, totalWeight, rng);
        return moves[index];
    }

    /// <summary>
    /// Выбрать индекс по положительным весам
    /// </summary>
    private static int ChooseWeightedIndex(double[] weights, double totalWeight, Random rng)
    {
        double r = rng.NextDouble() * totalWeight;

        for (int i = 0; i < weights.Length; i++)
        {
            r -= weights[i];
            if (r <= 0.0)
                return i;
        }

        return weights.Length - 1;
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
        ResetExecutedTurn();
    }

    /// <summary>
    /// Проверка завершения игры после хода movedPlayer
    /// </summary>
    private bool CheckGameOver(int movedPlayer) // 
    {
        if (_board.IsTerminal())
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
