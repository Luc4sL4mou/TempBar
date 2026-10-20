using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Controls;

namespace TempBar;

/// <summary>Estilo visual dos indicadores.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LayoutStyle
{
    Bar,     // barra horizontal com marcador
    Dial,    // velocímetro (semicírculo com ponteiro)
    Circle   // círculo com o valor no centro
}

/// <summary>Contrato comum aos indicadores de temperatura (barra, velocímetro e círculo).</summary>
public interface ITempView
{
    void Configure(string label, double min, double max);
    void SetTemperature(double? temp);
}

/// <summary>Cores e ajudantes compartilhados pelos indicadores.</summary>
public static class TempVisuals
{
    private static readonly Color Cool = (Color)ColorConverter.ConvertFromString("#2ECC71");   // verde
    private static readonly Color Mid = (Color)ColorConverter.ConvertFromString("#3498DB");    // azul
    private static readonly Color Hot = (Color)ColorConverter.ConvertFromString("#E74C3C");    // vermelho

    /// <summary>Cor do degradê verde → azul → vermelho para uma fração de 0 a 1.</summary>
    public static Color ColorAt(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        return fraction < 0.5
            ? Lerp(Cool, Mid, fraction * 2)
            : Lerp(Mid, Hot, (fraction - 0.5) * 2);
    }

    public static double Fraction(double? temp, double min, double max)
    {
        if (temp is not double t) return 0;
        double range = max - min;
        return range <= 0 ? 0 : Math.Clamp((t - min) / range, 0, 1);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>Texto branco com sombra escura para ler sobre qualquer fundo.</summary>
    public static TextBlock CreateText(double size, string text = "", FontWeight? weight = null) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        FontSize = size,
        FontWeight = weight ?? FontWeights.SemiBold,
        IsHitTestVisible = false,
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 0, Opacity = 1 }
    };

    /// <summary>Arco de círculo; ângulos em graus na tela (180 = esquerda, 270 = topo, 360 = direita).</summary>
    public static Geometry Arc(double cx, double cy, double radius, double startDeg, double endDeg)
    {
        static Point On(double cx, double cy, double r, double deg) =>
            new(cx + r * Math.Cos(deg * Math.PI / 180), cy + r * Math.Sin(deg * Math.PI / 180));

        var figure = new PathFigure { StartPoint = On(cx, cy, radius, startDeg), IsClosed = false };
        figure.Segments.Add(new ArcSegment(On(cx, cy, radius, endDeg), new Size(radius, radius), 0,
            Math.Abs(endDeg - startDeg) > 180, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    public static void AnimateAngle(RotateTransform transform, double target, bool animate)
    {
        if (!animate)
        {
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = target;
            return;
        }

        transform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
