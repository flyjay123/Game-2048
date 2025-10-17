using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Game2048.Data;

namespace Game2048;

public partial class GamePage : UserControl
{
    // 公共常量定义（放在类顶部）
    private const double TileCornerRadius = 6; // 圆角统一
    private const int TileSize = 100;
    private const int TileMargin = 6;
    private const int GridSize = 4;
    private const double Gap = 6; // 单个格子四周间隙（背景 Border.Margin="6"）
    private const double OuterMargin = 16; // 背景 ItemsControl 与 Canvas 的统一外边距
    
    private int score = 0;
    private int prevScore = 0;   // 可选：用于 Undo

    private int[,] oldBoard;
    
    private int[,] currentBoard = new int[GridSize, GridSize];
    private int[,] CurrentBoard
    {
        get => currentBoard;
        set
        {
            oldBoard = currentBoard;
            currentBoard = value;
        }
    } 

    // 添加背景网格数据绑定
    public ObservableCollection<BackgroundCell> BackgroundCells { get; }
        = new ObservableCollection<BackgroundCell>();

    public class BackgroundCell : INotifyPropertyChanged
    {
        private double _size;
        private Thickness _margin;
        private CornerRadius _cornerRadius;

        private SolidColorBrush _backgroundColor =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDC1B4"));

        public double Size
        {
            get => _size;
            set
            {
                if (Math.Abs(_size - value) > 0.1)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        public Thickness Margin
        {
            get => _margin;
            set
            {
                _margin = value;
                OnPropertyChanged(nameof(Margin));
            }
        }

        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = value;
                OnPropertyChanged(nameof(CornerRadius));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


    private Dictionary<(int, int), TileInfo> tileControls = new();

    public class AnimationInfo
    {
        public int FromRow { get; set; }
        public int FromCol { get; set; }
        public int ToRow { get; set; }
        public int ToCol { get; set; }
        public int NewValue { get; set; }
        public bool IsMerged { get; set; }
        public bool IsNew { get; set; }

        public AnimationInfo(int fromRow, int fromCol, int toRow, int toCol, int newValue, bool isMerged,
            bool isNew = false)
        {
            FromRow = fromRow;
            FromCol = fromCol;
            ToRow = toRow;
            ToCol = toCol;
            NewValue = newValue;
            IsMerged = isMerged;
            IsNew = isNew;
        }
    }

    public GamePage()
    {
        InitializeComponent();
        DataContext = this;

        InitBackgroundGrid();
        Loaded += async (_, __) =>
        {
            RecomputeLayout();           // 有效 slot/tile 尺寸
            AddRandomNumber();
            AddRandomNumber();
            await DrawCurrentBoardAsync(); // 等布局后再完整绘制一次
        };
    }
    
    private Task DrawCurrentBoardAsync()
    {
        TileCanvas.Children.Clear();
        tileControls.Clear();

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                int v = CurrentBoard[r, c];
                if (v == 0) continue;
                AddOrUpdateTile(r, c, v, isNew:false, isMerged:false); // 直接画当前棋盘
            }
        }
        return Task.CompletedTask;
    }

    private void AddRandomNumber()
    {
        Random rnd = new();
        while (true)
        {
            int x = rnd.Next(4);
            int y = rnd.Next(4);
            if (CurrentBoard[x, y] == 0)
            {
                CurrentBoard[x, y] = rnd.Next(0, 10) == 0 ? 4 : 2;
                break;
            }
        }
    }
    
    private Task AnimateMoveAsync(TileInfo tile, double targetLeft, double targetTop, TimeSpan duration)
    {
        var tcs = new TaskCompletionSource<bool>();

        double currentLeft = Canvas.GetLeft(tile.TileBorder);
        double currentTop  = Canvas.GetTop(tile.TileBorder);

        var animX = new DoubleAnimation
        {
            From = tile.Translate.X,
            To   = targetLeft - currentLeft,
            Duration = duration,
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        var animY = new DoubleAnimation
        {
            From = tile.Translate.Y,
            To   = targetTop - currentTop,
            Duration = duration,
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };

        int done = 0;
        void completeOne()
        {
            if (Interlocked.Increment(ref done) == 2)
            {
                // 动画结束：归位并清零偏移
                Canvas.SetLeft(tile.TileBorder, targetLeft);
                Canvas.SetTop(tile.TileBorder,  targetTop);
                tile.Translate.X = 0;
                tile.Translate.Y = 0;
                tcs.TrySetResult(true);
            }
        }

        animX.Completed += (s, e) => completeOne();
        animY.Completed += (s, e) => completeOne();

        tile.Translate.BeginAnimation(TranslateTransform.XProperty, animX);
        tile.Translate.BeginAnimation(TranslateTransform.YProperty, animY);

        return tcs.Task;
    }

    private SolidColorBrush GetTileColor(int val)
    {
        return val switch
        {
            2 => new SolidColorBrush(Color.FromRgb(238, 228, 218)),
            4 => new SolidColorBrush(Color.FromRgb(237, 224, 200)),
            8 => new SolidColorBrush(Color.FromRgb(242, 177, 121)),
            16 => new SolidColorBrush(Color.FromRgb(245, 149, 99)),
            32 => new SolidColorBrush(Color.FromRgb(246, 124, 95)),
            64 => new SolidColorBrush(Color.FromRgb(246, 94, 59)),
            128 => new SolidColorBrush(Color.FromRgb(237, 207, 114)),
            256 => new SolidColorBrush(Color.FromRgb(237, 204, 97)),
            512 => new SolidColorBrush(Color.FromRgb(237, 200, 80)),
            1024 => new SolidColorBrush(Color.FromRgb(237, 197, 63)),
            2048 => new SolidColorBrush(Color.FromRgb(237, 194, 46)),
            _ => new SolidColorBrush(Color.FromRgb(205, 193, 180))
        };
    }

    private (double slotW, double slotH, double tileW, double tileH) GetLayout()
    {
        double rw = Root.ActualWidth, rh = Root.ActualHeight;
        double availW = Math.Max(0, rw - 2 * OuterMargin);
        double availH = Math.Max(0, rh - 2 * OuterMargin);

        double slotW = availW / GridSize;
        double slotH = availH / GridSize;

        double tileW = Math.Max(0, slotW - 2 * Gap);
        double tileH = Math.Max(0, slotH - 2 * Gap);
        return (slotW, slotH, tileW, tileH);
    }

    private async void GamePage_KeyDown(object sender, KeyEventArgs e)
    {
        bool moved = false;
        List<AnimationInfo> animations = null;

        oldBoard = (int[,])CurrentBoard.Clone();
        prevScore = score; // 👈 记录上次分数，Undo 用

        switch (e.Key)
        {
            case Key.Up or Key.W:    animations = MoveUpCore(out moved);    break;
            case Key.Down or Key.S:  animations = MoveDownCore(out moved);  break;
            case Key.Left or Key.A:  animations = MoveLeftCore(out moved);  break;
            case Key.Right or Key.D: animations = MoveRightCore(out moved); break;
        }
        if (!moved) return;

        // ✅ 计算本次合并得分（所有合并产生的新值之和）
        int gained = animations.Where(a => a.IsMerged).Sum(a => a.NewValue);
        if (gained > 0)
        {
            score += gained;
            ScoreTextBlock.Text = score.ToString();
        }

        await PlayMoveAnimationsAsync(animations);

        AddRandomNumber();
        await DrawCurrentBoardAsync();

        if (IsGameOver()) MessageBox.Show("游戏结束！");
    }


    // 左移核心逻辑
    private List<AnimationInfo> MoveLeftCore(out bool moved)
    {
        moved = false;
        var animations = new List<AnimationInfo>();
        for (int row = 0; row < GridSize; row++)
        {
            int[] original = Enumerable.Range(0, GridSize).Select(col => CurrentBoard[row, col]).ToArray();
            var (newRow, rowMoved, rowAnimations) = ProcessRowLeft(original, row);
            if (rowMoved) moved = true;
            for (int col = 0; col < GridSize; col++)
                CurrentBoard[row, col] = newRow[col];
            animations.AddRange(rowAnimations);
        }

        return animations;
    }

    // 右移核心逻辑
    private List<AnimationInfo> MoveRightCore(out bool moved)
    {
        moved = false;
        var animations = new List<AnimationInfo>();
        for (int row = 0; row < GridSize; row++)
        {
            int[] original = Enumerable.Range(0, GridSize).Select(col => CurrentBoard[row, GridSize - 1 - col]).ToArray();
            var (newRow, rowMoved, rowAnimations) = ProcessRowLeft(original, row);
            if (rowMoved) moved = true;
            for (int col = 0; col < GridSize; col++)
                CurrentBoard[row, GridSize - 1 - col] = newRow[col];
            // 调整动画坐标
            foreach (var anim in rowAnimations)
            {
                anim.FromCol = GridSize - 1 - anim.FromCol;
                anim.ToCol = GridSize - 1 - anim.ToCol;
            }

            animations.AddRange(rowAnimations);
        }

        return animations;
    }

    // 上移核心逻辑
    private List<AnimationInfo> MoveUpCore(out bool moved)
    {
        moved = false;
        var animations = new List<AnimationInfo>();
        for (int col = 0; col < GridSize; col++)
        {
            int[] original = Enumerable.Range(0, GridSize).Select(row => CurrentBoard[row, col]).ToArray();
            var (newCol, colMoved, colAnimations) = ProcessRowLeft(original, col);
            if (colMoved) moved = true;
            for (int row = 0; row < GridSize; row++)
                CurrentBoard[row, col] = newCol[row];

            // ✅ 只交换（上移）
            foreach (var a in colAnimations)
            {
                int colIdx = a.FromRow;
                a.FromRow = a.FromCol;
                a.FromCol = colIdx;
                a.ToRow   = a.ToCol;
                a.ToCol   = colIdx;
            }
            animations.AddRange(colAnimations);
        }
        return animations;
    }

// 下移核心逻辑
    private List<AnimationInfo> MoveDownCore(out bool moved)
    {
        moved = false;
        var animations = new List<AnimationInfo>();
        for (int col = 0; col < GridSize; col++)
        {
            int[] original = Enumerable.Range(0, GridSize).Select(row => CurrentBoard[GridSize - 1 - row, col]).ToArray();
            var (newCol, colMoved, colAnimations) = ProcessRowLeft(original, col);
            if (colMoved) moved = true;
            for (int row = 0; row < GridSize; row++)
                CurrentBoard[GridSize - 1 - row, col] = newCol[row];

            // ✅ 只镜像（下移）
            foreach (var a in colAnimations)
            {
                int colIdx    = a.FromRow;
                int fromIndex = a.FromCol;
                int toIndex   = a.ToCol;

                a.FromRow = GridSize - 1 - fromIndex;
                a.FromCol = colIdx;
                a.ToRow   = GridSize - 1 - toIndex;
                a.ToCol   = colIdx;
            }
            animations.AddRange(colAnimations);
        }
        return animations;
    }

    // 通用行处理逻辑（左移方向）
    private (int[] newRow, bool moved, List<AnimationInfo> animations) ProcessRowLeft(int[] original, int row)
    {
        int[] rowData = (int[])original.Clone();
        bool moved = false;
        var animations = new List<AnimationInfo>();

        // 压缩
        int index = 0;
        for (int i = 0; i < rowData.Length; i++)
        {
            if (rowData[i] == 0) continue;
            if (i != index)
            {
                moved = true;
                animations.Add(new AnimationInfo(row, i, row, index, rowData[i], false));
            }

            rowData[index++] = rowData[i];
            if (index - 1 != i) rowData[i] = 0;
        }

        // 合并
        for (int i = 0; i < rowData.Length - 1; i++)
        {
            if (rowData[i] != 0 && rowData[i] == rowData[i + 1])
            {
                rowData[i] *= 2;
                animations.Add(new AnimationInfo(row, i + 1, row, i, rowData[i], true));
                rowData[i + 1] = 0;
                moved = true;
            }
        }

        // 再次压缩
        index = 0;
        for (int i = 0; i < rowData.Length; i++)
        {
            if (rowData[i] == 0) continue;
            if (i != index)
            {
                moved = true;
                animations.Add(new AnimationInfo(row, i, row, index, rowData[i], false));
            }

            rowData[index++] = rowData[i];
            if (index - 1 != i) rowData[i] = 0;
        }

        return (rowData, moved, animations);
    }

    private bool IsGameOver()
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (CurrentBoard[i, j] == 0)
                    return false;
                if (j < 3 && CurrentBoard[i, j] == CurrentBoard[i, j + 1])
                    return false;
                if (i < 3 && CurrentBoard[i, j] == CurrentBoard[i + 1, j])
                    return false;
            }
        }

        return true;
    }

// 初始化背景网格
    private void InitBackgroundGrid()
    {
        BackgroundCells.Clear();
        for (int i = 0; i < GridSize * GridSize; i++)
        {
            BackgroundCells.Add(new BackgroundCell
            {
                Size = 100, // 初值占位，Loaded 后会被 RecomputeLayout 覆盖
                Margin = new Thickness(Gap),
                CornerRadius = new CornerRadius(6)
            });
        }
    }

    private void AddOrUpdateTile(int row, int col, int value, bool isNew = false, bool isMerged = false)
    {
        var (slotW, slotH, tileW, tileH) = GetLayout();
        if (tileW <= 0 || tileH <= 0) return;

        double left = col * slotW + Gap; // 槽位左 + Gap
        double top = row * slotH + Gap; // 槽位上 + Gap

        var text = new TextBlock
        {
            Text = value.ToString(),
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center
        };

        var border = new Border
        {
            Width = tileW,
            Height = tileH,
            Background = GetTileColor(value),
            CornerRadius = new CornerRadius(6),
            Child = text
        };

        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);

        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var tg = new TransformGroup();
        tg.Children.Add(scale);
        tg.Children.Add(translate);
        border.RenderTransform = tg;
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        TileCanvas.Children.Add(border);

        var info = new TileInfo(border, translate, scale, row, col);
        tileControls[(row, col)] = info;

        if (isNew) PlayAppearAnimation(scale);
        else if (isMerged) PlayMergeAnimation(scale);
    }

    private async Task PlayMoveAnimationsAsync(List<AnimationInfo> animations)
    {
        // 1) 清场，用“旧棋盘”把源位置画出来（保证能找到 fromRow/fromCol 的 tile）
        TileCanvas.Children.Clear();
        tileControls.Clear();
        for (int r = 0; r < GridSize; r++)
        for (int c = 0; c < GridSize; c++)
            if (oldBoard[r, c] != 0)
                AddOrUpdateTile(r, c, oldBoard[r, c], isNew:false, isMerged:false);

        // 2) 开始所有移动动画并等待全部完成（不用 Task.Delay 了）
        var (slotW, slotH, tileW, tileH) = GetLayout();
        var tasks = new List<Task>();
        foreach (var anim in animations)
        {
            if (tileControls.TryGetValue((anim.FromRow, anim.FromCol), out var tile))
            {
                double targetLeft = anim.ToCol * slotW + Gap;
                double targetTop  = anim.ToRow * slotH + Gap;
                tasks.Add(AnimateMoveAsync(tile, targetLeft, targetTop, TimeSpan.FromMilliseconds(150)));
            }
        }
        await Task.WhenAll(tasks);

        // 3) 动画结束后，完整重绘“新棋盘”（避免旧 UI 残影）
        await DrawCurrentBoardAsync();

        // 4) 对发生合并的目标格，做一次弹跳（此时新棋盘的目标格已存在）
        foreach (var anim in animations.Where(a => a.IsMerged))
        {
            if (tileControls.TryGetValue((anim.ToRow, anim.ToCol), out var mergedTile))
            {
                PlayMergeAnimation(mergedTile.Scale);
            }
        }
    }


    private void PlayAppearAnimation(ScaleTransform scale)
    {
        var appearAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, appearAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, appearAnim);
    }

    private void PlayMergeAnimation(ScaleTransform scale)
    {
        var mergeAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.2,
            AutoReverse = true,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop // 动画结束后恢复原始值
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, mergeAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, mergeAnim);
    }


    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => RecomputeLayout();

    private void RecomputeLayout()
    {
        if (Root.ActualWidth > Root.ActualHeight)
        {
            Root.Width = Root.ActualHeight;
        }
        else
        {
            Root.Height = Root.ActualWidth;
        }
        double rw = Root.ActualWidth, rh = Root.ActualHeight;
        if (rw <= 0 || rh <= 0)
        {
            Dispatcher.BeginInvoke((Action)RecomputeLayout,
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        // 可用区域（扣掉 ItemsControl/Canvas 的外边距）
        double availW = Math.Max(0, rw - 2 * OuterMargin);
        double availH = Math.Max(0, rh - 2 * OuterMargin);

        // 每个槽位的宽/高（UniformGrid 均分）
        double slotW = availW / GridSize;
        double slotH = availH / GridSize;

        // 数字方块的实际宽/高（槽位内再扣去左右/上下的 Gap）
        double tileW = Math.Max(0, slotW - 2 * Gap);
        double tileH = Math.Max(0, slotH - 2 * Gap);

        // 背景格子不再需要 Size 属性（它自己会拉伸），只保留 Margin 即可
        foreach (var cell in BackgroundCells)
            cell.Margin = new Thickness(Gap);

        // 同步所有前景块
        foreach (var entry in tileControls.Values)
        {
            double left = entry.Col * slotW + Gap;
            double top = entry.Row * slotH + Gap;

            Canvas.SetLeft(entry.TileBorder, left);
            Canvas.SetTop(entry.TileBorder, top);

            entry.TileBorder.Width = tileW;
            entry.TileBorder.Height = tileH;

            entry.Translate.X = 0;
            entry.Translate.Y = 0;
        }
    }


    public void InvokeKeyDown(KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs != null)
        {
            GamePage_KeyDown(this, keyEventArgs);
        }
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentBoard = new int[GridSize, GridSize];
        TileCanvas.Children.Clear();
        tileControls.Clear();
        score = 0;
        ScoreTextBlock.Text = "0";
        AddRandomNumber();
        AddRandomNumber();
        _ = DrawCurrentBoardAsync();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (oldBoard != null)
        {
            CurrentBoard = (int[,])oldBoard.Clone();
            var tmp = score;
            score = prevScore;
            ScoreTextBlock.Text = score.ToString();
            prevScore = tmp;
            TileCanvas.Children.Clear();
            tileControls.Clear();
            _ = DrawCurrentBoardAsync();
        }
    }

    private void BreakButton_Click(object sender, RoutedEventArgs e)
    {
        // 改变鼠标指针为选择工具
        Mouse.OverrideCursor = Cursors.Cross;

        // 定义事件处理器
        void MouseHandler(object s, MouseButtonEventArgs args)
        {
            Point clickPoint = args.GetPosition(TileCanvas);
            foreach (var entry in tileControls.Values.ToList())
            {
                double left = Canvas.GetLeft(entry.TileBorder);
                double top = Canvas.GetTop(entry.TileBorder);
                double right = left + entry.TileBorder.Width;
                double bottom = top + entry.TileBorder.Height;

                if (clickPoint.X >= left && clickPoint.X <= right &&
                    clickPoint.Y >= top && clickPoint.Y <= bottom)
                {
                    // 删除数字方块
                    CurrentBoard[entry.Row, entry.Col] = 0;
                    TileCanvas.Children.Remove(entry.TileBorder);
                    tileControls.Remove((entry.Row, entry.Col));
                    break;
                }
            }

            // 恢复默认鼠标指针并解绑事件
            Mouse.OverrideCursor = null;
            TileCanvas.MouseLeftButtonDown -= MouseHandler;
        }

        // 绑定事件处理器
        TileCanvas.MouseLeftButtonDown += MouseHandler;
    }
}