using Microsoft.Maui.Graphics;

namespace BoardGames;

/// <summary>
/// Класс для рисования в приложении MAUI
/// </summary>
public sealed class MauiBoardCanvas : IBoardCanvas
{
    private readonly ICanvas _canvas;

    public MauiBoardCanvas(ICanvas canvas)
    {
        _canvas = canvas;
    }

    public void FillRectangle(GameColor color, float x, float y, float width, float height)
    {
        _canvas.FillColor = ToMauiColor(color);
        _canvas.FillRectangle(x, y, width, height);
    }

    public void DrawRectangle(GameColor color, float strokeSize, float x, float y, float width, float height)
    {
        _canvas.StrokeColor = ToMauiColor(color);
        _canvas.StrokeSize = strokeSize;
        _canvas.DrawRectangle(
            x + strokeSize / 2.0f,
            y + strokeSize / 2.0f,
            width - strokeSize,
            height - strokeSize);
    }

    public void FillEllipse(GameColor color, float x, float y, float width, float height)
    {
        _canvas.FillColor = ToMauiColor(color);
        _canvas.FillEllipse(x, y, width, height);
    }

    public void DrawEllipse(GameColor color, float strokeSize, float x, float y, float width, float height)
    {
        _canvas.StrokeColor = ToMauiColor(color);
        _canvas.StrokeSize = strokeSize;
        _canvas.DrawEllipse(x, y, width, height);
    }

    public void DrawLine(GameColor color, float strokeSize, float x1, float y1, float x2, float y2)
    {
        _canvas.StrokeColor = ToMauiColor(color);
        _canvas.StrokeSize = strokeSize;
        _canvas.DrawLine(x1, y1, x2, y2);
    }

    private static Color ToMauiColor(GameColor color) => Color.FromRgba(color.R, color.G, color.B, 1.0);

}
