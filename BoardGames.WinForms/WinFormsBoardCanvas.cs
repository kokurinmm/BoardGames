using System.Drawing;

namespace BoardGames;

/// <summary>
/// Класс для рисования в приложении Windows
/// </summary>
public sealed class WinFormsBoardCanvas : IBoardCanvas
{
    private readonly Graphics _graphics;

    public WinFormsBoardCanvas(Graphics graphics)
    {
        _graphics = graphics;
    }

    public void FillRectangle(GameColor color, float x, float y, float width, float height)
    {
        using SolidBrush brush = new(ToDrawingColor(color));
        _graphics.FillRectangle(brush, x, y, width, height);
    }

    public void DrawRectangle(GameColor color, float strokeSize, float x, float y, float width, float height)
    {
        using Pen pen = new(ToDrawingColor(color), strokeSize);
        pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
        _graphics.DrawRectangle(pen, x, y, width, height);
    }

    public void FillEllipse(GameColor color, float x, float y, float width, float height)
    {
        using SolidBrush brush = new(ToDrawingColor(color));
        _graphics.FillEllipse(brush, x, y, width, height);
    }

    public void DrawEllipse(GameColor color, float strokeSize, float x, float y, float width, float height)
    {
        using Pen pen = new(ToDrawingColor(color), strokeSize);
        _graphics.DrawEllipse(pen, x, y, width, height);
    }

    public void DrawLine(GameColor color, float strokeSize, float x1, float y1, float x2, float y2)
    {
        using Pen pen = new(ToDrawingColor(color), strokeSize);
        _graphics.DrawLine(pen, x1, y1, x2, y2);
    }

    private static Color ToDrawingColor(GameColor color)
    {
        int a = 255;
        int r = ToByte(color.R);
        int g = ToByte(color.G);
        int b = ToByte(color.B);

        return Color.FromArgb(a, r, g, b);
    }

    private static int ToByte(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return (int)MathF.Round(255f * x);
    }
}