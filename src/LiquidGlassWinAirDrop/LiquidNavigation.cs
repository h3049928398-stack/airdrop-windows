using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LiquidGlassWinAirDrop;

public sealed class LiquidNavigation : UserControl
{
    private readonly Image _body = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Image _lens = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button[] _buttons = new Button[4];
    private LiquidMaterial.Backdrop? _backdrop;
    private bool _animating, _motion = true, _reducedTransparency, _down, _dragging;
    private double _downX;
    private double _position, _target, _velocity, _lastTime, _press, _pointer = 0.3;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    public int SelectedIndex { get; private set; }
    public bool IsAnimating => _animating;
    public event Action<int>? SelectionChanged;

    public LiquidNavigation()
    {
        Height = 68;
        var root = new Grid { Background = Brushes.Transparent };
        root.Children.Add(new Border { CornerRadius = new(32), Margin = new(0,3,0,3), Background = new SolidColorBrush(Color.FromArgb(230,242,243,246)), Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.12, Color = Color.FromRgb(50,53,65) } });
        _body.Margin = new(0,3,0,3); root.Children.Add(_body); root.Children.Add(_lens);
        root.Children.Add(new Border { CornerRadius = new(32), Margin = new(0,3,0,3), BorderThickness = new(0.8), IsHitTestVisible = false, BorderBrush = new LinearGradientBrush(Color.FromArgb(240,255,255,255), Color.FromArgb(90,150,152,166),90) });
        var tabs = new UniformGrid { Columns = 4, Margin = new(5,4,5,4) };
        string[] names = ["接收", "文件", "连接", "设置"];
        string[] icons = ["M 5,14 A 9,9 0 1 1 19,14 M 8,12 A 5,5 0 1 1 16,12 M 7,20 L 12,12 L 17,20 Z", "M 2,5 L 9,5 L 12,8 L 22,8 L 22,20 L 2,20 Z", "M 2,8 Q 12,-1 22,8 M 5,12 Q 12,5 19,12 M 9,16 Q 12,13 15,16 M 12,20 L 12,20.1", "M 12,2 L 14,2 L 15,6 L 18,7 L 21,6 L 23,9 L 20,12 L 20,15 L 22,17 L 20,20 L 16,19 L 14,22 L 10,22 L 8,19 L 4,20 L 2,17 L 4,14 L 4,11 L 2,8 L 5,5 L 8,6 L 10,2 Z M 16,12 A 4,4 0 1 1 8,12 A 4,4 0 1 1 16,12"];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new Path { Data = Geometry.Parse(icons[i]), StrokeThickness = 1.5, Stretch = Stretch.Uniform, Width = 21, Height = 21, HorizontalAlignment = HorizontalAlignment.Center, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            stack.Children.Add(new TextBlock { Text = names[i], FontSize = 10, Margin = new(0,4,0,0), HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = stack, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Padding = new(0) };
            AutomationProperties.SetName(button, names[i]);
            button.Click += (_, _) => Select(index);
            button.PreviewMouseLeftButtonDown += (_, e) => { _down = true; _downX = e.GetPosition(this).X; _press = 1; RenderLens(); };
            button.PreviewMouseLeftButtonUp += (_, _) => { _press = 0; RenderLens(); };
            button.LostMouseCapture += (_, _) => { _press = 0; RenderLens(); };
            button.MouseLeave += (_, _) => { _press = 0; RenderLens(); };
            button.GotKeyboardFocus += (_, _) => Select(index);
            button.PreviewKeyDown += (_, e) =>
            {
                int next = e.Key switch { Key.Left => Math.Max(0,SelectedIndex-1), Key.Right => Math.Min(3,SelectedIndex+1), Key.Home => 0, Key.End => 3, _ => -1 };
                if (next >= 0) { Select(next); _buttons[next].Focus(); e.Handled = true; }
            };
            _buttons[i] = button; tabs.Children.Add(button);
        }
        root.Children.Add(tabs); Content = root;
        SizeChanged += (_, _) => { StopAnimation(); _position = _target = Slot * SelectedIndex; RenderAll(); };
        MouseMove += (_, e) =>
        {
            double x = e.GetPosition(this).X;
            _pointer = x / Math.Max(1,ActualWidth);
            if (_down && e.LeftButton == MouseButtonState.Pressed && Math.Abs(x-_downX) > 4 && !_dragging)
            { _dragging = true; StopAnimation(); CaptureMouse(); }
            if (_dragging)
            { _position = Math.Clamp(x-5-Slot*0.5,0,Slot*3); _press = 1; RenderLens(); e.Handled = true; }
            else if (_press > 0) RenderLens();
        };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            _down = false; _press = 0;
            if (_dragging)
            {
                int selected = Math.Clamp((int)((e.GetPosition(this).X-5)/Slot),0,3);
                _dragging = false; ReleaseMouseCapture(); Select(selected); e.Handled = true;
            }
            else RenderLens();
        };
        LostMouseCapture += (_, e) =>
        {
            if (e.OriginalSource != this || IsMouseCaptured) return;
            _down = false; _press = 0; if (_dragging) { _dragging = false; Select(SelectedIndex); }
        };
        Unloaded += (_, _) => StopAnimation();
        UpdateLabels();
    }
    private double Slot => Math.Max(1,(ActualWidth-10)/4);
    public void SetBackdrop(BitmapSource backdrop) { _backdrop = LiquidMaterial.Read(backdrop,4); RenderAll(); }
    public void SetPreferences(bool motion, bool reducedTransparency)
    {
        _motion = motion && SystemParameters.ClientAreaAnimation; _reducedTransparency = reducedTransparency;
        if (!_motion) { StopAnimation(); _position = _target; }
        RenderAll();
    }
    public void Select(int index, bool animate = true)
    {
        index = Math.Clamp(index,0,3); var changed = SelectedIndex != index;
        SelectedIndex = index; _target = Slot * index; UpdateLabels();
        if (_motion && animate && Math.Abs(_position-_target) > 0.1)
        {
            if (!_animating) { _lastTime = _clock.Elapsed.TotalSeconds; CompositionTarget.Rendering += Tick; _animating = true; }
        }
        else { StopAnimation(); _position = _target; RenderLens(); }
        if (changed) SelectionChanged?.Invoke(index);
    }
    private void Tick(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds, dt = Math.Clamp(now-_lastTime,0.001,0.032); _lastTime = now;
        _velocity += ((_target-_position)*260-_velocity*27)*dt; _position += _velocity*dt;
        if (Math.Abs(_target-_position) < 0.08 && Math.Abs(_velocity) < 0.3) { _position = _target; StopAnimation(); }
        RenderLens();
    }
    private void StopAnimation() { if (_animating) CompositionTarget.Rendering -= Tick; _animating = false; _velocity = 0; }
    private void RenderAll()
    {
        if (_backdrop is null || ActualWidth < 10) return;
        _body.Source = LiquidMaterial.Draw(_backdrop,(int)ActualWidth,62,0,3,31,4.5,_reducedTransparency ? 0.97 : 0.60);
        RenderLens();
    }
    private void RenderLens()
    {
        if (_backdrop is null || ActualWidth < 10) return;
        int width = Math.Max(1,(int)(Slot-2)); double x = Math.Clamp(5+_position+1,4,ActualWidth-width-4);
        _lens.Width = width; _lens.Height = 53; _lens.Margin = new(x,0,0,0);
        _lens.Source = LiquidMaterial.Draw(_backdrop,width,53,x,7.5,26.5,7.5,_reducedTransparency ? 0.97 : 0.42,_pointer,_press);
        _lens.Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.16, Color = Color.FromRgb(90,95,110) };
    }
    private void UpdateLabels()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            var brush = new SolidColorBrush(i == SelectedIndex ? Color.FromRgb(33,100,213) : Color.FromRgb(103,105,116));
            var panel = (StackPanel)_buttons[i].Content;
            ((Path)panel.Children[0]).Stroke = brush; ((TextBlock)panel.Children[1]).Foreground = brush;
            ((TextBlock)panel.Children[1]).FontWeight = i == SelectedIndex ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
}
