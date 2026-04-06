using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BoardGames;

public partial class MainForm : Form
{

    private const bool USE_TEST_CONTROLLER = false; // временно использовать тестовый контролер вместо полноценного

    private readonly BoardView _boardView = new(); // объект BoardView для рисования доски

    private IGameController _controller = null!; // текущий контроллер игры (зависит от выбранной игры)

    public MainForm()
    {
        InitializeComponent();

        _controller = CreateControllerForSelectedGame(); // создаём контроллер для выбранной по умолчанию игры
        
        InitializeBoardView(); // подключаем BoardView к панели

        BindUiEvents(); // подписка на события компонентов формы

        ApplyControllerToBoardView(); // связываем контролер с BoardView

        LoadDefaultsFromController(); // перенос значений по умолчанию из контролера в форму

        _controller.NewGame(); // запуск новой игры

        UpdateStatusAndParams(); // обновление надписи о цвете игрока

        _ = MaybeRunAiLoopAsync(); // если игра начинается с хода ИИ
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

        // переключение между алгоритмами ИИ.
        rbAlphaBeta.CheckedChanged += (_, __) =>
        {
            if (rbAlphaBeta.Checked)
            {
                _controller.Mode = AiMode.AlphaBeta;
                UpdateStatusAndParams();
            }
        };

        rbMonteCarlo.CheckedChanged += (_, __) =>
        {
            if (rbMonteCarlo.Checked)
            {
                _controller.Mode = AiMode.MonteCarlo;
                UpdateStatusAndParams();
            }
        };

        // изменение числовых параметров
        nudDepth.ValueChanged += (_, __) =>
        {
            UpdateStatusAndParams();
        };

        nudSims.ValueChanged += (_, __) =>
        {
            UpdateStatusAndParams();
        };

        // щелчок на кнопке новой игры
        btnNewGame.Click += (_, __) =>
        {
            _controller.NewGame();
            UpdateStatusAndParams();
            _boardView.Invalidate(); // доска требует перерисовки, но не срочно
            _ = MaybeRunAiLoopAsync(); // если очередь хода ИИ
        };
    }

    /// <summary>
    /// Создать контроллер для текущей выбранной игры
    /// </summary>
    private IGameController CreateControllerForSelectedGame()
    {
        GameKind kind = rbCheckers.Checked ? GameKind.Checkers : GameKind.Reversi;

        if (USE_TEST_CONTROLLER)
            return new TestController(kind);

        return kind == GameKind.Checkers
            ? new CheckersController()
            : new ReversiController();
    }

    /// <summary>
    /// Связать текущий контроллер игры с контролом BoardView
    /// </summary>
    private void ApplyControllerToBoardView()
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
            _boardView.Invalidate();
            _ = MaybeRunAiLoopAsync();
        };
    }

    /// <summary>
    /// Перенести значения параметров ИИ из контроллера в форму
    /// </summary>
    private void LoadDefaultsFromController()
    {
        rbAlphaBeta.Checked = _controller.Mode == AiMode.AlphaBeta;
        rbMonteCarlo.Checked = _controller.Mode == AiMode.MonteCarlo;

        nudDepth.Value = Math.Clamp(
            _controller.AlphaBetaDepth,
            (int)nudDepth.Minimum,
            (int)nudDepth.Maximum);

        nudSims.Value = Math.Clamp(
            _controller.MonteCarloSimulations,
            (int)nudSims.Minimum,
            (int)nudSims.Maximum);
    }

    /// <summary>
    /// Перенести актуальные значения параметров ИИ из формы в контроллер
    /// </summary>
    private void UpdateAiParamsFromUi()
    {
        _controller.AlphaBetaDepth = (int)nudDepth.Value;
        _controller.MonteCarloSimulations = (int)nudSims.Value;
    }

    /// <summary>
    /// Прочитать числовые параметры формы и обновить подпись, за какую сторону играет пользователь
    /// </summary>
    private void UpdateStatusAndParams()
    {
        UpdateAiParamsFromUi();
        lblStatus.Text = "Вы: " + _controller.HumanPlayerDisplayName;
    }

    /// <summary>
    /// Переключение между играми
    /// </summary>
    private void SwitchGame()
    {
        _controller = CreateControllerForSelectedGame();
        ApplyControllerToBoardView();
        LoadDefaultsFromController();
        _controller.NewGame();
        UpdateStatusAndParams();
        _boardView.Invalidate();
        _ = MaybeRunAiLoopAsync();
    }

    /// <summary>
    /// Асинхронный цикл хода ИИ, с небольшой задержкой без блокирования окна. Может быть несколько ходов подряд
    /// </summary>
    private async Task MaybeRunAiLoopAsync()
    {
        if (_controller.IsGameOver)
            return;

        while (_controller.IsAiTurn && !_controller.IsGameOver)
        {
            await Task.Delay(400);

            bool changed = _controller.MakeAiTurn();
            UpdateStatusAndParams();
            _boardView.Invalidate();

            if (!changed)
                break;
        }
    }

}