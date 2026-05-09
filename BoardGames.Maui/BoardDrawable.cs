using Microsoft.Maui.Graphics;

namespace BoardGames;

/// <summary>
/// Аналог BoardView из проекта Windows, только для MAUI
/// Служит посредником между контроллерами и графическим холстом GraphicsView
/// </summary>
public sealed class BoardDrawable : IDrawable
{
    public IGameController? Controller { get; set; } // контроллер конкретной игры

    public BoardRect CurrentBoardRect { get; private set; } // прямоугольник, где нарисована доска

    public void Draw(ICanvas canvas, RectF dirtyRect) // вызывается автоматически для перерисовки
    {
        if (Controller is null)
            return;

        int boardSize = Controller.BoardSize; // количество клеток по горизонтали и вертикали

        float cell = Math.Min(dirtyRect.Width, dirtyRect.Height) / boardSize;  // размер одной клетки
        if (cell <= 0)
            return;

        float size = cell * boardSize; // размер квадратной доски, умещающейся в доступную область

        float left = dirtyRect.Left + (dirtyRect.Width - size) / 2f; // центрирование доски в доступной области
        float top = dirtyRect.Top + (dirtyRect.Height - size) / 2f;

        CurrentBoardRect = new BoardRect(left, top, size, size);

        var boardCanvas = new MauiBoardCanvas(canvas);
        Controller.Draw(boardCanvas, CurrentBoardRect);
    }

    /// <summary>
    /// Находит клетку, в которую нажал пользователь, по координатам
    /// </summary>
    public (int row, int col)? HitTest(double x, double y)
    {
        if (Controller is null)
            return null;

        BoardRect rect = CurrentBoardRect;
        int boardSize = Controller.BoardSize; // количество клеток по горизонтали и вертикали

        if (x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom)
            return null;

        float cell = rect.Width / boardSize;

        int col = (int)((x - rect.Left) / cell);
        int row = (int)((y - rect.Top) / cell);

        if (row < 0 || row >= boardSize || col < 0 || col >= boardSize)
            return null;

        return (row, col);
    }
}