using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AxiWidgets.Core;
using Rect=System.Windows.Rect;

namespace AxiWidgets;
public sealed class WidgetPreview : FrameworkElement
{
    public string Kind {get;set;}="Calendar";
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth/364,ActualHeight/384));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(45,45,49)),new Pen(new SolidColorBrush(Color.FromArgb(44,255,255,255)),.6),new Rect(0,0,364,384),30,30);
        var drawing=Kind=="Calendar"?WidgetDrawing.Calendar(System.DateTime.Now):WidgetDrawing.Reminders(new ReminderItem[]{new(){Text="Review lecture notes"},new(){Text="Finish overlay design",Done=true},new(){Text="Plan the weekend"}},0);
        foreach(var c in drawing.Commands){
            var brush=new SolidColorBrush(Color.FromArgb((byte)(c.Color>>24),(byte)(c.Color>>16),(byte)(c.Color>>8),(byte)c.Color));
            var rect=new Rect(c.X,c.Y,System.Math.Max(0,c.W),System.Math.Max(0,c.H));
            if(c.Kind==0)dc.DrawRoundedRectangle(brush,null,rect,c.R,c.R);
            else if(c.Kind==1)dc.DrawRoundedRectangle(null,new Pen(brush,c.Size),rect,c.R,c.R);
            else if(c.Kind==3)dc.DrawLine(new Pen(brush,c.Size),new(c.X,c.Y),new(c.X+c.W,c.Y+c.H));
            else {var text=new FormattedText(c.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),c.Size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=c.W,MaxTextHeight=c.H,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=c.Align==2?TextAlignment.Center:c.Align==1?TextAlignment.Right:TextAlignment.Left};dc.DrawText(text,new(c.X,c.Y+(c.H-text.Height)/2));}
        }
        dc.Pop();
    }
}
