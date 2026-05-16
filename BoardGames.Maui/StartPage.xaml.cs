using System;

namespace BoardGames;

public partial class StartPage : ContentPage
{

    private bool _settingMctsMs; // устанавливается значение счётчика миллисекунд, не вызывать событие

    public StartPage()
    {
        InitializeComponent();

        GamePicker.SelectedIndex = 0;
        AiModePicker.SelectedIndex = 0;

        ConfigureMctsTimeScale();

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

    /// <summary>
    /// Установить max глубину alpha-beta, чтобы не зависала
    /// </summary>
    private void UpdateDepthMaximum()
    {
        int maxDepth = SelectedGameKind switch
        {
            GameKind.Checkers => 9,
            GameKind.Reversi => 7,
            GameKind.Corners => 5,
            _ => 5
        };

        DepthStepper.Maximum = maxDepth;

        if (DepthStepper.Value > maxDepth)
            DepthStepper.Value = maxDepth;

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
        if (_settingMctsMs) // если изменение счётчика происходит в коде, ничего не делать
            return;
        SetMctsIndex((int)e.NewValue);
    }

    private void ConfigureMctsTimeScale() // Stepper будет управлять индексом неравномерной шкалы
    {
        MctsStepper.Minimum = 0;
        MctsStepper.Maximum = AiParameterScales.MctsMs.Length - 1;
        MctsStepper.Increment = 1;
        SetMctsIndex(AiParameterScales.DefaultMctsIndex);
    }

    private void SetMctsIndex(int index)
    {
        if (index < 0)
            index = 0;
        int max = AiParameterScales.MctsMs.Length - 1;
        if (index > max)
            index = max;

        _settingMctsMs = true; // устанавливаем значение Stepper, вызывать событие не нужно
        MctsStepper.Value = index;
        _settingMctsMs = false;

        MctsLabel.Text = AiParameterScales.MctsMs[index].ToString();
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
            MctsTimeLimitMs = AiParameterScales.MctsMs[(int)MctsStepper.Value]
        };

        // открыть GamePage поверх текущей страницы, не блокировать перерисовку страницы, передать параметры партии
        await Navigation.PushAsync(new GamePage(options));
    }

    private async void OnHelpClicked(object? sender, EventArgs e) // Щелчок на кнопке справки
    {
        await Navigation.PushAsync(new HelpPage());
    }

}