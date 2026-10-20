using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TempBar;

/// <summary>Uma barra de temperatura: rótulo, mínimo, máximo, apontador e valor atual.</summary>
public partial class TempGauge : UserControl, ITempView
{
    public const double CellWidth = 200, CellHeight = 44;

    private const double ValueTextHalfWidth = 20;   // metade da largura do ValueText no XAML

    private double _min = 30;
    private double _max = 90;
    private double? _temp;

    public TempGauge()
    {
        InitializeComponent();
        SizeChanged += (_, _) => MovePointer(animate: false);
    }

    public void Configure(string label, double min, double max)
    {
        LabelText.Text = label;
        _min = min;
        _max = max;
        MinText.Text = $"{min:0}°";
        MaxText.Text = $"{max:0}°";
        MovePointer(animate: false);
    }

    public void SetTemperature(double? temp)
    {
        _temp = temp;
        ValueText.Text = temp is double t ? $"{t:0}°C" : "--";
        MovePointer(animate: true);
    }

    private void MovePointer(bool animate)
    {
        if (_temp is not double temp) return;

        double range = _max - _min;
        double fraction = range <= 0 ? 0 : Math.Clamp((temp - _min) / range, 0, 1);
        double barWidth = BarArea.ActualWidth;
        double x = fraction * barWidth;
        double textX = Math.Clamp(x, ValueTextHalfWidth, Math.Max(ValueTextHalfWidth, barWidth - ValueTextHalfWidth))
                       - ValueTextHalfWidth;

        Shift(PointerShift, x, animate);
        Shift(ValueShift, textX, animate);
    }

    private static void Shift(TranslateTransform transform, double target, bool animate)
    {
        if (!animate)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = target;
            return;
        }

        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
