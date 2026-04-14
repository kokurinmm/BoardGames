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

    public MainForm()
    {
        InitializeComponent();

        _controller = CreateController(); // создаём контроллер для выбранной по умолчанию игры

        _controller.HumanVsHuman = _playWithoutAi; // по умолчанию false, так что первая игра будет с ИИ

        InitializeBoardView(); // подключаем BoardView к панели

        BindUiEvents(); // подписка на события компонентов формы

        ApplyController(); // связываем контролер с BoardView

        LoadDefaultsFromController(); // перенос значений по умолчанию из контролера в форму

        _controller.NewGame(); // запуск новой игры

        UpdateStatusAndParams(); // обновление надписи о цвете игрока

        _ = MaybeRunAiLoop(); // если игра начинается с хода ИИ
    }

    /// <summary>
    /// Запустить новую игру в выбранном режиме (с ИИ или без ИИ)
    /// </summary>
    private void StartNewGame(bool withAi)
    {
        _playWithoutAi = !withAi;

        _controller.HumanVsHuman = _playWithoutAi;
        _controller.NewGame();

        UpdateStatusAndParams();
        _boardView.Refresh();
        _ = MaybeRunAiLoop();
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

        // переключение между алгоритмами ИИ
        rbAlphaBeta.CheckedChanged += (_, __) =>
        {
            if (rbAlphaBeta.Checked)
            {
                _controller.Mode = AiMode.AlphaBeta;
                UpdateStatusAndParams();
            }
        };

        //rbMonteCarlo.CheckedChanged += (_, __) => // упрощенный монте-карло, удалён из текущей версии программы
        //{
        //    if (rbMonteCarlo.Checked)
        //    {
        //        _controller.Mode = AiMode.MonteCarlo;
        //        UpdateStatusAndParams();
        //    }
        //};

        rbMcts.CheckedChanged += (_, __) =>
        {
            if (rbMcts.Checked)
            {
                _controller.Mode = AiMode.Mcts;
                UpdateStatusAndParams();
            }
        };

        // изменение числовых параметров
        nudDepth.ValueChanged += (_, __) =>
        {
            _controller.Mode = AiMode.AlphaBeta;
            UpdateStatusAndParams();
        };

        //nudSims.ValueChanged += (_, __) =>
        //{
        //    _controller.Mode = AiMode.MonteCarlo;
        //    UpdateStatusAndParams();
        //};

        nudMctsMs.ValueChanged += (_, __) =>
        {
            _controller.Mode = AiMode.Mcts;
            UpdateStatusAndParams();
        };

        // щелчок на кнопке новой игры
        btnNewGame.Click += (_, __) =>
        {
            StartNewGame(withAi: true);
        };

        // щелчок на кнопке игры без ИИ
        btnNoAiGame.Click += (_, __) =>
        {
            StartNewGame(withAi: false);
        };
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
        _boardView.DrawCallback = (g, rect) =>
        {
            _controller.Draw(g, rect);
        };

        // что делать при щелчке по клетке
        _boardView.CellClick = (row, col) =>
        {
            _controller.HandleCellClick(row, col);
            UpdateStatusAndParams();
            _boardView.Refresh();
            _ = MaybeRunAiLoop();
        };
    }

    /// <summary>
    /// Перенести значения параметров ИИ из контроллера в форму
    /// </summary>
    private void LoadDefaultsFromController()
    {
        rbAlphaBeta.Checked = _controller.Mode == AiMode.AlphaBeta;
        //rbMonteCarlo.Checked = _controller.Mode == AiMode.MonteCarlo;
        rbMcts.Checked = _controller.Mode == AiMode.Mcts;

        nudDepth.Value = Math.Clamp(
            _controller.AlphaBetaDepth,
            (int)nudDepth.Minimum,
            (int)nudDepth.Maximum);

        //nudSims.Value = Math.Clamp(
        //    _controller.MonteCarloSimulations,
        //    (int)nudSims.Minimum,
        //    (int)nudSims.Maximum);

        nudMctsMs.Value = Math.Clamp(
            _controller.MctsTimeLimitMs,
            (int)nudMctsMs.Minimum,
            (int)nudMctsMs.Maximum);
    }

    /// <summary>
    /// Перенести актуальные значения параметров ИИ из формы в контроллер
    /// </summary>
    private void UpdateAiParamsFromUi()
    {
        _controller.AlphaBetaDepth = (int)nudDepth.Value;
        //_controller.MonteCarloSimulations = (int)nudSims.Value;
        _controller.MctsTimeLimitMs = (int)nudMctsMs.Value;
    }

    /// <summary>
    /// Прочитать числовые параметры формы и обновить подпись, за какую сторону играет пользователь
    /// </summary>
    private void UpdateStatusAndParams()
    {
        UpdateAiParamsFromUi();
        rbAlphaBeta.Checked = _controller.Mode == AiMode.AlphaBeta;
        //rbMonteCarlo.Checked = _controller.Mode == AiMode.MonteCarlo;
        rbMcts.Checked = _controller.Mode == AiMode.Mcts;

        if (_controller.HumanVsHuman)
        {
            if (!_controller.IsGameOver)
                lblStatus.Text = "Ход: " + _controller.CurrentTurnDisplayName;
        }
        else
            lblStatus.Text = "Вы: " + _controller.HumanPlayerDisplayName;

        Text = $"{_controller.GameDisplayName} — Белые: {_controller.WhitePieceCount}, Чёрные: {_controller.BlackPieceCount}";

        groupBox2.Enabled = !_controller.HumanVsHuman; // заблокировать переключатели ИИ, если игра без ИИ

    }

    /// <summary>
    /// Переключение между играми
    /// </summary>
    private void SwitchGame()
    {
        _controller = CreateController();
        _controller.HumanVsHuman = _playWithoutAi;
        ApplyController();
        LoadDefaultsFromController();
        _controller.NewGame();
        UpdateStatusAndParams();
        _boardView.Refresh();
        _ = MaybeRunAiLoop();
    }

    /// <summary>
    /// Асинхронный цикл хода ИИ, с небольшой задержкой без блокирования окна. Может быть несколько ходов подряд
    /// </summary>
    private async Task MaybeRunAiLoop()
    {
        if (_aiLoopRunning)
            return;

        _aiLoopRunning = true;
        try
        {
            while (_controller.IsAiTurn && !_controller.IsGameOver)
            {
                await Task.Yield(); // пусть графический интерфейс обновит доску и не блокируется
                bool changed = _controller.BeginAiTurnAnimation();

                UpdateStatusAndParams();
                _boardView.Refresh();

                if (!changed)
                    break;

                // Если у подготовленного хода есть визуальные шаги, применяем их по одному с задержками
                while (_controller.HasPendingAiAnimation && !_controller.IsGameOver)
                {
                    await Task.Delay(400);

                    bool stepChanged = _controller.ApplyNextAiAnimationStep();

                    UpdateStatusAndParams();
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