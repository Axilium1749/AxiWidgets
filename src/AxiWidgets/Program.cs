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
internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        using var mutex=new Mutex(true,@"Local\AxiWidgets.Manager",out bool created);
        if(!created) return;
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        string mode=args.FirstOrDefault(x=>x.StartsWith("--poc="))?.Split('=')[1]??"desktop";
        if(mode is not ("desktop" or "normal" or "noactivate")) return;
        Log.Folder=Path.GetFullPath(args.FirstOrDefault(x=>x.StartsWith("--data="))?[7..]??Path.Combine(AppContext.BaseDirectory,".local"));
        app.DispatcherUnhandledException+=(_,e)=>Log.Write(e.Exception.ToString());
        Log.Write($"Starting OS={Environment.OSVersion} runtime={Environment.Version} mode={mode}");
        using var manager=new Manager(mode);
        app.Startup+=(_,_)=> { manager.Start(); if(args.Contains("--smoke")) app.Dispatcher.BeginInvoke(()=>manager.Smoke()); else if(args.Contains("--gallery")) app.Dispatcher.BeginInvoke(()=>manager.OpenGallery()); };
        app.Run();
    }
}
internal sealed class Manager : IDisposable
{
    private readonly List<ClockWindow> windows=[];
    private readonly Forms.NotifyIcon tray=new() { Icon=System.Drawing.SystemIcons.Application,Text="AxiWidgets" };
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
    private string Settings=>Path.Combine(Log.Folder,"layout.json");
    internal Manager(string mode) { this.mode=mode; retry.Tick+=(_,_)=>Recover(); appearanceSave.Tick+=(_,_)=>SaveAppearance(); }
    internal void Start()
    {
        var watch=Stopwatch.StartNew();
        try { if(File.Exists(AppearancePath)) Appearance=JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(AppearancePath))??new(); }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {Log.Write($"Appearance load failed: {e.Message}");}
        Appearance.Normalize();
        listener=new HwndSource(new HwndSourceParameters("AxiWidgets Shell Listener") { Width=0,Height=0,WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80 });
        listener.AddHook(Messages);
        List<ClockState>? states=null;
        try { if(File.Exists(Settings)) states=JsonSerializer.Deserialize<List<ClockState>>(File.ReadAllText(Settings)); }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { Log.Write($"Settings load failed: {e.Message}"); }
        foreach(var state in states??[new ClockState()]) {
            if(!double.IsFinite(state.X)||!double.IsFinite(state.Y)) {state.X=48;state.Y=48;}
            Add(state);
        }
        tray.MouseClick+=(_,_)=>OpenGallery(); tray.Visible=true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
        Microsoft.Win32.SystemEvents.TimeChanged+=TimeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferencesChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
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
    internal void ChangeAppearance(bool blur,double transparency)
    {
        Appearance.Blur=blur; Appearance.Transparency=transparency; Appearance.Normalize();
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
    internal void Smoke()
    {
        try {
            if(windows.Count==0) Add(new ClockState());
            var first=windows[0];
            if(!Native.IsWindow(first.Handle)||Native.GetParent(first.Handle)==0) throw new Exception("No desktop parent");
            if(Native.IsWindowEnabled(first.Handle)) throw new Exception("Normal mode accepts input");
            bool originalBlur=Appearance.Blur; double originalTransparency=Appearance.Transparency;
            ChangeAppearance(true,56);
            Log.Write($"SMOKE Acrylic request accepted={first.AcrylicAccepted}");
            if(first.AcrylicAccepted) {
                byte RenderTintAlpha(double transparency) {
                    ChangeAppearance(true,transparency); first.UpdateLayout();
                    var rendered=new System.Windows.Media.Imaging.RenderTargetBitmap((int)first.ActualWidth,(int)first.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                    rendered.Render(first);
                    var pixel=new byte[4];
                    rendered.CopyPixels(new Int32Rect(20,20,1,1),pixel,4,0);
                    return pixel[3];
                }
                byte dense=RenderTintAlpha(20),light=RenderTintAlpha(80);
                if(dense-light<100) throw new Exception($"Transparency did not change rendered tint: {dense}/{light}");
                Log.Write($"TRANSPARENCY RENDER PASS: blur on, alpha at 20%={dense}, at 80%={light}");
            }
            ChangeAppearance(false,0);
            if(first.AcrylicAccepted) throw new Exception("Acrylic did not turn off");
            ChangeAppearance(originalBlur,originalTransparency);
            editing=true; first.SetEdit(true);
            if(!Native.IsWindowEnabled(first.Handle)) throw new Exception("Edit mode disabled");
            Add(new ClockState { X=380,Y=48 });
            if(Neighbors(first).Count()!=windows.Count-1) throw new Exception("Instance enumeration failed");
            first.UpdateContextMenu();
            var clockMenu=first.ContextMenu!;
            clockMenu.PlacementTarget=first;
            clockMenu.IsOpen=true;
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
            if(windows.Any(w=>Native.IsWindowEnabled(w.Handle))) throw new Exception("Input remains enabled");
            OpenGallery();
            var editor=gallery!;
            editor.TransparencySlider.Value=0; editor.FlushAppearance();
            if(windows.Any(w=>w.AcrylicAccepted)) throw new Exception("Global solid mode did not reach all widgets");
            editor.BlurSlider.Value=0; editor.TransparencySlider.Value=75; editor.FlushAppearance();
            if(Appearance.Blur||Appearance.Transparency!=75) throw new Exception("Global appearance controls failed");
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
            editor.DoneButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if(editor.IsVisible||editor.PreviewTimerRunning||editing||windows.Any(w=>Native.IsWindowEnabled(w.Handle))) throw new Exception("Done failed to lock widgets or stop preview timer");
            var restoredAppearance=JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(AppearancePath))!;
            if(restoredAppearance.Blur||restoredAppearance.Transparency!=75) throw new Exception("Global appearance persistence failed");
            OpenGallery();
            if(gallery!=editor) throw new Exception("Gallery duplicated on reopen");
            FinishEditing();
            Log.Write("GALLERY SMOKE PASS: tool/topmost styles, close/move/resize/minimize/maximize suppression, search and timer stop, two preview actions, Done locks input and stops timer, reusable window.");
            Log.Write("SMOKE PASS: native desktop parent, normal/edit input, independent instances, JSON save/read, input disabled on exit edit.");
        } catch(Exception e) {Log.Write("SMOKE FAIL: "+e);Environment.ExitCode=1;}
        gallery?.ShutdownGallery(); gallery=null;
        Application.Current.Shutdown();
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
        gallery?.FlushAppearance(); SaveAppearance();
        editing=false;
        foreach(var w in windows) {w.SetEdit(false);w.Remember();}
        Save(); gallery?.Dismiss();
    }
    internal void AddFromGallery(bool seconds)
    {
        if(!editing) return;
        Native.GetCursorPos(out var cursor);
        var display=Display.For(new(cursor.X,cursor.Y,1,1));
        // Prefer a vacant slot above the gallery, then scan the work area.
        double x=24,y=24;
        var occupied=windows.Select(w=>Native.Bounds(w.Handle)).ToArray();
        bool found=false;
        for(double row=24;row+160<=display.Work.Height/display.Scale-8&&!found;row+=172)
            for(double col=display.Work.Width/display.Scale-324;col>=8;col-=312) {
                var r=new Rect(display.Work.X+col*display.Scale,display.Work.Y+row*display.Scale,300*display.Scale,160*display.Scale);
                if(occupied.All(o=>r.Right<=o.X||r.X>=o.Right||r.Bottom<=o.Y||r.Y>=o.Bottom)) {x=col;y=row;found=true;break;}
            }
        Add(new ClockState { Seconds=seconds,Acrylic=true,Monitor=display.Id,X=x,Y=y }); Save();
    }
    internal void RemoveClock(ClockWindow window)
    {
        if(!editing) return;
        windows.Remove(window); window.Close(); Save();
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
        foreach(var w in windows.ToArray()) if(Native.IsWindow(w.Handle))w.Close();
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
        disposed=true;retry.Stop();appearanceSave.Stop();gallery?.ShutdownGallery();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplaysChanged; Microsoft.Win32.SystemEvents.TimeChanged-=TimeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferencesChanged;Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;
        foreach(var w in windows.ToArray())w.Close(); tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();listener?.Dispose();
    }
}




