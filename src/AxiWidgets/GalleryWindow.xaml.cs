using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AxiWidgets.Core;

namespace AxiWidgets;

internal partial class GalleryWindow : Window
{
    private readonly Manager manager;
    private readonly DispatcherTimer timer=new();
    private bool allowClose;
    internal nint Handle { get; private set; }
    internal bool PreviewTimerRunning => timer.IsEnabled;
    internal GalleryWindow(Manager manager)
    {
        this.manager=manager;
        InitializeComponent();
        SourceInitialized+=(_,_)=> {
            Handle=new WindowInteropHelper(this).Handle;
            DesktopHost.ConfigureGallery(Handle);
            HwndSource.FromHwnd(Handle)?.AddHook(Hook);
            ApplyBackdrop(); Place();
        };
        SizeChanged+=(_,_)=> { if(Handle!=0) DesktopHost.RoundGallery(Handle); };
        IsVisibleChanged+=(_,_)=> { if(IsVisible) RefreshPreview(); else timer.Stop(); };
        timer.Tick+=(_,_)=>RefreshPreview();
        Closing+=PreventClose;
        Closed+=(_,_)=> {timer.Stop();HwndSource.FromHwnd(Handle)?.RemoveHook(Hook);};
    }
    internal void Open()
    {
        if(IsVisible) { Activate(); return; }
        AddedStatus.Text="";
        Show(); Place(); Activate(); Search.Focus();
    }
    internal void ApplyBackdrop()
    {
        DesktopHost.GalleryBackdrop(Handle);
        Surface.Background=new SolidColorBrush(Color.FromRgb(32,32,36));
        DesktopHost.RoundGallery(Handle);
    }
    internal void Place()
    {
        if(Handle==0) return;
        Native.GetCursorPos(out var cursor);
        var m=Display.For(new(cursor.X,cursor.Y,1,1));
        int width=(int)Math.Min(1000*m.Scale,m.Work.Width-32*m.Scale);
        int height=(int)Math.Min(520*m.Scale,m.Work.Height-48*m.Scale);
        Native.SetWindowPos(Handle,-1,(int)(m.Work.X+(m.Work.Width-width)/2),(int)(m.Work.Bottom-height-40*m.Scale),width,height,0x10);
        DesktopHost.RoundGallery(Handle);
    }
    private void RefreshPreview()
    {
        timer.Stop(); var now=DateTime.Now;
        MinutesPreview.Text=now.ToString("HH:mm",CultureInfo.CurrentCulture);
        SecondsPreview.Text=now.ToString("HH:mm:ss",CultureInfo.CurrentCulture);
        DatePreview.Text=SecondsDatePreview.Text=now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture);
        if(IsVisible && Previews.Visibility==Visibility.Visible) {timer.Interval=ClockSchedule.Next(now,true);timer.Start();}
    }
    private void SearchChanged(object sender,TextChangedEventArgs e)
    {
        if(Previews==null) return;
        string query=Search.Text.Trim();
        bool found=query.Length==0||"clock digital time seconds часы время".Contains(query,StringComparison.CurrentCultureIgnoreCase);
        SearchPlaceholder.Visibility=query.Length==0?Visibility.Visible:Visibility.Collapsed;
        ClockCategory.Visibility=Previews.Visibility=found?Visibility.Visible:Visibility.Collapsed;
        EmptySearch.Visibility=found?Visibility.Collapsed:Visibility.Visible;
        RefreshPreview();
    }
    private void SelectClock(object sender,RoutedEventArgs e) { Search.Clear(); }
    private void AddDigital(object sender,RoutedEventArgs e) { manager.AddFromGallery(false); AddedStatus.Text="Digital Clock added to your desktop"; }
    private void AddSeconds(object sender,RoutedEventArgs e) { manager.AddFromGallery(true); AddedStatus.Text="Clock with seconds added to your desktop"; }
    private void DoneClicked(object sender,RoutedEventArgs e) { manager.FinishEditing(); }
    internal void Dismiss() { Hide(); timer.Stop(); }
    internal void ShutdownGallery() { allowClose=true; Close(); }
    private void PreventClose(object? sender,CancelEventArgs e) { e.Cancel=!allowClose; }
    private nint Hook(nint hwnd,int msg,nint w,nint l,ref bool handled)
    {
        // No Alt+F4, system-menu move/resize/minimize, Escape or outside-click dismissal.
        if(!allowClose && msg==0x112 && ((long)w&0xfff0) is 0xf060 or 0xf010 or 0xf000 or 0xf020 or 0xf030) {handled=true;return 0;}
        if(msg==0x2e0) Dispatcher.BeginInvoke(()=>Place());
        return 0;
    }
}
