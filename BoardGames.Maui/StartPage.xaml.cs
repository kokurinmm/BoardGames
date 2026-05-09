namespace BoardGames;

public partial class StartPage : ContentPage
{
    public StartPage()
    {
        InitializeComponent();

        GamePicker.SelectedIndex = 0;
        AiModePicker.SelectedIndex = 0;

        UpdateDepthMaximum();
        UpdateAiPanels();
    }

    private GameKind SelectedGameKind => GamePicker.SelectedIndex switch // Чтение выбранного типа игры
    {
        0 => GameKind.Checkers,
        1 => GameKind.Reversi,
        2 => GameKind.Corners,
        _ => GameKind.Checkers
    };

    private AiMode SelectedAiMode => AiModePicker.SelectedIndex switch // Чтение выбранного алгоритма ИИ
    {
        0 => AiMode.AlphaBeta,
        1 => AiMode.Mcts,
        _ => AiMode.AlphaBeta
    };

    private void OnGameChanged(object? sender, EventArgs e) // Если выбрана другая игра, изменить max глубину
    {
        UpdateDepthMaximum();
    }

    private void OnAiSwitchToggled(object? sender, ToggledEventArgs e) // Обновить страницу при переключении чего-нибудь
    {
        UpdateAiPanels();
    }

    private void OnAiModeChanged(object? sender, EventArgs e)
    {
        UpdateAiPanels();
    }

    private void UpdateDepthMaximum() // Установить max глубину alpha-beta, чтобы не зависала
    {
        IGameController temp = SelectedGameKind switch
        {
            GameKind.Checkers => new CheckersController(),
            GameKind.Reversi => new ReversiController(),
            GameKind.Corners => new CornersController(),
            _ => new CheckersController()
        };

        DepthStepper.Maximum = temp.MaxDepth;

        if (DepthStepper.Value > temp.MaxDepth)
            DepthStepper.Value = temp.MaxDepth;

        DepthLabel.Text = ((int)DepthStepper.Value).ToString();
    }

    private void UpdateAiPanels() // Что должно быть видимо, а что невидимо на странице
    {
        bool withAi = AiSwitch.IsToggled;
        bool alphaBeta = SelectedAiMode == AiMode.AlphaBeta;

        AiSettingsPanel.IsVisible = withAi;

        DepthPanel.IsVisible = withAi && alphaBeta;
        MctsPanel.IsVisible = withAi && !alphaBeta;
    }

    private void OnDepthChanged(object? sender, ValueChangedEventArgs e)
    {
        DepthLabel.Text = ((int)e.NewValue).ToString();
    }

    private void OnMctsChanged(object? sender, ValueChangedEventArgs e)
    {
        MctsLabel.Text = ((int)e.NewValue).ToString();
    }

    private async void OnStartClicked(object? sender, EventArgs e) // Щелчок на кнопке "Новая игра"
    {
        bool humanVsHuman = !AiSwitch.IsToggled;

        var options = new GameOptions // все выбранные пользователем параметры партии
        {
            Kind = SelectedGameKind,
            HumanVsHuman = humanVsHuman,
            Mode = SelectedAiMode,
            AlphaBetaDepth = (int)DepthStepper.Value,
            MctsTimeLimitMs = (int)MctsStepper.Value
        };

        await Shell.Current.GoToAsync(
            nameof(GamePage), // перейти на страницу GamePage, передать параметры партии
            new Dictionary<string, object>
            {
                ["Options"] = options
            });
    }
}