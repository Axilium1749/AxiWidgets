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
    private string category="Clock";
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
        if(Previews==null||SpecialPreviews==null||SystemPreviewButton==null||SshPreviewButton==null)return;
        string query=Search.Text.Trim();
        bool Match(string text)=>query.Length==0||text.Contains(query,StringComparison.CurrentCultureIgnoreCase);
        bool ssh=Match("ssh servers terminal сервер подключения");
        bool system=Match("system cpu gpu memory network система процессор память сеть");
        bool clock=Match("clock digital time seconds часы время"),calendar=Match("calendar date календарь дата"),reminders=Match("reminders todo tasks напоминания задачи");
        SearchPlaceholder.Visibility=query.Length==0?Visibility.Visible:Visibility.Collapsed;
        SshCategory.Visibility=ssh?Visibility.Visible:Visibility.Collapsed;
        SshPreviewButton.Visibility=ssh&&(category=="SSH"||query.Length>0)?Visibility.Visible:Visibility.Collapsed;
        SystemCategory.Visibility=system?Visibility.Visible:Visibility.Collapsed;
        SystemPreviewButton.Visibility=system&&(category=="System"||query.Length>0)?Visibility.Visible:Visibility.Collapsed;
        ClockCategory.Visibility=clock?Visibility.Visible:Visibility.Collapsed;
        CalendarCategory.Visibility=calendar?Visibility.Visible:Visibility.Collapsed;
        RemindersCategory.Visibility=reminders?Visibility.Visible:Visibility.Collapsed;
        Previews.Visibility=clock&&(category=="Clock"||query.Length>0)?Visibility.Visible:Visibility.Collapsed;
        CalendarPreviewButton.Visibility=calendar&&(category=="Calendar"||query.Length>0)?Visibility.Visible:Visibility.Collapsed;
        RemindersPreviewButton.Visibility=reminders&&(category=="Reminders"||query.Length>0)?Visibility.Visible:Visibility.Collapsed;
        SpecialPreviews.Visibility=SshPreviewButton.Visibility==Visibility.Visible||SystemPreviewButton.Visibility==Visibility.Visible||CalendarPreviewButton.Visibility==Visibility.Visible||RemindersPreviewButton.Visibility==Visibility.Visible?Visibility.Visible:Visibility.Collapsed;
        EmptySearch.Visibility=clock||calendar||reminders||system||ssh?Visibility.Collapsed:Visibility.Visible;
        RefreshPreview();
    }
    private void SelectCategory(string value){
        category=value;Search.Clear();CategoryHeading.Text=value;
        CategoryDescription.Text=value=="SSH"?"Your SSH connections. Click a server to open a terminal.":value=="System"?"CPU, memory, GPU and network. Updated every two seconds.":value=="Clock"?"The time here, simply digital. Add a clock with or without seconds.":value=="Calendar"?"Your month at a glance. Updated automatically every day.":"Your local to-do list. Shared across all Reminders widgets.";
        SshCategory.Background=category=="SSH"?new SolidColorBrush(Color.FromRgb(10,100,214)):Brushes.Transparent;
        SystemCategory.Background=category=="System"?new SolidColorBrush(Color.FromRgb(10,100,214)):Brushes.Transparent;
        ClockCategory.Background=category=="Clock"?new SolidColorBrush(Color.FromRgb(10,100,214)):Brushes.Transparent;
        CalendarCategory.Background=category=="Calendar"?new SolidColorBrush(Color.FromRgb(10,100,214)):Brushes.Transparent;
        RemindersCategory.Background=category=="Reminders"?new SolidColorBrush(Color.FromRgb(10,100,214)):Brushes.Transparent;
        SearchChanged(Search,null!);
    }
    private void SelectSsh(object sender,RoutedEventArgs e)=>SelectCategory("SSH");
    private void AddSsh(object sender,RoutedEventArgs e){manager.AddFromGallery(false,"SSH");AddedStatus.Text="SSH added to your desktop";}
    private void SelectSystem(object sender,RoutedEventArgs e)=>SelectCategory("System");
    private void AddSystem(object sender,RoutedEventArgs e){manager.AddFromGallery(false,"System");AddedStatus.Text="System monitor added to your desktop";}
    private void SelectClock(object sender,RoutedEventArgs e)=>SelectCategory("Clock");
    private void SelectCalendar(object sender,RoutedEventArgs e)=>SelectCategory("Calendar");
    private void SelectReminders(object sender,RoutedEventArgs e)=>SelectCategory("Reminders");
    private void AddCalendar(object sender,RoutedEventArgs e){manager.AddFromGallery(false,"Calendar");AddedStatus.Text="Calendar added to your desktop";}
    private void AddReminders(object sender,RoutedEventArgs e){manager.AddFromGallery(false,"Reminders");AddedStatus.Text="Reminders added to your desktop";}
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
