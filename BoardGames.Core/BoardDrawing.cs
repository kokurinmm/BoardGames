namespace BoardGames;

// Кроссплатформенная графическая абстракция - прослойка между контроллерами и графикой на конкретных платформах

// Прямоугольник доски
public readonly record struct BoardRect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top + Height;
}

// Структура для цвета
public readonly record struct GameColor(float R, float G, float B);

// Все используемые в игре цвета - чтобы не зависеть от System.Drawing, или вообще от конкретной платформы 
public static class GameColors
{
    public static readonly GameColor White = Rgb(255, 255, 255);
    public static readonly GameColor Black = Rgb(0, 0, 0);
    public static readonly GameColor Gray = Rgb(128, 128, 128);
    public static readonly GameColor DimGray = Rgb(105, 105, 105);

    public static readonly GameColor Blue = Rgb(0, 0, 255);
    public static readonly GameColor DarkBlue = Rgb(0, 0, 139);
    public static readonly GameColor DodgerBlue = Rgb(30, 144, 255);
    public static readonly GameColor SteelBlue = Rgb(70, 130, 180);
    public static readonly GameColor LightSteelBlue = Rgb(176, 196, 222);
    public static readonly GameColor AliceBlue = Rgb(240, 248, 255);

    public static readonly GameColor Green = Rgb(0, 128, 0);
    public static readonly GameColor LimeGreen = Rgb(50, 205, 50);
    public static readonly GameColor DarkGreen = Rgb(0, 100, 0);

    public static readonly GameColor Yellow = Rgb(255, 255, 0);
    public static readonly GameColor Gold = Rgb(255, 215, 0);
    public static readonly GameColor Goldenrod = Rgb(218, 165, 32);

    public static readonly GameColor Crimson = Rgb(220, 20, 60);
    public static readonly GameColor Firebrick = Rgb(178, 34, 34);
    public static readonly GameColor MediumVioletRed = Rgb(199, 21, 133);

    public static readonly GameColor Peru = Rgb(205, 133, 63);
    public static readonly GameColor PeachPuff = Rgb(255, 218, 185);

    public static readonly GameColor LemonChiffon = Rgb(255, 250, 205);
    public static readonly GameColor Khaki = Rgb(240, 230, 140);
    public static readonly GameColor Wheat = Rgb(245, 222, 179);
    public static readonly GameColor BurlyWood = Rgb(222, 184, 135);
    public static readonly GameColor SaddleBrown = Rgb(139, 69, 19);

    private static GameColor Rgb(byte r, byte g, byte b) =>
        new(r / 255f, g / 255f, b / 255f);
}

/// <summary>
/// Интерфейс для методов рисования
/// </summary>
public interface IBoardCanvas
{
    void FillRectangle(GameColor color, float x, float y, float width, float height);
    void DrawRectangle(GameColor color, float strokeSize, float x, float y, float width, float height);

    void FillEllipse(GameColor color, float x, float y, float width, float height);
    void DrawEllipse(GameColor color, float strokeSize, float x, float y, float width, float height);

    void DrawLine(GameColor color, float strokeSize, float x1, float y1, float x2, float y2);
}
