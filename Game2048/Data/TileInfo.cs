using System.Windows.Controls;
using System.Windows.Media;

namespace Game2048.Data;

class TileInfo
{
    public Border TileBorder { get; set; }
    public TranslateTransform Translate { get; set; }
    public ScaleTransform Scale { get; set; }

    public int Row { get; set; }
    public int Col { get; set; }

    public TileInfo(Border border, TranslateTransform translate, ScaleTransform scale, int row, int col)
    {
        TileBorder = border;
        Translate = translate;
        Scale = scale;
        Row = row;
        Col = col;
    }
}
