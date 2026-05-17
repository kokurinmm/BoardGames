namespace BoardGames;

// Код для основной игровой страницы с доской

public partial class GamePage : ContentPage
{
    private readonly BoardDrawable _drawable = new();

    private IGameController? _controller; // контроллер конкретной игры
    private GameOptions? _options; // набор задаваемых пользователем параметров

    private bool _aiLoopRunning;

    public GamePage()
    {
        InitializeComponent(); // загрузка интерфейса из XAML

        BoardGraphicsView.Drawable = _drawable; // объект IDrawable для отрисовки доски

        var tap = new TapGestureRecognizer(); // для обработки нажатий пальцем
        tap.Tapped += OnBoardTapped;
        BoardGraphicsView.GestureRecognizers.Add(tap);
    }

    public GamePage(GameOptions options) // конструктор с параметрами - начать новую игру с заданными на StartPage параметрами
        : this()
    {
        _options = options;
        StartGame(options);
    }

    private void StartGame(GameOptions options) // Начало новой партии
    {

        _controller = options.Kind switch
        {
            GameKind.Checkers => new CheckersController(),
            GameKind.Reversi => new ReversiController(),
            GameKind.Corners => new CornersController(),
            _ => new CheckersController()
        };

        _controller.HumanVsHuman = options.HumanVsHuman;
        _controller.Mode = options.Mode;
        _controller.AlphaBetaDepth = Math.Min(options.AlphaBetaDepth, _controller.MaxDepth);
        _controller.MctsTimeLimitMs = options.MctsTimeLimitMs;

        _controller.NewGame();

        _drawable.Controller = _controller;

        RefreshUiState();
        BoardGraphicsView.Invalidate();

        _ = StartAiLoopAfterRenderAsync();
    }

    private async void OnBoardTapped(object? sender, TappedEventArgs e) // Обработка нажатия пальцем
    {
        if (_controller is null)
            return;

        if (_aiLoopRunning || _controller.IsGameOver)
            return;

        Point? p = e.GetPosition(BoardGraphicsView);
        if (p is null)
            return;

        (int row, int col)? cell = _drawable.HitTest(p.Value.X, p.Value.Y);
        if (cell is null)
            return;

        _controller.HandleCellClick(cell.Value.row, cell.Value.col); // главная часть обработки здесь

        RefreshUiState();
        BoardGraphicsView.Invalidate(); // надо перерисовать доску

        await MaybeCompleteCheckersCaptureCleanupAsync(); // задержка после цепочки взятий в шашках
        await MaybeCompleteHumanVsHumanTurnAsync(); // задержка перед передачей хода в режиме без ИИ в шашках
        await MaybeRunAiLoopAsync();
    }

    /// <summary>
    /// Делает задержку при старте новой игры, чтобы доска успела нарисоваться, а затем запускает цикл хода ИИ
    /// </summary>
    /// <returns></returns>
    private async Task StartAiLoopAfterRenderAsync()
    {
        await Task.Delay(200);

        if (_controller is null)
            return;

        if (!_controller.IsAiTurn)
            return;

        await MaybeRunAiLoopAsync();
    }

    /// <summary>
    /// Асинхронный цикл хода ИИ, с небольшой задержкой без блокирования окна. Может быть несколько ходов подряд
    /// </summary>
    private async Task MaybeRunAiLoopAsync()
    {
        if (_controller is null)
            return;

        if (_aiLoopRunning)
            return;

        _aiLoopRunning = true;

        RefreshUiState();

        try
        {
            while (_controller.IsAiTurn && !_controller.IsGameOver)
            {
                await Task.Delay(100); // пусть графический интерфейс обновит доску и не блокируется

                bool changed = _controller.BeginAiTurnAnimation();

                RefreshUiState();
                BoardGraphicsView.Invalidate();

                if (!changed)
                    break;

                bool firstAiStep = true;

                // Если у подготовленного хода есть визуальные шаги, применяем их по одному с задержками
                while (_controller.HasPendingAiAnimation && !_controller.IsGameOver)
                {
                    await Task.Delay(firstAiStep ? 100 : 500);

                    bool stepChanged = _controller.ApplyNextAiAnimationStep();

                    RefreshUiState();
                    BoardGraphicsView.Invalidate();

                    if (!stepChanged)
                        break;

                    firstAiStep = false;
                }

                await MaybeCompleteCheckersCaptureCleanupAsync(); // если нужно, пауза в конце цепочки шашечных взятий

                // Если после этого ИИ должен ходить ещё раз подряд, сделать паузу между полными ходами
                if (_controller.IsAiTurn && !_controller.IsGameOver)
                    await Task.Delay(350);
            }

            if (_controller.IsGameOver)
                await DisplayAlert("Конец игры", _controller.GameOverMessage ?? "Игра окончена", "OK");
        }
        finally
        {
            _aiLoopRunning = false;
            RefreshUiState();
        }
    }

    /// <summary>
    /// Если нужно, сделать задержку в конце цепочки взятий в шашках (когда все взятые фигуры показаны перечёркнутыми)
    /// </summary>
    private async Task MaybeCompleteCheckersCaptureCleanupAsync()
    {
        if (_controller is not CheckersController checkers)
            return;

        if (!checkers.PendingCapturedPiecesCleanup)
            return;

        await Task.Delay(350);

        checkers.CompleteCapturedPiecesCleanup();

        RefreshUiState();
        BoardGraphicsView.Invalidate();
    }

    /// <summary>
    /// Если нужно, сделать задержку, а затем вызвать функцию завершения хода в контроллере для режима игры без ИИ
    /// Актуально для игр типа шашек, с переворачивающейся доской в зависимости от того, чей ход
    /// </summary>
    private async Task MaybeCompleteHumanVsHumanTurnAsync()
    {
        if (_controller is not CheckersController checkers)
            return;

        if (!checkers.HumanVsHuman || !checkers.PendingHumanVsHumanTurn)
            return;

        await Task.Delay(500);

        checkers.CompleteHumanVsHumanTurn();

        RefreshUiState();
        BoardGraphicsView.Invalidate();
    }

    /// <summary>
    /// Обновить текстовые метки на странице
    /// </summary>
    private void RefreshUiState()
    {
        if (_controller is null)
            return;

        if(_options is not null && _options.Kind == GameKind.Reversi)
            Title =
                $"{_controller.GameDisplayName} — " +
                $"{_controller.BlackPieceCount} : {_controller.WhitePieceCount} — " +
                $"ход {_controller.CurrentFullMoveNumber}";
        else
            Title =
                $"{_controller.GameDisplayName} — " +
                $"{_controller.WhitePieceCount} : {_controller.BlackPieceCount} — " +
                $"ход {_controller.CurrentFullMoveNumber}";

        if (_controller.IsGameOver)
        {
            StatusLabel.Text = _controller.GameOverMessage ?? "Игра окончена";
            StatusLabel.TextColor = Colors.DarkGreen;
            return;
        }

        if (_controller.HumanVsHuman)
        {
            StatusLabel.Text = "Ход: " + _controller.CurrentTurnDisplayName;
            StatusLabel.TextColor = Colors.DarkBlue;
            return;
        }

        StatusLabel.Text = "Вы: " + _controller.HumanPlayerDisplayName;
        StatusLabel.TextColor = _controller.IsAiTurn ? Colors.Black : Colors.Crimson;
    }

    private void OnNewGameClicked(object? sender, EventArgs e) // щелчок на кнопке "Новая игра"
    {
        if (_options is not null)
            StartGame(_options);
    }

    private async void OnBackClicked(object? sender, EventArgs e) // щелчок на кнопке "Назад"
    {
        await Navigation.PopAsync(); // перейти на предыдущую страницу
    }

    private void OnBoardHostSizeChanged(object? sender, EventArgs e) // если изменился размер доски
    {
        if (BoardHost.Width <= 0 || BoardHost.Height <= 0)
            return;

        double availableWidth = Math.Max(
            0,
            BoardHost.Width - BoardHost.Padding.HorizontalThickness - 2.0 * BoardHost.StrokeThickness);

        double availableHeight = Math.Max(
            0,
            BoardHost.Height - BoardHost.Padding.VerticalThickness - 2.0 * BoardHost.StrokeThickness);

        // доска должна быть квадратной, максимально доступного размера
        double side = Math.Min(availableWidth, availableHeight);

        BoardGraphicsView.WidthRequest = side;
        BoardGraphicsView.HeightRequest = side;
    }
}