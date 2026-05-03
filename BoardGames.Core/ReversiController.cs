using System;
using System.Collections.Generic;
using System.Linq;
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
    public int MaxDepth { get; set; } = 9;
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

    private (int row, int col)? _lastAiSquare; // клетка, куда сходил ИИ (или пользователь в режиме HumanVsHuman)

    private ReversiBoard.Move? _pendingAiMove; // текущий ход ИИ
    private bool _hasPendingAiMove; // выполнен ли уже найденный ход

    private readonly MctsSession<ReversiBoard, MoveRef> _mcts; // сеанс MCTS

    public bool IsAiTurn => !HumanVsHuman && !IsGameOver && _turn == _aiColor;

    public ReversiController()
    {
        _mcts = new MctsSession<ReversiBoard, MoveRef>(
            // Здесь преобразование ходов в тип MoveRef
            legalMoves: (pos, side) => pos.LegalMoves(side).Select(m => new MoveRef(m.X, m.Y)).ToList(),
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

    public void Draw(IBoardCanvas canvas, BoardRect rect) // Отрисовка доски
    {
        float cell = rect.Width / BoardSize;

        canvas.FillRectangle(GameColors.DarkGreen, rect.Left, rect.Top, rect.Width, rect.Height);

        for (int i = 0; i <= BoardSize; i++)
        {
            float x = rect.Left + i * cell;
            float y = rect.Top + i * cell;
            canvas.DrawLine(GameColors.Black, 2, x, rect.Top, x, rect.Bottom);
            canvas.DrawLine(GameColors.Black, 2, rect.Left, y, rect.Right, y);
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

                GameColor fill = piece == ReversiBoard.BLACK
                    ? GameColors.Black
                    : GameColors.White;

                canvas.FillEllipse(fill, cx - radius, cy - radius, 2 * radius, 2 * radius);

                if (piece == ReversiBoard.WHITE)
                    canvas.DrawEllipse(GameColors.Black, 1, cx - radius, cy - radius, 2 * radius, 2 * radius);
            }

        // Подсветка допустимых ходов текущего игрока
        var legalMoves = _board.LegalMoves(_turn);
        foreach (ReversiBoard.Move move in legalMoves)
        {
            float cx = rect.Left + move.Y * cell + cell / 2.0f;
            float cy = rect.Top + move.X * cell + cell / 2.0f;
            float radius = cell * 0.12f;

            canvas.FillEllipse(GameColors.Yellow, cx - radius, cy - radius, 2 * radius, 2 * radius);
        }

        // Подсветка последнего хода ИИ
        if (_lastAiSquare is (int aiRow, int aiCol))
        {
            float x = rect.Left + aiCol * cell + 1.5f;
            float y = rect.Top + aiRow * cell + 1.5f;
            float w = cell - 3.5f;
            float h = cell - 3.5f;

            canvas.DrawRectangle(GameColors.Crimson, 3, x, y, w, h);
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

        ReversiBoard.Move move = new ReversiBoard.Move(row, col);
        if (!_board.IsLegalMove(row, col, _turn))
            return;

        _board.ApplyMove(row, col, _turn);

        if (HumanVsHuman)
            _lastAiSquare = (row, col); // в режиме без ИИ обводим такой же рамкой, как у ИИ

        ResolveTurn();
    }

    public bool BeginAiTurnAnimation() // Подготовить ход ИИ (в реверси нет анимированных ходов из нескольких шагов)
    {
        if (IsGameOver || _turn != _aiColor)
            return false;

        ReversiBoard.Move? bestMove = FindBestAiMove(); // вызываем даже если ходов нет и это пас - для MCTS

        if (!_board.HasAnyMoves(_turn))
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
    private ReversiBoard.Move? FindBestAiMove()
    {
        if (Mode != AiMode.Mcts && !_board.HasAnyMoves(_aiColor))
            return null; // даже если ходов нет, алгоритм MCTS должен обработать пас и перепривязать корень дерева 

        ReversiBoard.Move? bestMove = null;

        if (Mode == AiMode.AlphaBeta)
        {
            (double score, MoveRef? moveRef) = AlphaBeta.Search(
                position: _board,
                legalMoves: (pos, side) => pos.LegalMoves(side) // преобразование ходов в тип MoveRef
                    .Select(m => new MoveRef(m.X, m.Y))
                    .ToList(),
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
                legalMoves: (pos, side) => pos.LegalMoves(side)
                    .Select(m => new MoveRef(m.X, m.Y))
                    .ToList(),
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

        ReversiBoard.Move? bestMove = FindBestAiMove(); // вызываем даже если ходов нет и это пас - для MCTS

        if (!_board.HasAnyMoves(_turn))
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
            List<ReversiBoard.Move> moves = simulation.LegalMoves(side);

            if (moves.Count == 0)
            {
                passes++;
                side = ReversiBoard.Opponent(side);
                continue;
            }

            passes = 0;

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