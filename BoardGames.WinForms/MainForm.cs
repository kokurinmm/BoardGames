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

        InitializeBoardView(); // подключаем BoardView к панели

        BindUiEvents(); // подписка на события компонентов формы

        _controller = CreateController(); // создаём контроллер для выбранной по умолчанию игры
        ApplyController(); // связываем контролер с BoardView
        nudDepth.Maximum = _controller.MaxDepth; // максимальная глубина поиска для выбранной игры, чтобы не зависала

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
            _controller.AlphaBetaDepth = (int)nudDepth.Value;
        };

        nudMctsMs.ValueChanged += (_, __) =>
        {
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
        _boardView.DrawCallback = (canvas, rect) => _controller.Draw(canvas, rect);

        // что делать при щелчке по клетке
        _boardView.CellClick = (row, col) =>
        {
            if (_aiLoopRunning || _controller.IsGameOver) // во время хода ИИ и после конца игры щелчки не обрабатываем
                return;

            _controller.HandleCellClick(row, col);
            RefreshUiState();
            _boardView.Refresh();
            lblStatus.Refresh(); // обновляем цвет надписи
            _ = MaybeRunAiLoop();
        };
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
        nudDepth.Maximum = _controller.MaxDepth; // максимальная глубина поиска для выбранной игры, чтобы не зависала
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

                bool firstAiStep = true;

                // Если у подготовленного хода есть визуальные шаги, применяем их по одному с задержками
                while (_controller.HasPendingAiAnimation && !_controller.IsGameOver)
                {
                    if (firstAiStep)
                        await Task.Delay(100); // если это первый шаг в цепочке ходов ИИ, сделаем задержку поменьше
                    else
                        await Task.Delay(500);

                    bool stepChanged = _controller.ApplyNextAiAnimationStep();

                    RefreshUiState();
                    _boardView.Refresh();

                    if (!stepChanged)
                        break;

                    firstAiStep = false;
                }

                // Если после этого ИИ должен ходить ещё раз подряд, сделать паузу между полными ходами
                if (_controller.IsAiTurn && !_controller.IsGameOver)
                    await Task.Delay(350);
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

    private void btnHelp_Click(object sender, EventArgs e)
    {
        MessageBox.Show(
                "Игра в шашки, реверси и уголки с ИИ\n\n" +
                "Основные правила широко известны.\nОбратите внимание: в шашках после 15 ходов только дамками без взятий объявляется ничья.\n" +
                "В уголках вы должны освободить свой дом за свои первые 40 ходов, а после 80 пар ходов игра завершится.\n" +
                "Но обычно всё происходит гораздо быстрее.\n" +
                "Если белые уже провели свои фишки в дом соперника, чёрным даётся один дополнительный ход, " +
                "чтобы успеть сделать то же самое. Играя за чёрных, не увлекайтесь копированием ходов белых слишком долго.\n\n" +
                "Против вас будут играть два алгоритма ИИ.\n" +
                "Алгоритм альфа-бета отсечения просчитывает ходы на заданную глубину. " +
                "Алгоритм Monte Carlo Tree Search\nна протяжении всей партии " +
                "строит дерево ходов с их оценками, запускает случайные доигрывания партий из разных позиций " +
                "и глубоко исследует перспективные ветви. В шашках доигрывания неизученных позиций полностью случайные, " +
                "в реверси и уголках - не совсем.\n\nЗа какой цвет вы играете - определяется случайно.\nПереключение алгоритма ИИ перезапускает игру.\n" +
                "Но изменить уровень сложности можно и на ходу в процессе игры. " +
                "А если вы хотите поиграть сами с собой - нажмите кнопку \"Играть без ИИ\".\n\nВот и всё, интересных вам игр!",
                "Справка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
    }
}