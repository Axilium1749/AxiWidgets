using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace AxiWidgets;
internal sealed partial class Manager
{
    private ContextMenu? trayMenu;
    private void TrayClicked(object? sender,Forms.MouseEventArgs e)
    {
        if(disposed)return;
        if(e.Button==Forms.MouseButtons.Left){if(trayMenu!=null)trayMenu.IsOpen=false;OpenGallery();}
        else if(e.Button==Forms.MouseButtons.Right)OpenTrayMenu();
    }
    private void OpenTrayMenu()
    {
        if(disposed)return;
        // Give the popup an application foreground owner before it captures
        // input. Activating the popup itself would immediately dismiss it.
        if(listener!=null){Native.ShowWindow(listener.Handle,4);Native.SetForegroundWindow(listener.Handle);}
        if(trayMenu==null){
            var resources=new ResourceDictionary {Source=new Uri("/AxiWidgets;component/WidgetMenu.xaml",UriKind.Relative)};
            trayMenu=new ContextMenu {Style=(Style)resources["WidgetContextMenu"],StaysOpen=false};
            var open=new MenuItem {Header="Открыть меню виджетов"};
            open.Click+=(_,_)=>{trayMenu.IsOpen=false;OpenGallery();};
            var exit=new MenuItem {Header="Выход"};exit.Click+=(_,_)=>ExitApplication();
            trayMenu.Items.Add(open);
            trayMenu.Items.Add(new Separator {Style=(Style)trayMenu.FindResource("WidgetMenuSeparator")});
            trayMenu.Items.Add(exit);
        }
        Native.GetCursorPos(out var cursor);var display=Display.For(new(cursor.X,cursor.Y,1,1));
        trayMenu.IsOpen=false;trayMenu.Placement=PlacementMode.AbsolutePoint;
        trayMenu.HorizontalOffset=cursor.X/display.Scale;trayMenu.VerticalOffset=cursor.Y/display.Scale;
        trayMenu.IsOpen=true;trayMenu.ApplyTemplate();trayMenu.UpdateLayout();
        if(PresentationSource.FromVisual(trayMenu) is HwndSource source){
            var bounds=Native.Bounds(source.Handle);
            int x=(int)Math.Clamp(cursor.X,display.Work.X,Math.Max(display.Work.X,display.Work.Right-bounds.Width));
            int y=(int)Math.Clamp(cursor.Y-bounds.Height,display.Work.Y,Math.Max(display.Work.Y,display.Work.Bottom-bounds.Height));
            Native.SetWindowPos(source.Handle,-1,x,y,0,0,1|0x10);
        }
    }
    private void ExitApplication()
    {
        if(disposed)return;
        if(trayMenu!=null)trayMenu.IsOpen=false;
        foreach(var window in windows){window.SetEdit(false);window.Remember();}
        Save();SaveAppearance();
        gallery?.ShutdownGallery();gallery=null;
        Application.Current.Shutdown();
    }
}
