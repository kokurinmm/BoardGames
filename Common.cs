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
    MonteCarlo
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