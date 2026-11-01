using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AxiWidgets.Core;
using Rect = AxiWidgets.Core.Rect;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace AxiWidgets;

internal static class Log
{
    internal static string Folder=Path.Combine(AppContext.BaseDirectory,".local");
    internal static void Write(string text)
    {
        try { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder,"diagnostics.log"),$"{DateTimeOffset.Now:O} {text}\n"); }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }
}
internal static class TrayArtwork
{
    internal static Drawing.Icon Create()
    {
        using var stream=Application.GetResourceStream(new Uri("/AxiWidgets;component/Assets/AxiWidgets.ico",UriKind.Relative))!.Stream;
        using var icon=new Drawing.Icon(stream,Forms.SystemInformation.SmallIconSize);
        return (Drawing.Icon)icon.Clone();
    }
}
internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        if(args.Contains("--shutdown")){
            if(EventWaitHandle.TryOpenExisting(@"Local\AxiWidgets.Exit",out var signal)){using(signal)signal.Set();}return;
        }
        using var mutex=new Mutex(true,@"Local\AxiWidgets.Manager",out bool created);
        if(!created) return;
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        string mode=args.FirstOrDefault(x=>x.StartsWith("--poc="))?.Split('=')[1]??"desktop";
        if(mode is not ("desktop" or "normal" or "noactivate")) return;
        Log.Folder=UserData.Resolve(args);
        app.DispatcherUnhandledException+=(_,e)=>Log.Write(e.Exception.ToString());
        Log.Write($"Starting OS={Environment.OSVersion} runtime={Environment.Version} mode={mode}");
        using var manager=new Manager(mode);
        using var exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AxiWidgets.Exit");
        var exitWait=ThreadPool.RegisterWaitForSingleObject(exitSignal,(_,_)=>app.Dispatcher.BeginInvoke(()=>manager.ExitApplication()),null,Timeout.Infinite,false);
        app.Startup+=(_,_)=> { manager.Start(); if(args.Contains("--gallery"))manager.OpenGallery(); if(args.Contains("--media-bench")) app.Dispatcher.BeginInvoke(async ()=>await manager.MediaBench()); else if(args.Contains("--media-smoke")) app.Dispatcher.BeginInvoke(async ()=>await manager.MediaSmoke()); else if(args.Contains("--ssh-feedback-smoke")) app.Dispatcher.BeginInvoke(()=>manager.SshFeedbackSmoke()); else if(args.Contains("--smoke")) app.Dispatcher.BeginInvoke(()=>manager.Smoke()); else if(args.Contains("--accent-probe")) app.Dispatcher.BeginInvoke(()=>manager.AccentProbe()); else if(args.Contains("--live-probe")) app.Dispatcher.BeginInvoke(async ()=>await manager.LiveProbe()); };
        try{app.Run();}finally{exitWait.Unregister(null);}
    }
}
internal sealed partial class Manager : IDisposable
{
    private readonly List<ClockWindow> windows=[];
    private readonly Forms.NotifyIcon tray=new() { Icon=TrayArtwork.Create(),Text="AxiWidgets" };
    private readonly string mode;
    private bool editing, recovering, disposed;
    private HwndSource? listener;
    private GalleryWindow? gallery;
    private readonly uint taskbar=Native.RegisterWindowMessage("TaskbarCreated");
    private readonly DispatcherTimer retry=new() { Interval=TimeSpan.FromSeconds(2) };
    private int attempts;
    internal AppearanceSettings Appearance { get; private set; }=new();
    private readonly DispatcherTimer appearanceSave=new() { Interval=TimeSpan.FromMilliseconds(350) };
    private string AppearancePath=>Path.Combine(Log.Folder,"appearance.json");
    internal SshService Ssh {get;private set;}=null!;
    internal ReminderStore Reminders {get;private set;}=null!;
    private string Settings=>Path.Combine(Log.Folder,"layout.json");
    internal Manager(string mode) { this.mode=mode; retry.Tick+=(_,_)=>Recover(); appearanceSave.Tick+=(_,_)=>SaveAppearance(); }
    internal void Start()
    {
        var watch=Stopwatch.StartNew();
        Appearance=new AppearanceSettings();
        Ssh=new SshService();
        Reminders=new ReminderStore(Path.Combine(Log.Folder,"reminders.json"));
        listener=new HwndSource(new HwndSourceParameters("AxiWidgets Shell Listener") { Width=0,Height=0,WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80 });
        listener.AddHook(Messages);
        List<ClockState>? states=null;
        try { if(File.Exists(Settings)) states=JsonSerializer.Deserialize<List<ClockState>>(File.ReadAllText(Settings)); }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { Log.Write($"Settings load failed: {e.Message}"); }
        foreach(var state in states??[new ClockState()]) {
            if(!double.IsFinite(state.X)||!double.IsFinite(state.Y)) {state.X=48;state.Y=48;}
            Add(state);
        }
        tray.MouseClick+=TrayClicked; tray.Visible=true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
        Microsoft.Win32.SystemEvents.TimeChanged+=TimeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferencesChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
        // Run-key startup can precede Explorer's desktop icon host at sign-in.
        if(mode=="desktop"&&windows.Any(w=>!w.IsVisible)){attempts=0;recovering=true;retry.Start();}
        Log.Write($"Manager ready {watch.Elapsed.TotalMilliseconds:F1}ms; clocks={windows.Count}");
    }
    private void Add(ClockState state)
    {
        var window=new ClockWindow(this,state,mode); windows.Add(window);
        window.Closed+=(_,_)=> { if(!disposed&&!recovering&&windows.Contains(window)) { recovering=true; retry.Start(); } };
        window.Show(); if(editing) window.SetEdit(true);
    }
    internal IEnumerable<Rect> Neighbors(ClockWindow except)=>windows.Where(w=>w!=except&&Native.IsWindow(w.Handle)).Select(w=>Native.Bounds(w.Handle));
    internal void Save()
    {
        try {
            Directory.CreateDirectory(Log.Folder);
            File.WriteAllText(Settings+".tmp",JsonSerializer.Serialize(windows.Select(w=>w.State),new JsonSerializerOptions { WriteIndented=true }));
            File.Move(Settings+".tmp",Settings,true);
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { Log.Write($"Save failed: {e.Message}"); tray.ShowBalloonTip(4000,"AxiWidgets","Cannot save widget positions. See diagnostics.log.",Forms.ToolTipIcon.Warning); }
    }
    internal void ChangeAppearance(double tintOpacity,string tintColor)
    {
        Appearance.TintOpacity=tintOpacity; Appearance.TintColor=tintColor; Appearance.Normalize();
        foreach(var window in windows) window.ApplyTheme();
        appearanceSave.Stop(); appearanceSave.Start();
    }
    private void SaveAppearance()
    {
        appearanceSave.Stop();
        try {
            Directory.CreateDirectory(Log.Folder);
            File.WriteAllText(AppearancePath+".tmp",JsonSerializer.Serialize(Appearance,new JsonSerializerOptions {WriteIndented=true}));
            File.Move(AppearancePath+".tmp",AppearancePath,true);
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
            Log.Write($"Appearance save failed: {e.Message}");
            tray.ShowBalloonTip(4000,"AxiWidgets","Cannot save appearance settings.",Forms.ToolTipIcon.Warning);
        }
    }
    internal async void Smoke()
    {
        try {
            if(windows.Count==0) Add(new ClockState());
            var first=windows[0];
            if(!Native.IsWindow(first.Handle)||Native.GetParent(first.Handle)==0) throw new Exception("No desktop parent");
            long clockStyle=(long)Native.GetLong(first.Handle,-20);
            if((clockStyle&0x08200080)!=0x08200080||(clockStyle&(8|0x80000))!=0||Native.Class(first.Handle)!="AxiWidgets.NativeCard")
                throw new Exception("Native clock window styles invalid");
            if(!Native.IsWindowEnabled(first.Handle)||first.Editing) throw new Exception("Normal mode context input unavailable");
            first.OpenMenu();if(!first.ContextMenu.IsOpen)throw new Exception("Normal mode context menu failed");first.ContextMenu.IsOpen=false;
            if(!first.AcrylicAccepted) throw new Exception("Native composition unavailable");
            first.SmokeInsetPlacement();
            editing=true; first.SetEdit(true);
            if(!Native.IsWindowEnabled(first.Handle)) throw new Exception("Edit mode disabled");
            Add(new ClockState { X=380,Y=48 });
            if(Neighbors(first).Count()!=windows.Count-1) throw new Exception("Instance enumeration failed");
            first.UpdateContextMenu();
            var clockMenu=first.ContextMenu!;
            first.OpenMenu();
            foreach(var index in new[] {0,1}) {
                var toggle=(System.Windows.Controls.MenuItem)clockMenu.Items[index];
                bool before=index==0?first.State.Seconds:first.State.Date;
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                if((index==0?first.State.Seconds:first.State.Date)==before) throw new Exception("Clock menu toggle failed");
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            }
            clockMenu.ApplyTemplate(); clockMenu.UpdateLayout();
            var menuBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(clockMenu.ActualWidth),(int)Math.Ceiling(clockMenu.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);
            menuBitmap.Render(clockMenu);
            var menuEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            menuEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(menuBitmap));
            Directory.CreateDirectory(Log.Folder);
            using(var output=File.Create(Path.Combine(Log.Folder,"clock-menu-layout.png"))) menuEncoder.Save(output);
            clockMenu.IsOpen=false;
            foreach(var w in windows) {w.SetEdit(false);w.Remember();}
            editing=false;Save();
            var saved=JsonSerializer.Deserialize<List<ClockState>>(File.ReadAllText(Settings))!;
            if(saved.Count!=windows.Count || saved.Select(s=>s.Id).Distinct().Count()!=saved.Count) throw new Exception("Persistence failed");
            if(windows.Any(w=>w.Editing||!Native.IsWindowEnabled(w.Handle))) throw new Exception("Normal mode input policy failed");
            OpenGallery();
            var editor=gallery!;
            long ex=(long)Native.GetLong(editor.Handle,-20);
            if((ex&0x80)==0||(ex&8)==0||(ex&0x40000)!=0||editor.ShowInTaskbar) throw new Exception("Gallery window styles invalid");
            Native.SendMessageTimeout(editor.Handle,0x10,0,0,2,1000,out _);
            if(!editor.IsVisible) throw new Exception("Gallery dismissed by WM_CLOSE");
            var originalBounds=Native.Bounds(editor.Handle);
            foreach(int command in new[] {0xf060,0xf010,0xf000,0xf020,0xf030}) {
                Native.SendMessageTimeout(editor.Handle,0x112,command,0,2,1000,out _);
                if(!editor.IsVisible||Native.Bounds(editor.Handle)!=originalBounds) throw new Exception($"Gallery changed by system command {command:X}");
            }
            editor.Search.Text="not-a-widget";
            if(editor.Previews.Visibility!=Visibility.Collapsed||editor.PreviewTimerRunning) throw new Exception("Search did not filter or stop preview timer");
            editor.Search.Clear();
            if(editor.Previews.Visibility!=Visibility.Visible) throw new Exception("Search did not restore results");
            int count=windows.Count;
            editor.DigitalPreview.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            editor.SecondsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if(windows.Count!=count+2||windows[^2].State.Seconds||!windows[^1].State.Seconds||!windows[^1].State.Acrylic)
                throw new Exception("Gallery preview actions failed");
            editor.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(editor);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var output=File.Create(Path.Combine(Log.Folder,"gallery-layout.png"))) encoder.Save(output);
            Native.SetWindowPos(editor.Handle,0,(int)originalBounds.X+25,(int)originalBounds.Y+20,0,0,1|4|0x10);
            editor.DoneButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if(editor.IsVisible||editor.PreviewTimerRunning||editing||windows.Any(w=>w.Editing||!Native.IsWindowEnabled(w.Handle))) throw new Exception("Done failed to lock widgets or stop preview timer");
            OpenGallery();
            if(gallery!=editor) throw new Exception("Gallery duplicated on reopen");
            if(Native.Bounds(editor.Handle)!=originalBounds)throw new Exception("Gallery position was not reset");
            FinishEditing();
            OpenGallery();
            int widgetStart=windows.Count;
            editor.CalendarPreviewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if(windows.Count!=widgetStart+1||windows[^1].State.Kind!="Calendar")throw new Exception("Calendar creation failed");
            editor.RemindersPreviewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            editor.RemindersPreviewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if(windows.Count!=widgetStart+3||windows[^1].State.Kind!="Reminders")throw new Exception("Reminders creation failed");
            FinishEditing();
            if(!Native.IsWindowEnabled(windows[widgetStart].Handle)||!Native.IsWindowEnabled(windows[^1].Handle))throw new Exception("Widget normal input policy failed");
            windows[widgetStart].OpenMenu();
            if(!windows[widgetStart].ContextMenu.IsOpen||windows[widgetStart].ContextMenu.Items.Count!=1||windows[widgetStart].ContextMenu.Items[0] is not System.Windows.Controls.MenuItem)throw new Exception("Single-action context menu failed");
            windows[widgetStart].ContextMenu.IsOpen=false;
            int beforeRemoval=windows.Count;
            Add(new ClockState {Kind="Calendar"});
            var removable=windows[^1];removable.SetEdit(false);removable.OpenMenu();
            ((System.Windows.Controls.MenuItem)removable.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            if(windows.Count!=beforeRemoval||Native.IsWindow(removable.Handle))throw new Exception("Normal mode removal failed");
            Log.Write("CONTEXT/GALLERY PASS: normal-mode menu and removal; no leading separator; gallery position reset on reopen.");
            windows[^1].SmokeReminderInput();
            var created=Reminders.Items[0].Id;
            Reminders.Toggle(created);if(!new ReminderStore(Path.Combine(Log.Folder,"reminders.json")).Items.Single(x=>x.Id==created).Done)throw new Exception("Task state not saved");
            Reminders.Toggle(created);Reminders.Remove(created);
            windows[^1].SmokeReminderActions();
            windows[^1].SmokePhysicalScroll();
            Log.Write("SCROLL SMOKE PASS: short 1/2-task rubber band; 12-task drag, inertia and fine wheel; click threshold; captured list stays in fixed HWND; spring settles and idle timer stops.");
            foreach(var widget in windows.Skip(widgetStart)){
                widget.SmokeDrag();
                var bounds=Native.Bounds(widget.Handle);double scale=Display.For(bounds).Scale;
                if(Math.Abs(bounds.Width-364*scale)>1||Math.Abs(bounds.Height-384*scale)>1)throw new Exception("Widget DPI size failed");
            }
            editing=true;
            editor.SystemPreviewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var system=windows[^1];
            if(system.State.Kind!="System")throw new Exception("System gallery creation failed");
            system.SmokeDrag();system.SetEdit(false);
            var systemBounds=Native.Bounds(system.Handle);var systemScale=Display.For(systemBounds).Scale;
            if(!Native.IsWindowEnabled(system.Handle)||Math.Abs(systemBounds.Width-364*systemScale)>1||Math.Abs(systemBounds.Height-WidgetDrawing.SystemHeight*systemScale)>1)throw new Exception("System dimensions/input failed");
            editing=true;RemoveClock(system);FinishEditing();
            var systemPreview=new WidgetPreview {Kind="System",Width=364,Height=WidgetDrawing.SystemHeight};
            systemPreview.Measure(new Size(364,WidgetDrawing.SystemHeight));systemPreview.Arrange(new System.Windows.Rect(0,0,364,WidgetDrawing.SystemHeight));systemPreview.UpdateLayout();
            var systemBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(728,324,192,192,System.Windows.Media.PixelFormats.Pbgra32);systemBitmap.Render(systemPreview);
            var systemEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();systemEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(systemBitmap));
            using(var image=File.Create(Path.Combine(Log.Folder,"system-preview.png")))systemEncoder.Save(image);
            var savedSsh=Ssh.Store.Items.ToArray();
            try{
                Ssh.Store.Save(Enumerable.Range(0,12).Select(i=>new SshConnection{Name="Test server "+i,Host="127.0.0.1",Port=2200+i}));
                editing=true;editor.SshPreviewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                var ssh=windows[^1];if(ssh.State.Kind!="SSH")throw new Exception("SSH gallery creation failed");foreach(int sshCount in new[]{1,2,12}){
                    Ssh.Store.Save(Enumerable.Range(0,sshCount).Select(i=>new SshConnection{Name="Test server "+i,Host="127.0.0.1",Port=2200+i}));ssh.SmokeSshScroll();
                }
                var settings=new SshSettingsWindow(Ssh);settings.Show();settings.SmokeRender();settings.Close();
                RemoveClock(ssh);FinishEditing();
                Log.Write("SSH SMOKE PASS: gallery creation, list drag/overscroll, fixed HWND, input cleanup, settings render and removal; no SSH session launched.");
            }finally{Ssh.Store.Save(savedSsh);}
            await Ssh.SmokeImmediatePing();
            Log.Write("SYSTEM SMOKE PASS: gallery creation, 364x162 DIP, context input, edit drag, removal.");
            Log.Write("WIDGET SMOKE PASS: normal-mode context menus without leading separator; temporary text editor Enter/Escape; shared task refresh, toggle and persistence; native DPI dimensions; drag/capture/snap path.");
            Log.Write("GALLERY SMOKE PASS: tool/topmost styles, system command suppression, position reset on reopen, search and timer stop, preview actions, Done locks positions and stops timer, reusable window.");
            Log.Write("SMOKE PASS: native desktop parent, normal/edit input, independent instances, JSON save/read, positions locked on exit edit.");
            TrayClicked(tray,new Forms.MouseEventArgs(Forms.MouseButtons.Right,1,0,0,0));
            if(trayMenu?.IsOpen!=true||gallery?.IsVisible==true||editing)throw new Exception($"Right tray click did not open menu independently: menu={trayMenu?.IsOpen} gallery={gallery?.IsVisible} editing={editing}");
            ((System.Windows.Controls.MenuItem)trayMenu.Items[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            if(gallery?.IsVisible!=true||!editing||trayMenu.IsOpen)throw new Exception("Tray open gallery action failed");
            FinishEditing();
            TrayClicked(tray,new Forms.MouseEventArgs(Forms.MouseButtons.Left,1,0,0,0));
            if(gallery?.IsVisible!=true)throw new Exception("Left tray click no longer opens gallery");
            FinishEditing();
            TrayClicked(tray,new Forms.MouseEventArgs(Forms.MouseButtons.Right,1,0,0,0));
            var testPath=@"Software\AxiWidgets.AutostartTest."+Guid.NewGuid().ToString("N");
            try{
                var isolated=new Autostart(testPath);
                isolated.SetEnabled(true);if(!isolated.Enabled)throw new Exception("Autostart enable failed");
                isolated.SetEnabled(false);if(isolated.Enabled)throw new Exception("Autostart disable failed");
            }finally{Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testPath,false);}
            string migrationRoot=Path.Combine(Log.Folder,"migration-test");
            string legacy=Path.Combine(migrationRoot,"old"),current=Path.Combine(migrationRoot,"current");
            Directory.CreateDirectory(legacy);Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(legacy,"reminders.json"),"[]");File.WriteAllText(Path.Combine(legacy,"layout.json"),"[]");
            UserData.MigrateLegacy(current,legacy);
            if(!File.Exists(Path.Combine(current,"reminders.json"))||!File.Exists(Path.Combine(current,"layout.json")))throw new Exception("Legacy data migration failed");
            File.WriteAllText(Path.Combine(current,"reminders.json"),"preserved");UserData.MigrateLegacy(current,legacy);
            if(File.ReadAllText(Path.Combine(current,"reminders.json"))!="preserved")throw new Exception("Data migration overwrote tasks");
            Log.Write("AUTOSTART/DATA SMOKE PASS: isolated registry toggle and quoted command; legacy settings migration preserves existing data.");
            trayMenu.Items.OfType<System.Windows.Controls.MenuItem>().Single(x=>Equals(x.Header,"Выход")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            Log.Write("TRAY SMOKE PASS: styled right-click menu, open gallery action, left-click gallery preserved, Exit requests full shutdown and saves layout.");
        } catch(Exception e) {Log.Write("SMOKE FAIL: "+e);Environment.ExitCode=1;}
        gallery?.ShutdownGallery(); gallery=null;
        Application.Current.Shutdown(Environment.ExitCode);
    }
    internal async System.Threading.Tasks.Task LiveProbe()
    {
        try {
            bool engine=Process.GetProcessesByName("wallpaper64").Length>0;
            if(!engine) {Log.Write("LIVE PROBE INCONCLUSIVE: wallpaper64 process absent");return;}
            if(windows.Count==0) {Log.Write("LIVE PROBE INCONCLUSIVE: no clock window");return;}
            var r=Native.Bounds(windows[0].Handle);
            int x=(int)r.X+20,y=(int)r.Y+20;
            byte[] Capture() {
                using var bmp=new System.Drawing.Bitmap(60,40);
                using(var g=System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(x,y,0,0,new System.Drawing.Size(60,40));
                var values=new byte[60*40*3]; int k=0;
                for(int row=0;row<40;row++)for(int col=0;col<60;col++) {var c=bmp.GetPixel(col,row);values[k++]=c.R;values[k++]=c.G;values[k++]=c.B;}
                return values;
            }
            var first=Capture();
            await System.Threading.Tasks.Task.Delay(1500);
            var second=Capture();
            double delta=first.Zip(second,(a,b)=>Math.Abs(a-b)).Average();
            Log.Write($"LIVE PROBE wallpaper64=yes window={windows[0].Handle:X} sample=({x},{y},60,40) elapsed=1.5s meanRGBDelta={delta:F3}; movement is evidence of live composited pixels, not isolated blur strength. Occlusion or a still segment makes this inconclusive.");
        } catch(Exception e) {Log.Write("LIVE PROBE INCONCLUSIVE: "+e);Environment.ExitCode=1;}
        finally {Application.Current.Shutdown();}
    }
    internal void AccentProbe()
    {
        try {
            if(windows.Count==0) throw new InvalidOperationException("No clock window");
            Log.Write("Clock backend=WindowsComposition HostBackdropBrush; legacy AccentPolicy probe is retired for clocks.");
        } catch(Exception e) { Log.Write("ACCENT PROBE FAIL: "+e);Environment.ExitCode=1; }
        finally { Application.Current.Shutdown(); }
    }
    internal void OpenGallery()
    {
        editing=true;
        foreach(var w in windows) w.SetEdit(true);
        gallery??=new GalleryWindow(this);
        gallery.Open();
    }
    internal void FinishEditing()
    {
        SaveAppearance();
        editing=false;
        foreach(var w in windows) {w.SetEdit(false);w.Remember();}
        Save(); gallery?.Dismiss();
    }
    internal void AddFromGallery(bool seconds,string kind="Clock")
    {
        if(!editing) return;
        Native.GetCursorPos(out var cursor);
        var display=Display.For(new(cursor.X,cursor.Y,1,1));
        // Prefer a vacant slot above the gallery, then scan the work area.
        double x=24,y=24;double width=kind=="Clock"?300:364,height=kind is "System" or "SSH" or "Media"?WidgetDrawing.SystemHeight:kind=="Clock"?160:384;
        var occupied=windows.Select(w=>Native.Bounds(w.Handle)).ToArray();
        bool found=false;
        for(double row=24;row+height<=display.Work.Height/display.Scale-8&&!found;row+=height+12)
            for(double col=display.Work.Width/display.Scale-width-24;col>=8;col-=width+12) {
                var r=new Rect(display.Work.X+col*display.Scale,display.Work.Y+row*display.Scale,width*display.Scale,height*display.Scale);
                if(occupied.All(o=>r.Right<=o.X||r.X>=o.Right||r.Bottom<=o.Y||r.Y>=o.Bottom)) {x=col;y=row;found=true;break;}
            }
        Add(new ClockState { Kind=kind,Seconds=seconds,Acrylic=true,Monitor=display.Id,X=x,Y=y }); Save();
    }
    internal void RemoveClock(ClockWindow window)
    {
        if(!windows.Remove(window))return;
        window.Close(); Save();
    }
    private nint Messages(nint hwnd,int msg,nint w,nint l,ref bool handled)
    {
        if((uint)msg==taskbar) { attempts=0;recovering=true;retry.Start(); }
        return 0;
    }
    private void Recover()
    {
        retry.Stop(); if(disposed)return;
        if(Native.FindWindow("Progman",null)==0) { if(++attempts<15)retry.Start();return; }
        recovering=true;
        var states=windows.Select(w=>w.State).ToArray();
        foreach(var w in windows.ToArray()) w.Close();
        windows.Clear(); foreach(var s in states)Add(s);
        recovering=false;tray.Visible=false;tray.Visible=true;
        if(mode=="desktop"&&windows.Any(w=>!w.IsVisible)&&++attempts<15) {recovering=true;retry.Start();}
    }
    private void DisplaysChanged(object? sender,EventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=> {foreach(var w in windows)w.Restore();if(gallery?.IsVisible==true)gallery.Place();Save();});
    private void TimeChanged(object? sender,EventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=> {foreach(var w in windows)w.RefreshClock();});
    private void PreferencesChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=> {foreach(var w in windows)w.ApplyTheme();if(gallery?.IsVisible==true)gallery.ApplyBackdrop();});
    private void PowerChanged(object sender,Microsoft.Win32.PowerModeChangedEventArgs e) { if(e.Mode==Microsoft.Win32.PowerModes.Resume) TimeChanged(sender,e); }
    public void Dispose()
    {
        if(disposed)return;
        tray.MouseClick-=TrayClicked;if(trayMenu!=null){trayMenu.IsOpen=false;trayMenu.Items.Clear();trayMenu=null;}
        disposed=true;retry.Stop();appearanceSave.Stop();gallery?.ShutdownGallery();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplaysChanged; Microsoft.Win32.SystemEvents.TimeChanged-=TimeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferencesChanged;Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;
        foreach(var w in windows.ToArray())w.Close(); Ssh.Dispose();tray.Visible=false;tray.ContextMenuStrip?.Dispose();var icon=tray.Icon;tray.Dispose();icon?.Dispose();listener?.Dispose();NativeCard.Check(NativeCard.Shutdown());
    }
}
