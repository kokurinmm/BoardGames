namespace BoardGames;

public sealed class GameOptions // Класс для параметров партии
{
    public GameKind Kind { get; init; } // какая именно игра
    public bool HumanVsHuman { get; init; } // режим без ИИ
    public AiMode Mode { get; init; } // алгоритм ИИ
    public int AlphaBetaDepth { get; init; } // глубина в алгоритме альфа-бета
    public int MctsTimeLimitMs { get; init; } // количество миллисекунд для размышления в MCTS
}