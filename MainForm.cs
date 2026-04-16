using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BoardGames;

public partial class MainForm : Form
{

    private readonly BoardView _boardView = new(); // объект BoardView для рисования доски

    private IGameController _controller = null!; // текущий контроллер игры (зависит от выбранной игры)

    private bool _aiLoopRunning; // выполняется асинхронный цикл (чтобы не запустилось два сразу)

    private bool _playWithoutAi; // игра без ИИ
    
    private bool _syncingUi; // возможно изменение значения на счётчиках из контроллера, которое не должно вызывать событие

    public MainForm()
    {
        InitializeComponent();

        InitializeBoardView(); // подключаем BoardView к панели

        BindUiEvents(); // подписка на события компонентов формы

        _controller = CreateController(); // создаём контроллер для выбранной по умолчанию игры
        ApplyController(); // связываем контролер с BoardView
        LoadDefaultsFromController(); // перенос значений по умолчанию из контролера в форму

        StartNewGame(withAi: true);
    }

    /// <summary>
    /// Запустить новую игру в выбранном режиме (с ИИ или без ИИ)
    /// </summary>
    private void StartNewGame(bool withAi)
    {
        _playWithoutAi = !withAi;

        _controller.HumanVsHuman = _playWithoutAi;
        UpdateAiParamsFromUi();
        _controller.NewGame(); // запуск новой игры

        RefreshUiState();
        _boardView.Refresh();
        _ = MaybeRunAiLoop(); // если игра начинается с хода ИИ
    }

    private void InitializeBoardView()
    {
        _boardView.Dock = DockStyle.Fill; // доска, рисуемая BoardView, должна занимать всю панель целиком 
        pnlBoard.Controls.Add(_boardView); // разместить BoardView на панели pnlBoard
    }

    /// <summary>
    /// Подписка на события формы: добавляем (+=) к событиям обработчики с игнорируемыми параметрами (_, __)
    /// </summary>
    private void BindUiEvents()
    {
        // переключение между играми
        rbCheckers.CheckedChanged += (_, __) =>
        {
            if (rbCheckers.Checked)
                SwitchGame();
        };

        rbReversi.CheckedChanged += (_, __) =>
        {
            if (rbReversi.Checked)
                SwitchGame();
        };

        rbCorners.CheckedChanged += (_, __) =>
        {
            if (rbCorners.Checked)
                SwitchGame();
        };

        // переключение между алгоритмами ИИ запускает новую игру
        rbAlphaBeta.CheckedChanged += (_, __) =>
        {
            if (rbAlphaBeta.Checked)
                StartNewGame(withAi: !_playWithoutAi);
        };

        rbMcts.CheckedChanged += (_, __) =>
        {
            if (rbMcts.Checked)
                StartNewGame(withAi: !_playWithoutAi);
        };

        // изменение числовых параметров
        nudDepth.ValueChanged += (_, __) =>
        {
            if (_syncingUi)
                return;
            _controller.AlphaBetaDepth = (int)nudDepth.Value;
        };

        nudMctsMs.ValueChanged += (_, __) =>
        {
            if (_syncingUi)
                return;
            _controller.MctsTimeLimitMs = (int)nudMctsMs.Value;
        };

        // щелчок на кнопке новой игры
        btnNewGame.Click += (_, __) => StartNewGame(withAi: true);

        // щелчок на кнопке игры без ИИ
        btnNoAiGame.Click += (_, __) => StartNewGame(withAi: false);
    }

    /// <summary>
    /// Создать контроллер для текущей выбранной игры
    /// </summary>
    private IGameController CreateController()
    {
        GameKind kind;

        if (rbCheckers.Checked)
            kind = GameKind.Checkers;
        else if (rbReversi.Checked)
            kind = GameKind.Reversi;
        else if (rbCorners.Checked)
            kind = GameKind.Corners;
        else
            kind = GameKind.Checkers;

        return kind switch
        {
            GameKind.Checkers => new CheckersController(),
            GameKind.Reversi => new ReversiController(),
            GameKind.Corners => new CornersController(),
            _ => new CheckersController()
        };
    }

    /// <summary>
    /// Связать текущий контроллер игры с контролом BoardView
    /// </summary>
    private void ApplyController()
    {
        _boardView.BoardSize = _controller.BoardSize;

        // как рисовать доску
        _boardView.DrawCallback = (g, rect) => _controller.Draw(g, rect);

        // что делать при щелчке по клетке
        _boardView.CellClick = (row, col) =>
        {
            if (_aiLoopRunning) // во время хода ИИ щелчки не обрабатываем
                return;

            _controller.HandleCellClick(row, col);
            RefreshUiState();
            _boardView.Refresh();
            lblStatus.Refresh(); // обновляем цвет надписи
            _ = MaybeRunAiLoop();
        };
    }

    /// <summary>
    /// Перенести значения параметров ИИ из контроллера в форму
    /// </summary>
    private void LoadDefaultsFromController()
    {
        _syncingUi = true; // будем изменять значения в счётчиках, события вызывать не нужно
        try
        {
            nudDepth.Value = Math.Clamp(
                _controller.AlphaBetaDepth,
                (int)nudDepth.Minimum,
                (int)nudDepth.Maximum);

            nudMctsMs.Value = Math.Clamp(
                _controller.MctsTimeLimitMs,
                (int)nudMctsMs.Minimum,
                (int)nudMctsMs.Maximum);
        }
        finally
        {
            _syncingUi = false; // обработку изменения значений счётчиков обязательно нужно вернуть
        }
    }

    /// <summary>
    /// Перенести актуальные значения параметров ИИ из формы в контроллер
    /// </summary>
    private void UpdateAiParamsFromUi()
    {
        _controller.Mode = rbAlphaBeta.Checked ? AiMode.AlphaBeta : AiMode.Mcts;
        _controller.AlphaBetaDepth = (int)nudDepth.Value;
        _controller.MctsTimeLimitMs = (int)nudMctsMs.Value;
    }

    /// <summary>
    /// Обновить форму и прочитать параметры ИИ из неё
    /// </summary>
    private void RefreshUiState()
    {
        UpdateAiParamsFromUi();
        Text = $"{_controller.GameDisplayName} — Белые: {_controller.WhitePieceCount}, Чёрные: {_controller.BlackPieceCount}";

        groupBox2.Enabled = !_controller.HumanVsHuman;

        if (!_controller.HumanVsHuman)
        {
            nudDepth.Enabled = _controller.Mode == AiMode.AlphaBeta;
            label1.Enabled = _controller.Mode == AiMode.AlphaBeta;
            nudMctsMs.Enabled = _controller.Mode == AiMode.Mcts;
            label3.Enabled = _controller.Mode == AiMode.Mcts;
        }

        if (_controller.IsGameOver)
        {
            lblStatus.Text = _controller.GameOverMessage ?? "Игра окончена";
            lblStatus.ForeColor = Color.DarkGreen;
            return;
        }

        if (_controller.HumanVsHuman)
        {
            lblStatus.Text = "Ход: " + _controller.CurrentTurnDisplayName;
            lblStatus.ForeColor = Color.DarkBlue;
            return;
        }

        lblStatus.Text = "Вы: " + _controller.HumanPlayerDisplayName;
        lblStatus.ForeColor = _controller.IsAiTurn ? Color.Black : Color.Crimson;
       
    }

    /// <summary>
    /// Переключение между играми
    /// </summary>
    private void SwitchGame()
    {
        _controller = CreateController();
        ApplyController();
        LoadDefaultsFromController();
        StartNewGame(withAi: !_playWithoutAi);
    }

    /// <summary>
    /// Асинхронный цикл хода ИИ, с небольшой задержкой без блокирования окна. Может быть несколько ходов подряд
    /// </summary>
    private async Task MaybeRunAiLoop()
    {
        if (_aiLoopRunning)
            return;

        _aiLoopRunning = true;
        RefreshUiState();
        lblStatus.Refresh();

        try
        {
            while (_controller.IsAiTurn && !_controller.IsGameOver)
            {
                await Task.Yield(); // пусть графический интерфейс обновит доску и не блокируется
                bool changed = _controller.BeginAiTurnAnimation();

                RefreshUiState();
                _boardView.Refresh();

                if (!changed)
                    break;

                // Если у подготовленного хода есть визуальные шаги, применяем их по одному с задержками
                while (_controller.HasPendingAiAnimation && !_controller.IsGameOver)
                {
                    await Task.Delay(400);

                    bool stepChanged = _controller.ApplyNextAiAnimationStep();

                    RefreshUiState();
                    _boardView.Refresh();

                    if (!stepChanged)
                        break;
                }

                // Если после этого ИИ должен ходить ещё раз подряд, сделать паузу между полными ходами
                if (_controller.IsAiTurn && !_controller.IsGameOver)
                    await Task.Delay(400);
            }
            if (_controller.IsGameOver)
            {
                ShowGameOverMessage();
                return;
            }
        }
        finally
        {
            _aiLoopRunning = false;
            RefreshUiState();
        }
    }

    /// <summary>
    /// Показать окно об окончании игры
    /// </summary>
    private void ShowGameOverMessage()
    {
        MessageBox.Show(
            _controller.GameOverMessage,
            "Конец игры",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

}