using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace BoardGames;

/// <summary>
/// Поле доски (рисование и обработка кликов, в любой игре)
/// </summary>
public sealed class BoardView : Control
{

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BoardSize { get; set; } = 8; // размер доски

    /// <summary>Callback отрисовки доски</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<Graphics, Rectangle>? DrawCallback { get; set; }

    /// <summary>Обработка щелчка на поле доски</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<int, int>? CellClick { get; set; }

    public BoardView() // конструктор
    {
        DoubleBuffered = true; // двойная буферизация для лучшего отображения доски
        SetStyle(ControlStyles.ResizeRedraw, true); // метод класса Control для перерисовки доски при изменении размеров
        BackColor = Color.White;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e) // переопределение метода рисования из родительского класса Control
    {
        base.OnPaint(e); // стандартный метод рисования
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; // включить сглаживание
        var rect = GetBoardRect(); // вычисляет область, в которой должна рисоваться доска
        DrawCallback?.Invoke(e.Graphics, rect); // запуск метода рисования конкретной игры, если он не NULL
    }

    protected override void OnMouseClick(MouseEventArgs e) // переопределение обработчика щелчка мышью
    {
        base.OnMouseClick(e); // стандартный обработчик
        var rect = GetBoardRect(); // вычисляет область, в которой должна рисоваться доска
        if (!rect.Contains(e.Location)) return; // если щелчок не внутри доски, то обрабатывать не нужно

        int cell = rect.Width / BoardSize; // размер клетки
        int col = (e.X - rect.Left) / cell; // преобразование X-координаты курсора мыши в номер столбца
        int row = (e.Y - rect.Top) / cell; // преобразование Y-координаты курсора мыши в номер строки

        if (row < 0 || row >= BoardSize || col < 0 || col >= BoardSize) return; // row и col должны быть от 0 до 7
        CellClick?.Invoke(row, col); // вызов обработчика щелчка конкретной игры
    }

    public Rectangle GetBoardRect() // вычисление квадратной области доски
    {
        int cell = Math.Min(ClientSize.Width, ClientSize.Height) / BoardSize;
        int size = cell * BoardSize;

        int left = (ClientSize.Width - size) / 2;
        int top = (ClientSize.Height - size) / 2;

        return new Rectangle(left, top, size, size);
    }
}