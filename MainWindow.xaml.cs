using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TempBar;

public partial class MainWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private readonly TemperatureReader _reader = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _reading;
    private bool _ready;   // evita que eventos disparados na inicialização sobrescrevam as configurações
    private System.Windows.Forms.NotifyIcon? _tray;
    private UserControl? _cpuView, _gpuView;   // indicadores atuais (barra, velocímetro ou círculo)
    private double? _lastCpu, _lastGpu;

    // Mantém a janela acima de tudo (barra de tarefas, jogos em tela cheia sem borda):
    // reafirmamos o Topmost sem ativar a janela, a cada troca de janela em primeiro plano
    // e também num timer curto, pois outras janelas topmost podem passar na frente.
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    private delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10, SwpShowWindow = 0x40;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    private const uint EventSystemForeground = 0x0003, WinEventOutOfContext = 0;

    private readonly DispatcherTimer _topTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private WinEventProc? _foregroundProc;   // mantido em campo para não ser coletado pelo GC
    private IntPtr _foregroundHook;

    private void KeepOnTop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && IsVisible)
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        // clicar na barra não rouba o foco do jogo; também some do Alt+Tab
        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));

        _foregroundProc = (_, _, _, _, _, _, _) => Dispatcher.BeginInvoke(KeepOnTop);
        _foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero,
            _foregroundProc, 0, 0, WinEventOutOfContext);

        _topTimer.Tick += (_, _) => KeepOnTop();
        _topTimer.Start();
    }

    public MainWindow()
    {
        InitializeComponent();
        // iniciado pelo Windows: sobe na última posição e bloqueado
        if (Environment.GetCommandLineArgs().Contains(Autostart.Argument)) _settings.Locked = true;
        AutostartItem.IsChecked = Autostart.IsEnabled();
        if (AutostartItem.IsChecked) Autostart.Enable();   // atualiza o caminho caso o .exe tenha sido movido
        ApplyLayout();      // define o tamanho da janela, usado por RestorePosition
        RestorePosition();
        ApplyLockState();
        ApplyOpacity();

        _timer.Tick += async (_, _) => await UpdateTemperatureAsync();
        _timer.Start();
        _ = UpdateTemperatureAsync();
        _ready = true;
    }

    // ---------- temperatura ----------

    private async Task UpdateTemperatureAsync()
    {
        if (_reading) return;
        _reading = true;
        try
        {
            var (cpu, gpu) = await Task.Run(() => (_reader.Read("CPU"), _reader.Read("GPU")));
            _lastCpu = cpu;
            _lastGpu = gpu;
            (_cpuView as ITempView)?.SetTemperature(cpu);
            (_gpuView as ITempView)?.SetTemperature(gpu);
        }
        finally
        {
            _reading = false;
        }
    }

    // ---------- arrastar ----------

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.Locked) return;
        try { DragMove(); } catch (InvalidOperationException) { }
        SavePosition(); // DragMove só retorna quando o botão é solto
    }

    // ---------- menu ----------

    private void LockItem_Click(object sender, RoutedEventArgs e)
    {
        _settings.Locked = !_settings.Locked;
        ApplyLockState();
        _settings.Save();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _settings.OpacityPercent = e.NewValue;
        ApplyOpacity();
        _settings.Save();
    }

    private void Layout_Click(object sender, RoutedEventArgs e)
    {
        _settings.Layout = Enum.Parse<LayoutStyle>((string)((MenuItem)sender).Tag);
        ApplyLayout();
        _settings.Save();
    }

    private void Orientation_Click(object sender, RoutedEventArgs e)
    {
        _settings.SideBySide = (string)((MenuItem)sender).Tag == "H";
        ApplyOrientation();
        _settings.Save();
    }

    private void AutostartItem_Click(object sender, RoutedEventArgs e)
    {
        bool want = AutostartItem.IsChecked;
        if (want)
        {
            // iniciar com o Windows sempre começa na última posição, bloqueada
            _settings.Locked = true;
            ApplyLockState();
            SavePosition();
        }
        bool ok = want ? Autostart.Enable() : Autostart.Disable();
        if (!ok)
        {
            AutostartItem.IsChecked = !want;
            MessageBox.Show("Não foi possível alterar o início automático com o Windows.", "TempBar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TrayItem_Click(object sender, RoutedEventArgs e) => MinimizeToTray();

    private void ExitItem_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyLockState()
    {
        LockItem.Header = _settings.Locked ? "Desbloquear posição" : "Bloquear posição";
        Root.Cursor = _settings.Locked ? Cursors.Arrow : Cursors.SizeAll;
    }

    private (double Width, double Height) CellSize() => _settings.Layout switch
    {
        LayoutStyle.Dial => (TempDial.CellWidth, TempDial.CellHeight),
        LayoutStyle.Circle => (TempCircle.CellWidth, TempCircle.CellHeight),
        _ => (TempGauge.CellWidth, TempGauge.CellHeight)
    };

    private UserControl CreateView() => _settings.Layout switch
    {
        LayoutStyle.Dial => new TempDial(),
        LayoutStyle.Circle => new TempCircle(),
        _ => new TempGauge()
    };

    /// <summary>Recria os indicadores de CPU e GPU no estilo escolhido e reaplica a orientação.</summary>
    private void ApplyLayout()
    {
        BarItem.IsChecked = _settings.Layout == LayoutStyle.Bar;
        DialItem.IsChecked = _settings.Layout == LayoutStyle.Dial;
        CircleItem.IsChecked = _settings.Layout == LayoutStyle.Circle;

        if (_cpuView != null) Root.Children.Remove(_cpuView);
        if (_gpuView != null) Root.Children.Remove(_gpuView);

        _cpuView = CreateView();
        _gpuView = CreateView();
        ((ITempView)_cpuView).Configure("CPU", _settings.MinTemp, _settings.MaxTemp);
        ((ITempView)_gpuView).Configure("GPU", _settings.MinTemp, _settings.MaxTemp);
        ((ITempView)_cpuView).SetTemperature(_lastCpu);
        ((ITempView)_gpuView).SetTemperature(_lastGpu);
        Root.Children.Add(_cpuView);
        Root.Children.Add(_gpuView);

        ApplyOrientation();
    }

    private void ApplyOrientation()
    {
        bool side = _settings.SideBySide;
        VerticalItem.IsChecked = !side;
        HorizontalItem.IsChecked = side;

        var (w, h) = CellSize();
        FirstRow.Height = new GridLength(h);
        FirstColumn.Width = new GridLength(w);
        SecondRow.Height = new GridLength(side ? 0 : h);
        SecondColumn.Width = new GridLength(side ? w : 0);

        Grid.SetRow(_cpuView!, 0);
        Grid.SetColumn(_cpuView!, 0);
        Grid.SetRow(_gpuView!, side ? 0 : 1);
        Grid.SetColumn(_gpuView!, side ? 1 : 0);

        Width = side ? w * 2 : w;
        Height = side ? h : h * 2;
    }

    private void ApplyOpacity()
    {
        double percent = Math.Clamp(_settings.OpacityPercent, 20, 100);   // nunca some por completo
        Opacity = percent / 100;
        OpacityLabel.Text = $"Transparência: {percent:0}%";
        if (OpacitySlider.Value != percent) OpacitySlider.Value = percent;
    }

    // ---------- bandeja ----------

    private void MinimizeToTray()
    {
        if (_tray == null)
        {
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/TempBar.ico")).Stream;
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Mostrar TempBar", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Sair", null, (_, _) => Close());

            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(stream),
                Text = "TempBar",
                ContextMenuStrip = menu
            };
            _tray.MouseClick += (_, args) =>
            {
                if (args.Button == System.Windows.Forms.MouseButtons.Left) RestoreFromTray();
            };
        }

        _tray.Visible = true;
        Hide();
    }

    private void RestoreFromTray()
    {
        Show();
        KeepOnTop();
        if (_tray != null) _tray.Visible = false;
    }

    // ---------- posição ----------

    private void RestorePosition()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (_settings.Left is double l && _settings.Top is double t
            && l >= SystemParameters.VirtualScreenLeft
            && t >= SystemParameters.VirtualScreenTop
            && l <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width
            && t <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height)
        {
            Left = l;
            Top = t;
        }
        else
        {
            // primeira execução (ou posição fora da tela): topo central da tela principal
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = 8;
        }
    }

    private void SavePosition()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Save();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _topTimer.Stop();
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        SavePosition();
        _reader.Dispose();
        _tray?.Dispose();
        base.OnClosed(e);
    }
}
