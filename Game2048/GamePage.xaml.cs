using System;
using System.Collections.Generic;
using System.Linq;
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
    private TextBlock[,] blocks = new TextBlock[4, 4];
    
    private const int GridSize = 4;
    private const int TileSize = 100;
    private const int TileMargin = 6;

    private TextBlock[,] tileBlocks = new TextBlock[GridSize, GridSize];
    private int[,] board = new int[GridSize, GridSize];

    public enum Direction { Up, Down, Left, Right }
    
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

        public AnimationInfo(int fromRow, int fromCol, int toRow, int toCol, int newValue, bool isMerged, bool isNew = false)
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
        InitBoard();
        AddRandomNumber();
        AddRandomNumber();
        UpdateUI(null);
    }

    
    private void InitBoard()
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                TextBlock tb = new TextBlock
                {
                    Text = "",
                    Height = 30,
                    Width = 30,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromRgb(205, 193, 180)),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5)
                };
                blocks[i, j] = tb;
            }
        }
    }

    private void AddRandomNumber()
    {
        Random rnd = new();
        while (true)
        {
            int x = rnd.Next(4);
            int y = rnd.Next(4);
            if (board[x, y] == 0)
            {
                board[x, y] = rnd.Next(0, 10) == 0 ? 4 : 2;
                break;
            }
        }
    }
    
    private void PrintBoardToConsole()
    {
        Console.WriteLine("==== 当前棋盘 ====");
        for (int i = 0; i < GridSize; i++)
        {
            for (int j = 0; j < GridSize; j++)
            {
                Console.Write($"{board[i, j],4}"); // 宽度对齐
            }
            Console.WriteLine();
        }
        Console.WriteLine("=================");
    }


    private async void UpdateUI(List<AnimationInfo> animations)
    {
        TileCanvas.Children.Clear();
        tileControls.Clear();

        // 标记需要移除的合并源块
        var mergedSources = animations?
            .Where(a => a.IsMerged)
            .Select(a => (a.FromRow, a.FromCol))
            .ToList();
        
        for (int i = 0; i < GridSize; i++)
        {
            for (int j = 0; j < GridSize; j++)
            {
                int val = board[i, j];
                if (val == 0) continue;

                // 跳过被合并的源块
                if (mergedSources?.Contains((i, j)) == true) continue;
                
                bool isMerged = animations?.Any(a => a.ToRow == i && a.ToCol == j && a.IsMerged) == true;
                bool isNew = animations == null || animations.All(a => !(a.ToRow == i && a.ToCol == j));

                AddOrUpdateTile(i, j, val, isNew, isMerged);
            }
        }

        if (animations != null)
        {
            foreach (var anim in animations)
            {
                if (tileControls.TryGetValue((anim.FromRow, anim.FromCol), out var tile))
                {
                    MoveTile(tile, anim.ToRow, anim.ToCol);
                }
            }
        }

        await Task.Delay(150);
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
        
    private void GamePage_KeyDown(object sender, KeyEventArgs e)
    {
        bool moved = false;
        List<AnimationInfo> animations = null;

        switch (e.Key)
        {
            case Key.Up:
                animations = MoveUpCore(out moved);
                break;
            case Key.Down:
                animations = MoveDownCore(out moved);
                break;
            case Key.Left:
                animations = MoveLeftCore(out moved);
                break;
            case Key.Right:
                animations = MoveRightCore(out moved);
                break;
        }

        if (moved)
        {
            UpdateUI(animations);
            AddRandomNumber();
            UpdateUI(null);
            if (IsGameOver()) MessageBox.Show("游戏结束！");
        }
    }
    
    // 左移核心逻辑
    private List<AnimationInfo> MoveLeftCore(out bool moved)
    {
        moved = false;
        var animations = new List<AnimationInfo>();
        for (int row = 0; row < GridSize; row++)
        {
            int[] original = Enumerable.Range(0, GridSize).Select(col => board[row, col]).ToArray();
            var (newRow, rowMoved, rowAnimations) = ProcessRowLeft(original, row);
            if (rowMoved) moved = true;
            for (int col = 0; col < GridSize; col++)
                board[row, col] = newRow[col];
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
            int[] original = Enumerable.Range(0, GridSize).Select(col => board[row, GridSize - 1 - col]).ToArray();
            var (newRow, rowMoved, rowAnimations) = ProcessRowLeft(original, row);
            if (rowMoved) moved = true;
            for (int col = 0; col < GridSize; col++)
                board[row, GridSize - 1 - col] = newRow[col];
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
            int[] original = Enumerable.Range(0, GridSize).Select(row => board[row, col]).ToArray();
            var (newCol, colMoved, colAnimations) = ProcessRowLeft(original, col);
            if (colMoved) moved = true;
            for (int row = 0; row < GridSize; row++)
                board[row, col] = newCol[row];
            // 转换行列坐标
            foreach (var anim in colAnimations)
            {
                (anim.FromRow, anim.FromCol) = (anim.FromCol, anim.FromRow);
                (anim.ToRow, anim.ToCol) = (anim.ToCol, anim.ToRow);
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
            int[] original = Enumerable.Range(0, GridSize).Select(row => board[GridSize - 1 - row, col]).ToArray();
            var (newCol, colMoved, colAnimations) = ProcessRowLeft(original, col);
            if (colMoved) moved = true;
            for (int row = 0; row < GridSize; row++)
                board[GridSize - 1 - row, col] = newCol[row];
            // 调整坐标
            foreach (var anim in colAnimations)
            {
                anim.FromRow = GridSize - 1 - anim.FromRow;
                anim.ToRow = GridSize - 1 - anim.ToRow;
                (anim.FromRow, anim.FromCol) = (anim.FromCol, anim.FromRow);
                (anim.ToRow, anim.ToCol) = (anim.ToCol, anim.ToRow);
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
                if (board[i, j] == 0)
                    return false;
                if (j < 3 && board[i, j] == board[i, j + 1])
                    return false;
                if (i < 3 && board[i, j] == board[i + 1, j])
                    return false;
            }
        }
        return true;
    }
    

    private void AddOrUpdateTile(int row, int col, int value, bool isNew = false, bool isMerged = false)
    {
        // 创建 UI
        var text = new TextBlock
        {
            Text = value.ToString(),
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Colors.Black),
            TextAlignment = TextAlignment.Center
        };

        var border = new Border
        {
            Width = TileSize,
            Height = TileSize,
            Background = GetTileColor(value),
            CornerRadius = new CornerRadius(6),
            Child = text
        };

        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scale);
        transformGroup.Children.Add(translate);
        border.RenderTransform = transformGroup;
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        TileCanvas.Children.Add(border);
        Canvas.SetLeft(border, col * (TileSize + TileMargin * 2));
        Canvas.SetTop(border, row * (TileSize + TileMargin * 2));

        var info = new TileInfo(border, translate, scale, row, col);
        tileControls[(row, col)] = info;

        if (isNew)
            PlayAppearAnimation(scale);
        else if (isMerged)
            PlayMergeAnimation(scale);
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

    
    private void MoveTile(TileInfo tile, int toRow, int toCol)
    {
        double deltaX = (toCol - tile.Col) * (TileSize + TileMargin * 2);
        double deltaY = (toRow - tile.Row) * (TileSize + TileMargin * 2);

        var animX = new DoubleAnimation
        {
            To = deltaX,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        tile.Translate.BeginAnimation(TranslateTransform.XProperty, animX);

        var animY = new DoubleAnimation
        {
            To = deltaY,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        tile.Translate.BeginAnimation(TranslateTransform.YProperty, animY);

        // 更新记录位置
        tile.Row = toRow;
        tile.Col = toCol;
    }
    

    public void InvokeKeyDown(KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs != null)
        {
            GamePage_KeyDown(this, keyEventArgs);
        }
    }
}