using System.Drawing;

namespace BoardGames;

public interface IGameController // Общий интерфейс для всех доступных игр
{
    GameKind Kind { get; } // какая выбрана игра
    int BoardSize { get; } // размер доски

    AiMode Mode { get; set; } // алгоритм ИИ
    int AlphaBetaDepth { get; set; } // глубина для алгоритма альфа-бета отсечения
    int MonteCarloSimulations { get; set; } // количество симуляций для метода Монте-Карло

    void NewGame(); // начать новую игру
    void Draw(Graphics g, Rectangle boardRect); // нарисовать доску
    void HandleCellClick(int row, int col); // обработка щелчка на поле доски

    /// <summary>
    /// Если сейчас ход ИИ, выполнить его ход и вернуть true, если состояние игры действительно изменилось
    /// </summary>
    bool MakeAiTurn();

    bool IsGameOver { get; } // окончена ли игра
    bool IsAiTurn { get; } // принадлежит ли ИИ очередь хода

    string HumanPlayerDisplayName { get; } // за какую сторону играет пользователь
}