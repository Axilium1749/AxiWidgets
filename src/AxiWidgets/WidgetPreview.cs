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
        dc.PushTransform(new ScaleTransform(ActualWidth/364,ActualHeight/(Kind=="System"?WidgetDrawing.SystemHeight:384)));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(45,45,49)),new Pen(new SolidColorBrush(Color.FromArgb(44,255,255,255)),.6),new Rect(0,0,364,Kind=="System"?WidgetDrawing.SystemHeight:384),Kind=="System"?WidgetDrawing.SystemRadius:30,Kind=="System"?WidgetDrawing.SystemRadius:30);
        var drawing=Kind=="System"?WidgetDrawing.SystemCard(new(){Cpu=42,Ghz=4.8,RamUsed=10.54*1073741824,RamTotal=31*1073741824.0,Gpu=68,VramUsed=6*1073741824.0,VramTotal=12*1073741824.0,Down=3000000,Up=375000,CpuName="Ryzen 7 7700",GpuName="RTX 3060 Ti",RamName="DDR5 6000 MT/s"},new double[]{15,23,25,31,29,22,25,31,27,30,43,52,48,39,33,24,26,35,44,47,40,31,25,32,41,40,36,42,48,45}):Kind=="Calendar"?WidgetDrawing.Calendar(System.DateTime.Now):WidgetDrawing.Reminders(new ReminderItem[]{new(){Text="Review lecture notes"},new(){Text="Finish overlay design",Done=true},new(){Text="Plan the weekend"}},0);
        foreach(var c in drawing.Commands){
            var brush=new SolidColorBrush(Color.FromArgb((byte)(c.Color>>24),(byte)(c.Color>>16),(byte)(c.Color>>8),(byte)c.Color));
            var rect=new Rect(c.X,c.Y,System.Math.Max(0,c.W),System.Math.Max(0,c.H));
            if(c.Kind==0)dc.DrawRoundedRectangle(brush,null,rect,c.R,c.R);
            else if(c.Kind==1)dc.DrawRoundedRectangle(null,new Pen(brush,c.Size),rect,c.R,c.R);
            else if(c.Kind==9){
                double a=c.R*System.Math.PI/180,b=(c.R+c.H)*System.Math.PI/180;
                var geometry=new StreamGeometry();
                using(var path=geometry.Open()){
                    path.BeginFigure(new(c.X+c.W*System.Math.Cos(a),c.Y+c.W*System.Math.Sin(a)),false,false);
                    path.ArcTo(new(c.X+c.W*System.Math.Cos(b),c.Y+c.W*System.Math.Sin(b)),new Size(c.W,c.W),0,c.H>180,SweepDirection.Clockwise,true,false);
                }
                dc.DrawGeometry(null,new Pen(brush,c.Size),geometry);
            }
            else if(c.Kind==3)dc.DrawLine(new Pen(brush,c.Size),new(c.X,c.Y),new(c.X+c.W,c.Y+c.H));
            else {var text=new FormattedText(c.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,c.Kind==8?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),c.Size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=c.W,MaxTextHeight=System.Math.Max(c.H,c.Size*2),Trimming=TextTrimming.CharacterEllipsis,TextAlignment=c.Align==2?TextAlignment.Center:c.Align==1?TextAlignment.Right:TextAlignment.Left};dc.DrawText(text,new(c.X,c.Y+(c.H-text.Height)/2));}
        }
        dc.Pop();
    }
}
