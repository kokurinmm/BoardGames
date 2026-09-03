// То, что нужно во всех файлах проекта

namespace BoardGames;

/// <summary>
/// Доступные игры
/// </summary>
public enum GameKind
{
    Checkers,
    Reversi,
    Corners
}

/// <summary>
/// Доступные алгоритмы ИИ
/// </summary>
public enum AiMode
{
    AlphaBeta,
    Mcts
}

/// <summary>
/// Вспомогательный класс для отображения имени стороны игры
/// Кодировка для сторон игры: +1 - игрок, который ходит первым, -1 - игрок, который ходит вторым
/// </summary>
public static class Players
{
    public static string CheckersName(int player) => player > 0 ? "БЕЛЫЕ" : "ЧЁРНЫЕ";
    public static string ReversiName(int player) => player > 0 ? "ЧЁРНЫЕ" : "БЕЛЫЕ";
    public static string CornersName(int player) => player > 0 ? "БЕЛЫЕ" : "ЧЁРНЫЕ";
}

/// <summary>
/// Шкала количества миллисекунд для MCTS
/// </summary>
public static class AiParameterScales
{
    public static readonly int[] MctsMs =
    {
        1, 2, 3, 5, 7, 10, 15, 20, 30, 50, 70, 100, 150, 200, 300, 500, 700, 1000, 1500, 2000, 3000, 5000, 7000, 10000
    };

    public const int DefaultMctsIndex = 14; // индекс значения по умолчанию в шкале
}