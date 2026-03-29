// То, что нужно во всех файлах проекта

namespace BoardGames;

/// <summary>
/// Доступные игры
/// </summary>
public enum GameKind
{
    Checkers,
    Reversi
}

/// <summary>
/// Доступные алгоритмы ИИ
/// </summary>
public enum AiMode
{
    AlphaBeta,
    MonteCarlo
}

/// <summary>
/// Кодировка для сторон игры: +1 - игрок, который ходит первым, -1 - игрок, который ходит вторым
/// </summary>
public static class Players
{

    /// <summary>
    /// Имя стороны в шашках
    /// </summary>
    public static string CheckersName(int player) => player > 0 ? "БЕЛЫЕ" : "ЧЁРНЫЕ";

    /// <summary>
    /// Имя стороны в реверси
    /// </summary>
    public static string ReversiName(int player) => player > 0 ? "ЧЁРНЫЕ" : "БЕЛЫЕ";
}