using System.Drawing;

namespace BoardGames;

/// <summary>
/// Временный тестовый контроллер
/// </summary>
public sealed class TestController : IGameController
{
    public GameKind Kind { get; }
    public int BoardSize => 8;

    public string GameDisplayName => "Тест";

    public int WhitePieceCount => 0;

    public int BlackPieceCount => 0;

    public AiMode Mode { get; set; } = AiMode.AlphaBeta;
    public int AlphaBetaDepth { get; set; } = 3;
    public int MonteCarloSimulations { get; set; } = 50;

    public bool IsGameOver => false;
    public bool IsAiTurn => false;

    public int HumanPlayer { get; private set; } = 1;
    public string HumanPlayerDisplayName => Players.CheckersName(HumanPlayer);

    public string? GameOverMessage => null;

    // последняя клетка, по которой щёлкнул пользователь
    private int _lastRow = -1;
    private int _lastCol = -1;

    public TestController(GameKind kind)
    {
        Kind = kind;
    }

    public void NewGame()
    {
        HumanPlayer = Random.Shared.Next(2) == 0 ? 1 : -1;
        _lastRow = -1;
        _lastCol = -1;
    }

    public void Draw(Graphics g, Rectangle boardRect)
    {
        int cell = boardRect.Width / BoardSize;

        if (Kind == GameKind.Checkers)
        {
            // пустая шашечная доска
            for (int row = 0; row < BoardSize; row++)
                for (int col = 0; col < BoardSize; col++)
                {
                    Rectangle rect = new Rectangle(
                        boardRect.Left + col * cell,
                        boardRect.Top + row * cell,
                        cell,
                        cell);

                    Color color = ((row + col) % 2 == 0)
                        ? Color.PeachPuff
                        : Color.Peru;

                    using SolidBrush brush = new SolidBrush(color);
                    g.FillRectangle(brush, rect);
                }
        }
        else
        {
            // пустая доска реверси
            using SolidBrush bgBrush = new SolidBrush(Color.DarkGreen);
            g.FillRectangle(bgBrush, boardRect);

            using Pen gridPen = new Pen(Color.Black, 1);
            for (int i = 0; i <= BoardSize; i++)
            {
                int x = boardRect.Left + i * cell;
                int y = boardRect.Top + i * cell;

                g.DrawLine(gridPen, x, boardRect.Top, x, boardRect.Bottom);
                g.DrawLine(gridPen, boardRect.Left, y, boardRect.Right, y);
            }
        }

        // подсветка клетки, по которой щёлкнул пользователь
        if (_lastRow >= 0 && _lastCol >= 0)
        {
            Rectangle rect = new Rectangle(
                boardRect.Left + _lastCol * cell,
                boardRect.Top + _lastRow * cell,
                cell,
                cell);

            using Pen pen = new Pen(Color.Red, 3);
            pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
            g.DrawRectangle(pen, rect);
        }
    }

    public void HandleCellClick(int row, int col)
    {
        _lastRow = row;
        _lastCol = col;
    }

    public bool BeginAiTurnAnimation() => false;

    public bool HasPendingAiAnimation => false;

    public bool ApplyNextAiAnimationStep() => false;

    public bool MakeAiTurn()
    {
        // в тестовом контролере ИИ ничего не делает
        return false;
    }
}