using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics; // для контроля времени работы алгоритма
using System.Linq;

namespace BoardGames;

/// <summary>
/// Сеанс MCTS (Monte Carlo Tree Search) для одной партии
/// Объект хранит дерево ходов с их оценками, которое сохраняется и достраивается после каждого хода
/// </summary>
public sealed class MctsSession<TPos, TMove> where TMove : class
{
    /// <summary>
    /// Узел дерева MCTS
    /// </summary>
    private sealed class Node
    {
        public TPos Position { get; } // позиция на доске
        public string PositionKey { get; } // уникальный ключ позиции
        public int SideToMove { get; } // кому принадлежит очередь хода

        public Node? Parent { get; set; } // узел-родитель
        public TMove? MoveFromParent { get; set; } // ход, ведущий от родителя к данному узлу

        public List<Node> Children { get; } = new(); // список дочерних узлов

        public List<TMove>? UnexpandedMoves { get; set; } // ходы из узла, ещё не развёрнутые в дочерние узлы
        public bool MovesInitialized { get; set; } // создан ли список ходов из этого узла

        public bool PassAvailable { get; set; } // допускается ли пас, если нет ходов (пас это ход в дочерний узел)
        public bool PassExpanded { get; set; } // был ли уже сделан пас (тогда второй пас это конец игры)

        public int Visits { get; set; } // количество посещений узла (лучшие ходы будут иметь максимум посещений)
        public double TotalScore { get; set; } // общий счёт, накопленный в узле

        public double AverageScore => Visits == 0 ? 0.0 : TotalScore / Visits; // средний счёт (как часто узел вёл к победе)

        public Node(TPos position, string positionKey, int sideToMove, Node? parent, TMove? moveFromParent)
        {
            Position = position;
            PositionKey = positionKey;
            SideToMove = sideToMove;
            Parent = parent;
            MoveFromParent = moveFromParent;
        }
    }

    private readonly Func<TPos, int, List<TMove>> _legalMoves; // функция возможных ходов в данной позиции у данного игрока
    private readonly Func<TPos, TMove, int, TPos> _applyMoveToCopy; // применить к позиции ход данного игрока
    private readonly Func<TPos, int, int, Random, double> _rolloutScore; // оценка позиции путём случайных доигрываний
    private readonly Func<int, int> _opponent; // цвет противника (обычно функция умножения на -1)
    private readonly Func<TPos, int, bool> _isTerminal; // является ли позиция финальной при ходе данного игрока
    private readonly Func<TPos, string> _positionKey; // ключ позиции
    private readonly bool _canPass; // разрешён ли пас, если нет ходов
    private readonly double _explorationConstant; // коэффициент исследования

    private readonly Random _rng = Random.Shared;

    private Node? _root; // корень

    public MctsSession(
        Func<TPos, int, List<TMove>> legalMoves,
        Func<TPos, TMove, int, TPos> applyMoveToCopy,
        Func<TPos, int, int, Random, double> rolloutScore,
        Func<int, int> opponent,
        Func<TPos, int, bool> isTerminal,
        Func<TPos, string> positionKey,
        bool canPass,
        double explorationConstant = 1.4)
    {
        _legalMoves = legalMoves;
        _applyMoveToCopy = applyMoveToCopy;
        _rolloutScore = rolloutScore;
        _opponent = opponent;
        _isTerminal = isTerminal;
        _positionKey = positionKey;
        _canPass = canPass;
        _explorationConstant = explorationConstant;
    }

    /// <summary>
    /// Стереть всё дерево
    /// </summary>
    public void Reset()
    {
        _root = null;
    }

    /// <summary>
    /// Найти лучший ход за отведённое время timeLimitMs (с максимальным количеством посещений)
    /// </summary>
    public TMove? SearchBestMove(TPos position, int sideToMove, int rootPlayer, int timeLimitMs)
    {
        AdvanceRootToPosition(position, sideToMove);

        if (_root is null)
            return null;

        InitializeNode(_root);

        if ((_root.UnexpandedMoves is null || _root.UnexpandedMoves.Count == 0) &&
            !_root.PassAvailable &&
            _root.Children.Count == 0) // ходить некуда - нет ни неразвёрнутых ходов, ни дочерних узлов, ни паса
        {
            return null;
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeLimitMs)
        {
            RunIteration(rootPlayer);
        }

        Node? bestChild = _root.Children
            .OrderByDescending(ch => ch.Visits)
            .ThenByDescending(ch => ch.AverageScore)
            .FirstOrDefault();

        return bestChild?.MoveFromParent;
    }

    /// <summary>
    /// Сдвинуть корень дерева к актуальной позиции, чтобы не строить его каждый ход заново
    /// </summary>
    public void AdvanceRootToPosition(TPos position, int sideToMove)
    {
        string key = _positionKey(position);

        if (_root is null)
        {
            _root = CreateRoot(position, sideToMove);



            System.Diagnostics.Debug.WriteLine($"MCTS: дерева не было, создан новый корень. Side={sideToMove}");
            System.Diagnostics.Debug.WriteLine($"MCTS root: Visits={_root.Visits}, Children={_root.Children.Count}, Side={_root.SideToMove}");


            return;
        }

        // Если корень уже и так соответствует позиции, ничего не делаем
        if (_root.PositionKey == key && _root.SideToMove == sideToMove)
        {


            System.Diagnostics.Debug.WriteLine($"MCTS: корень уже соответствует текущей позиции. Visits={_root.Visits}, Children={_root.Children.Count}");
            System.Diagnostics.Debug.WriteLine($"MCTS root: Visits={_root.Visits}, Children={_root.Children.Count}, Side={_root.SideToMove}");


            return;
        }
        // Ищем позицию среди дочерних узлов; если не нашлась, создаём новое дерево
        Node? child = FindChildOfRoot(key, sideToMove);
        if (child is not null)
        {
            child.Parent = null;
            _root = child;




            System.Diagnostics.Debug.WriteLine($"MCTS: корень успешно переведён в дочерний узел. Visits={_root.Visits}, Children={_root.Children.Count}");
            System.Diagnostics.Debug.WriteLine($"MCTS root: Visits={_root.Visits}, Children={_root.Children.Count}, Side={_root.SideToMove}");



            return;
        }

        _root = CreateRoot(position, sideToMove);




        System.Diagnostics.Debug.WriteLine($"MCTS: среди детей совпадения не найдено, дерево пересоздано. Side={sideToMove}");
        System.Diagnostics.Debug.WriteLine($"MCTS root: Visits={_root.Visits}, Children={_root.Children.Count}, Side={_root.SideToMove}");

    }

    private Node CreateRoot(TPos position, int sideToMove)
    {
        return new Node(
            position,
            _positionKey(position),
            sideToMove,
            parent: null,
            moveFromParent: null);
    }

    /// <summary>
    /// Выполнить итерацию MCTS: Selection -> Expansion -> Rollout -> Backpropagation
    /// root_player - игрок, с точки зрения которого строится оценка (ИИ)
    /// </summary>
    private void RunIteration(int rootPlayer)
    {
        if (_root is null)
            return;

        Node node = _root;

        // Selection
        while (true)
        {
            if (_isTerminal(node.Position, node.SideToMove))
                break;

            InitializeNode(node);

            // Если есть ещё неразвёрнутые ходы, делаем Expansion
            if (node.UnexpandedMoves?.Count > 0)
            {
                int index = _rng.Next(node.UnexpandedMoves.Count);
                TMove move = node.UnexpandedMoves[index];
                node.UnexpandedMoves.RemoveAt(index);

                TPos childPosition = _applyMoveToCopy(node.Position, move, node.SideToMove);
                Node child = new(
                    childPosition,
                    _positionKey(childPosition),
                    _opponent(node.SideToMove),
                    node,
                    move);

                node.Children.Add(child);
                node = child;
                break;
            }

            // Если ходов нет, но по правилам возможен пас, разворачиваем пас один раз
            if (node.PassAvailable && !node.PassExpanded)
            {
                Node passChild = new(
                    node.Position,
                    node.PositionKey,
                    _opponent(node.SideToMove),
                    node,
                    null);

                node.PassExpanded = true;
                node.Children.Add(passChild);
                node = passChild;
                break;
            }

            // Иначе спускаемся к лучшему дочернему узлу по формуле UCT
            if (node.Children.Count == 0)
                break;
            node = SelectChildByUct(node);
        }

        // Rollout
        double result = _rolloutScore(node.Position, rootPlayer, node.SideToMove, _rng);

        // Backpropagation
        for (Node? current = node; current is not null; current = current.Parent)
        {
            current.Visits++;
            current.TotalScore += result;
        }
    }

    /// <summary>
    /// Инициализировать список ходов узла (если это не было уже сделано ранее)
    /// </summary>
    private void InitializeNode(Node node)
    {
        if (node.MovesInitialized)
            return;

        node.UnexpandedMoves = _legalMoves(node.Position, node.SideToMove);
        node.MovesInitialized = true;

        if (node.UnexpandedMoves.Count == 0 && _canPass && !_isTerminal(node.Position, node.SideToMove))
            node.PassAvailable = true;
    }

    /// <summary>
    /// Выбор дочернего узла по формуле UCT: averageScore + C * sqrt(ln(parentVisits) / childVisits)
    /// Первое слагаемое отвечает за эксплуатацию перспективных ветвей, второе за исследование малоизученных
    /// </summary>
    private Node SelectChildByUct(Node node)
    {
        double logParentVisits = Math.Log(Math.Max(1, node.Visits));

        return node.Children
            .OrderByDescending(child =>
                child.AverageScore +
                _explorationConstant * Math.Sqrt(logParentVisits / Math.Max(1, child.Visits)))
            .First();
    }

    /// <summary>
    /// Поиск узла с данным ключом позиции среди дочерних узлов корня
    /// </summary>
    private Node? FindChildOfRoot(string positionKey, int sideToMove)
    {
        if (_root is null)
            return null;

        foreach (Node child in _root.Children)
        {
            if (child.PositionKey == positionKey && child.SideToMove == sideToMove)
                return child;
        }

        return null;
    }
}