using System.Drawing;

namespace BoardGames;

public interface IGameController // Общий интерфейс для всех доступных игр
{
    GameKind Kind { get; } // какая выбрана игра
    int BoardSize { get; } // размер доски

    string GameDisplayName { get; } // имя игры для заголовка формы
    int WhitePieceCount { get; } // количество белых фишек для заголовка формы
    int BlackPieceCount { get; } // количество чёрных фишек для заголовка формы

    AiMode Mode { get; set; } // алгоритм ИИ
    int AlphaBetaDepth { get; set; } // глубина для алгоритма альфа-бета отсечения
    int MonteCarloSimulations { get; set; } // количество симуляций для метода Монте-Карло
    int MctsTimeLimitMs { get; set; } // выделенное время на размышления для MCTS

    bool HumanVsHuman { get; set; } // игра без ИИ

    void NewGame(); // начать новую игру
    void Draw(Graphics g, Rectangle boardRect); // нарисовать доску
    void HandleCellClick(int row, int col); // обработка щелчка на поле доски

    /// <summary>
    /// Если сейчас ход ИИ, выполнить его ход и вернуть true, если состояние игры действительно изменилось
    /// </summary>
    bool MakeAiTurn();

    /// <summary>
    /// Подготовить ход ИИ (найти лучший ход или цепочку ходов)
    /// </summary>
    bool BeginAiTurnAnimation();

    /// <summary>
    /// Применить следующий визуальный шаг цепочки ходов ИИ
    /// </summary>
    bool ApplyNextAiAnimationStep();

    bool HasPendingAiAnimation { get; } // есть ли у текущей цепочки ходов ещё не выполненные визуальные шаги

    bool IsGameOver { get; } // окончена ли игра
    bool IsAiTurn { get; } // принадлежит ли ИИ очередь хода

    string HumanPlayerDisplayName { get; } // за какую сторону играет пользователь (строка для отображения)
    string CurrentTurnDisplayName { get; } // кому принадлежит очередь хода (строка для отображения)

    string? GameOverMessage { get; } // текст сообщения об окончании и итогах игры (null, пока игра не закончена)
}