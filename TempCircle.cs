using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TempBar;

/// <summary>Círculo com o valor no centro; a cor segue o degradê verde → azul → vermelho da temperatura.</summary>
public class TempCircle : UserControl, ITempView
{
    public const double CellWidth = 66, CellHeight = 66;

    private readonly TextBlock _labelText = TempVisuals.CreateText(9);
    private readonly TextBlock _valueText = TempVisuals.CreateText(15, "--", FontWeights.Bold);
    private readonly SolidColorBrush _ring = new(TempVisuals.ColorAt(0));
    private readonly SolidColorBrush _fill = new(TempVisuals.ColorAt(0));

    private double _min = 30;
    private double _max = 90;

    public TempCircle()
    {
        Width = CellWidth;
        Height = CellHeight;

        _fill.Opacity = 0.28;

        var disc = new Ellipse
        {
            Margin = new Thickness(3),
            Fill = _fill,
            Stroke = _ring,
            StrokeThickness = 4,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.6 }
        };

        _labelText.HorizontalAlignment = HorizontalAlignment.Center;
        _valueText.HorizontalAlignment = HorizontalAlignment.Center;
        var texts = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children = { _labelText, _valueText }
        };

        var grid = new Grid { IsHitTestVisible = false };
        grid.Children.Add(disc);
        grid.Children.Add(texts);
        Content = grid;
    }

    public void Configure(string label, double min, double max)
    {
        _labelText.Text = label;
        _min = min;
        _max = max;
    }

    public void SetTemperature(double? temp)
    {
        _valueText.Text = temp is double t ? $"{t:0}°" : "--";

        var target = TempVisuals.ColorAt(TempVisuals.Fraction(temp, _min, _max));
        AnimateColor(_ring, target);
        AnimateColor(_fill, target);
    }

    private static void AnimateColor(SolidColorBrush brush, Color target) =>
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
}
