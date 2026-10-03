using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Screen = System.Windows.Forms.Screen;

namespace StudyRoomTools {
  public sealed class ToolApp : Application {
    readonly Dictionary<string,InkDocument> documents=new Dictionary<string,InkDocument>();
    readonly List<Overlay> overlays=new List<Overlay>();
    public Tool CurrentTool=Tool.Interact;
    public Color InkColor=Colors.Red;
    public double StrokeWidth=3,TextSize=28;
    public Toolbar Panel;
    public Screen[] Monitors;
    public int SelectedMonitor;
    public bool DemoMode;
    Overlay activeOverlay;
    Window prompt,demo;
    System.Windows.Forms.NotifyIcon tray;
    Mutex mutex;
    EventWaitHandle openEvent,demoEvent,stopEvent;
    bool ownsMutex,closing,escapeRegistered;
    bool backgroundStart,shareWasActive;
    public bool IsClosing {get{return closing;}}
    readonly ShareLeases leases=new ShareLeases();
    SharingBridge bridge;
    DispatcherTimer leaseTimer;
    [STAThread] public static int Main(string[] args) {
      var mode=CommandLine.Mode(args);bool demoMode=mode=="demo";
      if(mode=="invalid") {
        MessageBox.Show("StudyRoom solo acepta abrir sus herramientas. No se ejecutó ninguna acción.","Herramientas StudyRoom",MessageBoxButton.OK,MessageBoxImage.Information);return 2;
      }
      if(mode=="install"||mode=="uninstall") {
#if STORE_PACKAGE
        MessageBox.Show("Windows administra la instalación y desinstalación de esta aplicación de Microsoft Store.","StudyRoom",MessageBoxButton.OK,MessageBoxImage.Information);return 2;
#else
        return Setup.Run(mode=="install");
#endif
      }
      try {Native.SetProcessDpiAwarenessContext(new IntPtr(-4));} catch(EntryPointNotFoundException) {}
      var app=new ToolApp();app.backgroundStart=mode=="background";return app.Start(demoMode);
    }
    int Start(bool demoMode) {
      DemoMode=demoMode;
      var suffix=WindowsIdentity.GetCurrent().User.Value;
      mutex=new Mutex(true,"Local\\StudyRoom.Tools."+suffix,out ownsMutex);
      openEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\StudyRoom.Tools.Open."+suffix);
      demoEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\StudyRoom.Tools.Demo."+suffix);
      stopEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\StudyRoom.Tools.Exit."+suffix);
      if(!ownsMutex) {if(!backgroundStart)(demoMode?demoEvent:openEvent).Set();openEvent.Dispose();demoEvent.Dispose();stopEvent.Dispose();mutex.Dispose();return 0;}
      ShutdownMode=ShutdownMode.OnExplicitShutdown;
      DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {e.Handled=true;SetTool(Tool.Interact);MessageBox.Show("Las herramientas volvieron a Interactuar tras un error. Puedes cerrarlas y abrirlas de nuevo.","StudyRoom");};
      Startup+=delegate {
        RefreshMonitors();Panel=new Toolbar(this);MainWindow=Panel;Panel.Show();Panel.PlaceTopRight();
        var source=HwndSource.FromHwnd(Native.Handle(Panel));source.AddHook(Hook);
        CreateTray();SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        bridge=new SharingBridge(leases,delegate{Dispatcher.BeginInvoke(new Action(SyncSharing));});
        if(!bridge.Start())tray.ShowBalloonTip(5000,"StudyRoom","La apertura automática no está disponible porque el puerto local está ocupado. Puedes abrir las herramientas manualmente.",System.Windows.Forms.ToolTipIcon.Warning);
        leaseTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};leaseTimer.Tick+=delegate{SyncSharing();};leaseTimer.Start();
        new Thread(delegate() { while(true) {var signal=WaitHandle.WaitAny(new WaitHandle[]{openEvent,demoEvent,stopEvent});if(signal==2){if(!closing)Dispatcher.BeginInvoke(new Action(Quit));return;}Dispatcher.BeginInvoke(new Action(delegate {if(closing)return;if(signal==1)ShowDemo();ShowPanel();}));} }) {IsBackground=true,Name="StudyRoom activation"}.Start();
        if(demoMode)ShowDemo();
        else if(backgroundStart)HideTools();
      };
      Exit+=delegate {closing=true;stopEvent.Set();if(bridge!=null)bridge.Dispose();if(leaseTimer!=null)leaseTimer.Stop();SystemEvents.DisplaySettingsChanged-=DisplayChanged;if(Panel!=null)Native.UnregisterHotKey(Native.Handle(Panel),1);if(tray!=null)tray.Dispose();if(ownsMutex)mutex.ReleaseMutex();mutex.Dispose();};
      return Run();
    }
    void CreateTray() {
      tray=new System.Windows.Forms.NotifyIcon {Icon=System.Drawing.SystemIcons.Application,Text="Herramientas StudyRoom",Visible=true};
      var menu=new System.Windows.Forms.ContextMenuStrip();
      menu.Items.Add("Mostrar herramientas",null,delegate {Dispatcher.BeginInvoke(new Action(ShowPanel));});
      menu.Items.Add("Interactuar con Windows",null,delegate {Dispatcher.BeginInvoke(new Action(delegate {SetTool(Tool.Interact);}));});
      menu.Items.Add("Salir completamente",null,delegate {Dispatcher.BeginInvoke(new Action(Quit));});
      tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate {ShowPanel();};
    }
    void DisplayChanged(object sender,EventArgs args) {Dispatcher.BeginInvoke(new Action(delegate {SetTool(Tool.Interact);RefreshMonitors();if(Panel!=null){Panel.Rebuild();Panel.PlaceTopRight();}}));}
    void RefreshMonitors() {
      foreach(var overlay in overlays)overlay.Close();overlays.Clear();
      Monitors=Screen.AllScreens;SelectedMonitor=Math.Min(SelectedMonitor,Monitors.Length-1);
      foreach(var screen in Monitors) {InkDocument document;if(!documents.TryGetValue(screen.DeviceName,out document)){document=new InkDocument();documents.Add(screen.DeviceName,document);}var overlay=new Overlay(this,screen,document);overlays.Add(overlay);overlay.Show();}
      activeOverlay=overlays[SelectedMonitor];
    }
    IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) {
      if(msg==0x0312&&w.ToInt32()==1){SetTool(Tool.Interact);if(prompt!=null)prompt.Close();handled=true;}return IntPtr.Zero;
    }
    public void SetTool(Tool tool) {
      CurrentTool=tool;
      foreach(var overlay in overlays){overlay.CancelPreview();overlay.SetDrawing(tool!=Tool.Interact);}
      if(Panel!=null) {
        var handle=Native.Handle(Panel);if(escapeRegistered){Native.UnregisterHotKey(handle,1);escapeRegistered=false;}
        if(tool!=Tool.Interact)escapeRegistered=Native.RegisterHotKey(handle,1,0x4000,0x1B);
        Panel.UpdateStatus();
        Native.SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010);
      }
      ProtectControls();
    }
    public void ProtectControls() {
      var handles=new List<IntPtr>();
      if(Panel!=null)handles.AddRange(Panel.ProtectedHandles());
      if(prompt!=null&&prompt.IsVisible)handles.Add(Native.Handle(prompt));
      foreach(var overlay in overlays)Native.ExcludeWindows(Native.Handle(overlay),handles);
    }
    public void ShowPanel() {if(Panel==null)return;SetTool(Tool.Interact);foreach(var overlay in overlays)overlay.Show();Panel.Show();Panel.Activate();Panel.PlaceTopRight();}
    void SyncSharing() {
      if(closing||Panel==null)return;bool active=leases.Active(DateTime.UtcNow);
      if(active&&!shareWasActive){shareWasActive=true;SetTool(Tool.Interact);Panel.Collapse();foreach(var overlay in overlays)overlay.Show();Panel.Show();Panel.PlaceTopRight();}
      else if(!active&&shareWasActive){shareWasActive=false;foreach(var overlay in overlays){overlay.Document.Reset();overlay.Refresh();}HideTools();}
      else if(active&&!Panel.IsVisible){SetTool(Tool.Interact);Panel.Collapse();foreach(var overlay in overlays)overlay.Show();Panel.Show();Panel.PlaceTopRight();}
    }
    void HideTools(){SetTool(Tool.Interact);if(prompt!=null)prompt.Close();foreach(var overlay in overlays)overlay.Hide();if(Panel!=null)Panel.Hide();}
    public void ClosePanel(){if(leases.Active(DateTime.UtcNow)){SetTool(Tool.Interact);Panel.Collapse();Panel.Show();Panel.PlaceTopRight();}else HideTools();}
    public void Quit(){closing=true;Shutdown();}
    public void SelectMonitor(int index) {SelectedMonitor=Math.Max(0,Math.Min(index,Monitors.Length-1));activeOverlay=overlays[SelectedMonitor];Panel.PlaceTopRight();}
    public void Active(Overlay overlay) {activeOverlay=overlay;}
    public void Undo() {if(activeOverlay!=null){activeOverlay.Document.Undo();activeOverlay.Refresh();}}
    public void Redo() {if(activeOverlay!=null){activeOverlay.Document.Redo();activeOverlay.Refresh();}}
    public void Clear() {foreach(var overlay in overlays){overlay.Document.Clear();overlay.Refresh();}}
    public void AddText(Overlay overlay,Point location) {
      if(prompt!=null)return;
      var window=new Window {Title="Texto en pantalla",Width=370,Height=250,ResizeMode=ResizeMode.NoResize,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brushes.White};
      prompt=window;
      var layout=new StackPanel {Margin=new Thickness(16)};
      layout.Children.Add(new TextBlock {Text="Escribe el texto (máximo 200 caracteres)",Margin=new Thickness(0,0,0,8)});
      var input=new TextBox {AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=200,Height=100,FontSize=16,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};layout.Children.Add(input);
      var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
      var cancel=new Button {Content="Cancelar",Padding=new Thickness(12,6,12,6),Margin=new Thickness(4)};cancel.Click+=delegate {window.Close();};
      var accept=new Button {Content="Insertar texto",Padding=new Thickness(12,6,12,6),Margin=new Thickness(4)};
      accept.Click+=delegate {if(!String.IsNullOrWhiteSpace(input.Text)){var mark=NewMark(Tool.Text);mark.Text=input.Text.Trim();mark.Points.Add(location);overlay.Document.Add(mark);overlay.Refresh();}window.Close();};
      buttons.Children.Add(cancel);buttons.Children.Add(accept);layout.Children.Add(buttons);window.Content=layout;
      window.Closed+=delegate {prompt=null;ProtectControls();};window.LocationChanged+=delegate{ProtectControls();};window.SizeChanged+=delegate{ProtectControls();};window.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){window.Close();SetTool(Tool.Interact);}};
      window.Loaded+=delegate {input.Focus();ProtectControls();};window.Show();ProtectControls();
    }
    public Mark NewMark(Tool kind) {return new Mark {Kind=kind,Color=InkColor,Width=StrokeWidth,FontSize=TextSize};}
    public void ShowDemo() {
      if(demo!=null){demo.Activate();return;}
      if(!DemoMode){DemoMode=true;RefreshMonitors();}
      demo=new Window {Title="Prueba StudyRoom — fondo sin datos personales",WindowState=WindowState.Maximized,Background=new SolidColorBrush(Color.FromRgb(244,247,251))};
      var layout=new Grid {Margin=new Thickness(80)};var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
      stack.Children.Add(new TextBlock {Text="Prueba StudyRoom",FontSize=48,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(27,50,76))});
      stack.Children.Add(new TextBlock {Text="Un fondo de prueba para dibujar y comprobar las herramientas.\n\n1. Abre el botón verde de la esquina.\n2. Elige una herramienta y dibuja.\n3. Pulsa Escape para volver a usar Windows.\n\nComparte Pantalla completa para que se vean las marcas.",FontSize=22,LineHeight=34,Margin=new Thickness(0,24,0,24)});
      var close=new Button {Content="Cerrar fondo de prueba",Padding=new Thickness(16,10,16,10),HorizontalAlignment=HorizontalAlignment.Left};close.Click+=delegate {demo.Close();};stack.Children.Add(close);layout.Children.Add(stack);demo.Content=layout;
      demo.Closed+=delegate {demo=null;};demo.Show();Panel.Topmost=true;Panel.Activate();
    }
  }
  public sealed class Overlay : Window {
    readonly ToolApp app;readonly Surface surface;readonly Screen monitor;
    public InkDocument Document;
    Mark preview;
    bool drawing;
    public Overlay(ToolApp app,Screen monitor,InkDocument document) {
      this.app=app;this.monitor=monitor;Document=document;Title="StudyRoom: marcas en "+monitor.DeviceName;
      WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=app.DemoMode;ShowActivated=false;
      surface=new Surface(this);Content=surface;
      SourceInitialized+=delegate {var source=HwndSource.FromHwnd(Native.Handle(this));source.AddHook(MouseActivationHook);var b=monitor.Bounds;Native.SetWindowPos(Native.Handle(this),new IntPtr(-1),b.Left,b.Top,b.Width,b.Height,0x0010);Native.PassThrough(this,true,!app.DemoMode);};
      Loaded+=delegate {var b=monitor.Bounds;Native.SetWindowPos(Native.Handle(this),new IntPtr(-1),b.Left,b.Top,b.Width,b.Height,0x0010);};
      surface.MouseLeftButtonDown+=Down;surface.MouseMove+=Move;surface.MouseLeftButtonUp+=Up;
      surface.LostMouseCapture+=delegate {if(preview!=null){preview=null;Refresh();}};
      PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){app.SetTool(Tool.Interact);e.Handled=true;}};
      SizeChanged+=delegate {Refresh();};
    }
    IntPtr MouseActivationHook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
      if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;
    }
    public void SetDrawing(bool value) {drawing=value;Native.PassThrough(this,!value,!app.DemoMode);surface.Cursor=value?(app.CurrentTool==Tool.Text?Cursors.IBeam:app.CurrentTool==Tool.Eraser?Cursors.Cross:Cursors.Pen):Cursors.Arrow;Refresh();}
    public void Refresh(){surface.InvalidateVisual();}
    public void CancelPreview(){preview=null;if(surface.IsMouseCaptured)surface.ReleaseMouseCapture();Refresh();}
    Point Position(MouseEventArgs e){return Geometry.Normalize(e.GetPosition(surface),surface.ActualWidth,surface.ActualHeight);}
    void Down(object sender,MouseButtonEventArgs e) {
      if(!drawing)return;app.Active(this);var point=Position(e);
      if(app.CurrentTool==Tool.Eraser){Document.Erase(e.GetPosition(surface),surface.ActualWidth,surface.ActualHeight);Refresh();return;}
      if(app.CurrentTool==Tool.Text){app.AddText(this,point);return;}
      preview=app.NewMark(app.CurrentTool);preview.Points.Add(point);if(app.CurrentTool!=Tool.Pen)preview.Points.Add(point);surface.CaptureMouse();Refresh();e.Handled=true;
    }
    void Move(object sender,MouseEventArgs e) {
      if(preview==null||e.LeftButton!=MouseButtonState.Pressed)return;var point=Position(e);
      if(preview.Kind==Tool.Pen){if(preview.Points.Count<3000&&(Geometry.Pixel(point,surface.ActualWidth,surface.ActualHeight)-Geometry.Pixel(preview.Points[preview.Points.Count-1],surface.ActualWidth,surface.ActualHeight)).Length>=1.5)preview.Points.Add(point);}
      else preview.Points[1]=point;Refresh();
    }
    void Up(object sender,MouseButtonEventArgs e) {if(preview!=null){Document.Add(preview);preview=null;}if(surface.IsMouseCaptured)surface.ReleaseMouseCapture();Refresh();}
    sealed class Surface:FrameworkElement {
      readonly Overlay overlay;
      public Surface(Overlay overlay){this.overlay=overlay;Focusable=true;}
      protected override void OnRender(DrawingContext dc) {
        if(overlay.drawing)dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1,0,0,0)),null,new Rect(0,0,ActualWidth,ActualHeight));
        foreach(var mark in overlay.Document.Marks)Draw(dc,mark);if(overlay.preview!=null)Draw(dc,overlay.preview);
      }
      void Draw(DrawingContext dc,Mark mark) {
        if(mark.Points.Count==0)return;var brush=new SolidColorBrush(mark.Color);var pen=new Pen(brush,mark.Width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        var a=Geometry.Pixel(mark.Points[0],ActualWidth,ActualHeight);var b=Geometry.Pixel(mark.Points[mark.Points.Count-1],ActualWidth,ActualHeight);
        if(mark.Kind==Tool.Text){var text=new FormattedText(mark.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),mark.FontSize,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);text.MaxTextWidth=Math.Max(40,ActualWidth-a.X);dc.DrawText(text,a);return;}
        if(mark.Kind==Tool.Rectangle){dc.DrawRectangle(null,pen,Geometry.Bounds(a,b));return;}
        if(mark.Kind==Tool.Ellipse){var r=Geometry.Bounds(a,b);dc.DrawEllipse(null,pen,new Point(r.Left+r.Width/2,r.Top+r.Height/2),r.Width/2,r.Height/2);return;}
        if(mark.Points.Count==1){dc.DrawEllipse(brush,null,a,mark.Width/2,mark.Width/2);return;}
        var shape=new StreamGeometry();using(var context=shape.Open()){context.BeginFigure(a,false,false);for(int i=1;i<mark.Points.Count;i++)context.LineTo(Geometry.Pixel(mark.Points[i],ActualWidth,ActualHeight),true,false);}dc.DrawGeometry(null,pen,shape);
        if(mark.Kind==Tool.Arrow){var wings=Geometry.ArrowWings(a,b,mark.Width);dc.DrawLine(pen,b,wings[0]);dc.DrawLine(pen,b,wings[1]);}
      }
    }
  }
  public sealed class Toolbar:Window {
    readonly ToolApp app;
    readonly Brush dark=new SolidColorBrush(Color.FromRgb(28,44,62));
    TextBlock status;
    readonly Dictionary<Tool,Button> tools=new Dictionary<Tool,Button>();
    readonly List<ComboBox> combos=new List<ComboBox>();
    bool expanded;
    public Toolbar(ToolApp app) {
      this.app=app;Title="Herramientas StudyRoom";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=true;SizeToContent=SizeToContent.WidthAndHeight;FontFamily=new FontFamily("Segoe UI");FontSize=13;
      MinWidth=0;MinHeight=0;
      SourceInitialized+=delegate{HwndSource.FromHwnd(Native.Handle(this)).AddHook(SizingHook);};
      LocationChanged+=delegate{app.ProtectControls();};SizeChanged+=delegate{app.ProtectControls();};IsVisibleChanged+=delegate{app.ProtectControls();};
      Rebuild();Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!app.IsClosing&&!Dispatcher.HasShutdownStarted){e.Cancel=true;app.ClosePanel();}};PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape){app.SetTool(Tool.Interact);e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z){app.Undo();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Y){app.Redo();e.Handled=true;}};
    }
    Button Button(string label,Action action) {var button=new Button {Content=label,Padding=new Thickness(8,7,8,7),Margin=new Thickness(3),MinHeight=34,Foreground=dark,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(208,218,229))};button.Click+=delegate{action();};return button;}
    IntPtr SizingHook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
      if(message==0x24){var limits=(Native.MINMAXINFO)Marshal.PtrToStructure(l,typeof(Native.MINMAXINFO));limits.MinTrackSize.X=1;limits.MinTrackSize.Y=1;Marshal.StructureToPtr(limits,l,false);handled=true;}return IntPtr.Zero;
    }
    public void Rebuild() {
      tools.Clear();
      combos.Clear();
      var border=new Border {Background=new SolidColorBrush(Color.FromRgb(248,250,253)),BorderBrush=new SolidColorBrush(Color.FromRgb(183,199,216)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(expanded?10:4)};
      if(!expanded) {SizeToContent=SizeToContent.Manual;Width=58;Height=58;border.Background=new SolidColorBrush(Color.FromRgb(33,92,63));border.BorderBrush=new SolidColorBrush(Color.FromRgb(179,213,163));var open=Button("✎",delegate{Toggle(true);});open.Width=48;open.Height=48;open.Margin=new Thickness(0);open.FontWeight=FontWeights.Bold;open.FontSize=28;open.Foreground=Brushes.White;open.Background=Brushes.Transparent;open.BorderThickness=new Thickness(0);open.ToolTip="Abrir herramientas StudyRoom";System.Windows.Automation.AutomationProperties.SetName(open,"Abrir herramientas StudyRoom");border.Child=open;Content=border;return;}
      Width=Double.NaN;Height=Double.NaN;SizeToContent=SizeToContent.WidthAndHeight;
      var stack=new StackPanel {Width=320};
      var header=new DockPanel();var collapse=Button("−",delegate{Toggle(false);});collapse.ToolTip="Contraer";DockPanel.SetDock(collapse,Dock.Right);header.Children.Add(collapse);
      var close=Button("×",app.ClosePanel);close.ToolTip="Ocultar herramientas (vuelven mientras compartes)";DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
      var drag=new TextBlock {Text="STUDYROOM  ·  arrastrar",FontWeight=FontWeights.Bold,Foreground=dark,VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.SizeAll,Margin=new Thickness(4)};drag.MouseLeftButtonDown+=delegate{try{DragMove();}catch(InvalidOperationException){}};header.Children.Add(drag);stack.Children.Add(header);
      status=new TextBlock {Margin=new Thickness(4,7,4,7),TextWrapping=TextWrapping.Wrap,Foreground=dark};stack.Children.Add(status);
      var interact=Button("Interactuar con Windows  ·  Esc",delegate{app.SetTool(Tool.Interact);});tools.Add(Tool.Interact,interact);stack.Children.Add(interact);
      var grid=new UniformGrid {Columns=3};
      AddTool(grid,"Lápiz",Tool.Pen);AddTool(grid,"Borrador",Tool.Eraser);AddTool(grid,"Texto",Tool.Text);AddTool(grid,"Rectángulo",Tool.Rectangle);AddTool(grid,"Elipse",Tool.Ellipse);AddTool(grid,"Línea",Tool.Line);AddTool(grid,"Flecha",Tool.Arrow);
      grid.Children.Add(Button("Deshacer",app.Undo));grid.Children.Add(Button("Rehacer",app.Redo));stack.Children.Add(grid);
      var colors=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(3,8,3,4)};colors.Children.Add(new TextBlock {Text="Color",VerticalAlignment=VerticalAlignment.Center,Width=42,Foreground=dark});
      var palette=new Color[]{Colors.Red,Color.FromRgb(255,196,0),Color.FromRgb(18,160,87),Color.FromRgb(24,104,225),Color.FromRgb(151,66,210),Colors.White,Color.FromRgb(24,28,36)};
      foreach(var color in palette){var selected=color;var button=Button("",delegate{app.InkColor=selected;});button.Background=new SolidColorBrush(color);button.Width=29;button.MinHeight=27;button.Height=27;button.Padding=new Thickness(0);button.ToolTip="Color "+color.ToString();colors.Children.Add(button);}stack.Children.Add(colors);
      var widths=new DockPanel {Margin=new Thickness(4,6,4,6)};widths.Children.Add(new TextBlock {Text="Grosor",Width=65,VerticalAlignment=VerticalAlignment.Center,Foreground=dark});var width=new ComboBox {ItemsSource=new int[]{2,3,5,8,12},SelectedItem=(int)app.StrokeWidth,MinWidth=74};width.SelectionChanged+=delegate{if(width.SelectedItem!=null)app.StrokeWidth=(int)width.SelectedItem;};TrackCombo(width);widths.Children.Add(width);stack.Children.Add(widths);
      var sizes=new DockPanel {Margin=new Thickness(4,2,4,6)};sizes.Children.Add(new TextBlock {Text="Texto",Width=65,VerticalAlignment=VerticalAlignment.Center,Foreground=dark});var size=new ComboBox {ItemsSource=new int[]{18,24,28,36,48,64},SelectedItem=(int)app.TextSize,MinWidth=74};size.SelectionChanged+=delegate{if(size.SelectedItem!=null)app.TextSize=(int)size.SelectedItem;};TrackCombo(size);sizes.Children.Add(size);stack.Children.Add(sizes);
      var screens=new ComboBox {Margin=new Thickness(4,5,4,5),MinHeight=29};for(int i=0;i<app.Monitors.Length;i++)screens.Items.Add("Panel en monitor "+(i+1)+(app.Monitors[i].Primary?" · principal":""));screens.SelectedIndex=app.SelectedMonitor;screens.SelectionChanged+=delegate{if(screens.SelectedIndex>=0)app.SelectMonitor(screens.SelectedIndex);};TrackCombo(screens);stack.Children.Add(screens);
      stack.Children.Add(Button("Borrar todo en las pantallas",app.Clear));
#if STORE_PACKAGE
      stack.Children.Add(Button("Aplicación de Microsoft Store",delegate{app.SetTool(Tool.Interact);MessageBox.Show("La integración con StudyRoom la administra Windows. Las herramientas estarán disponibles mientras la aplicación esté abierta. Puedes gestionar su inicio automático en Configuración de Windows.","StudyRoom",MessageBoxButton.OK,MessageBoxImage.Information);}));
#else
      stack.Children.Add(Button("Integrar con StudyRoom",delegate{app.SetTool(Tool.Interact);Setup.Run(true);}));
#endif
      stack.Children.Add(new TextBlock {Text="Puedes cambiar herramientas directamente en este menú.\nComparte Pantalla completa para mostrar las marcas.\nEsc vuelve a Interactuar. El borrador elimina un objeto.",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=dark,Margin=new Thickness(5,7,5,3)});
      border.Child=stack;Content=border;UpdateStatus();
    }
    void TrackCombo(ComboBox combo) {
      combos.Add(combo);
      EventHandler update=delegate{Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(app.ProtectControls));};
      combo.DropDownOpened+=update;combo.DropDownClosed+=update;
    }
    public IEnumerable<IntPtr> ProtectedHandles() {
      if(!IsVisible)yield break;
      yield return Native.Handle(this);
      foreach(var combo in combos) {
        if(!combo.IsDropDownOpen||combo.Template==null)continue;
        var popup=combo.Template.FindName("PART_Popup",combo) as Popup;
        if(popup==null||popup.Child==null)continue;
        var source=PresentationSource.FromVisual(popup.Child) as HwndSource;
        if(source!=null)yield return source.Handle;
      }
    }
    void AddTool(Panel grid,string label,Tool tool) {var button=Button(label,delegate{app.SetTool(tool);});tools.Add(tool,button);grid.Children.Add(button);}
    void Toggle(bool value) {app.SetTool(Tool.Interact);expanded=value;Rebuild();Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(PlaceTopRight));}
    public void Collapse(){Toggle(false);}
    public void UpdateStatus() {
      if(status!=null)status.Text=app.CurrentTool==Tool.Interact?"Puedes usar el escritorio normalmente.":"Dibujando sobre el escritorio · Esc para salir";
      foreach(var item in tools)item.Value.Background=item.Key==app.CurrentTool?new SolidColorBrush(Color.FromRgb(207,235,255)):Brushes.White;
    }
    public void PlaceTopRight() {
      if(!IsLoaded)return;UpdateLayout();var work=app.Monitors[app.SelectedMonitor].WorkingArea;Native.RECT rectangle;Native.GetWindowRect(Native.Handle(this),out rectangle);var width=rectangle.Right-rectangle.Left;var height=rectangle.Bottom-rectangle.Top;
      Native.SetWindowPos(Native.Handle(this),new IntPtr(-1),Math.Max(work.Left,work.Right-width-18),Math.Max(work.Top,work.Top+18),width,height,0x0010);
      app.ProtectControls();
    }
  }
}
