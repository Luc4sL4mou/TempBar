using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TempBar;

/// <summary>Velocímetro: semicírculo com degradê verde → azul → vermelho e ponteiro animado.</summary>
public class TempDial : UserControl, ITempView
{
    public const double CellWidth = 112, CellHeight = 72;

    private const double Cx = 56, Cy = 52, Radius = 44, Thickness = 7, NeedleLength = 34;
    private const int Segments = 36;   // WPF não tem degradê angular: o arco é feito de trechos coloridos

    private readonly TextBlock _info = TempVisuals.CreateText(11);
    private readonly TextBlock _minText = TempVisuals.CreateText(9);
    private readonly TextBlock _maxText = TempVisuals.CreateText(9);
    private readonly RotateTransform _needle = new(0, Cx, Cy);

    private string _label = "CPU";
    private double _min = 30;
    private double _max = 90;
    private double? _temp;

    public TempDial()
    {
        Width = CellWidth;
        Height = CellHeight;

        var canvas = new Canvas { IsHitTestVisible = false };

        for (int i = 0; i < Segments; i++)
        {
            double a0 = 180 + 180.0 * i / Segments;
            double a1 = 180 + 180.0 * (i + 1) / Segments + (i < Segments - 1 ? 0.6 : 0);   // leve sobreposição evita frestas
            canvas.Children.Add(new Path
            {
                Data = TempVisuals.Arc(Cx, Cy, Radius, a0, a1),
                Stroke = new SolidColorBrush(TempVisuals.ColorAt((i + 0.5) / Segments)),
                StrokeThickness = Thickness,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat
            });
        }

        var needle = new Line
        {
            X1 = Cx, Y1 = Cy, X2 = Cx - NeedleLength, Y2 = Cy,   // aponta para a esquerda (mínimo)
            Stroke = Brushes.White,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = _needle,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.9 }
        };
        canvas.Children.Add(needle);

        var hub = new Ellipse { Width = 10, Height = 10, Fill = Brushes.White };
        Canvas.SetLeft(hub, Cx - 5);
        Canvas.SetTop(hub, Cy - 5);
        canvas.Children.Add(hub);

        _info.Width = CellWidth;
        _info.TextAlignment = TextAlignment.Center;
        Canvas.SetTop(_info, Cy + 4);
        canvas.Children.Add(_info);

        // mínimo e máximo logo abaixo das pontas do arco
        _minText.Width = 24; _minText.TextAlignment = TextAlignment.Center;
        _maxText.Width = 24; _maxText.TextAlignment = TextAlignment.Center;
        Canvas.SetLeft(_minText, Cx - Radius - 12); Canvas.SetTop(_minText, Cy + 6);
        Canvas.SetLeft(_maxText, Cx + Radius - 12); Canvas.SetTop(_maxText, Cy + 6);
        canvas.Children.Add(_minText);
        canvas.Children.Add(_maxText);

        Content = canvas;
        Configure(_label, _min, _max);
    }

    public void Configure(string label, double min, double max)
    {
        _label = label;
        _min = min;
        _max = max;
        _minText.Text = $"{min:0}°";
        _maxText.Text = $"{max:0}°";
        Refresh(animate: false);
    }

    public void SetTemperature(double? temp)
    {
        _temp = temp;
        Refresh(animate: true);
    }

    private void Refresh(bool animate)
    {
        _info.Text = _temp is double t ? $"{_label} {t:0}°C" : $"{_label} --";
        TempVisuals.AnimateAngle(_needle, TempVisuals.Fraction(_temp, _min, _max) * 180, animate);
    }
}
