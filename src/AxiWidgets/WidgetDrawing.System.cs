using System;
using System.Collections.Generic;
using System.Globalization;
namespace AxiWidgets;
internal sealed partial class WidgetDrawing
{
    // Reference card is 1476 x 656: scale every measurement to 364 DIP wide.
    internal const float SystemHeight=162, SystemRadius=30;
    void Gauge(float cx,float cy,double value,uint color,bool animated=false)
    {
        const float radius=33.5f,thickness=6.5f;
        void Arc(double sweep,uint ink){
            if(sweep<=0)return;
            C(9,cx,cy,radius,(float)sweep,ink,135,thickness);
            foreach(double angle in new[]{135.0,135+sweep}){
                double a=angle*Math.PI/180;
                C(0,cx+radius*(float)Math.Cos(a)-thickness/2,cy+radius*(float)Math.Sin(a)-thickness/2,thickness,thickness,ink,thickness/2);
            }
        }
        Arc(270,0xFF454D57);
        if(animated)C(11,cx,cy,radius,(float)Math.Clamp(value,0,100),color,size:thickness);
        else Arc(270*Math.Clamp(value,0,100)/100,color);
    }
    internal static WidgetDrawing SystemCard(SystemSample m,IReadOnlyList<double>? cpuHistory=null,bool animated=false)
    {
        var d=new WidgetDrawing();const uint green=0xFF73EF67,cyan=0xFF00D6E8,purple=0xFF8A30F5,red=0xFFFF5562,muted=0xFFB5BECD,divider=0x665F6D7E;
        void Label(string value,float x,float y,float w,float h,float size,uint color=White,int align=0)=>d.C(8,x,y,w,h,color,size:size,text:value,align:align);
        string Percent(double value)=>value<0?"—":$"{value:0}%";
        double ram=m.RamTotal>0?m.RamUsed/m.RamTotal*100:-1;
        foreach(var column in new[]{(X:60f,Title:"CPU",Value:m.Cpu,Color:green),(X:182f,Title:"RAM",Value:ram,Color:cyan),(X:304f,Title:"GPU",Value:m.Gpu,Color:purple)}){
            Label(column.Title,column.X-40,7,80,14,10,White,2);
            d.Gauge(column.X,59,column.Value,column.Color,animated);
            Label(Percent(column.Value),column.X-32,46,64,27,21,White,2);
        }
        d.Line(120.5f,9,0,111,divider,.6f);d.Line(243.5f,9,0,111,divider,.6f);
        Label("CLOCK",13,94,42,13,9,muted);Label(m.Ghz<0?"—":$"{m.Ghz:0.0} GHz",57,94,51,13,10.5f,White,1);
        // The gauge ends at 86 DIP; center this row above the 126.5 DIP divider.
        Label("USED",130,96.25f,31,20,10.5f,muted);
        string memory=m.RamTotal<=0?"—":$"{m.RamUsed/1073741824:0.0} / {m.RamTotal/1073741824:0.0} GB";
        Label(memory,163,96.25f,74,20,11,White,1);

        Label("VRAM",257,95,37,13,9.5f,red);
        string vram=m.VramUsed<0||m.VramTotal<=0?"—":string.Create(CultureInfo.InvariantCulture,$"{m.VramUsed/1073741824:0.#}/{m.VramTotal/1073741824:0.#} GB");
        Label(vram,293,95,58,13,10,White,1);
        d.C(0,257,111,94,7,0xFF363E47,3.5f);d.C(1,257,111,94,7,0x337F8995,3.5f,.4f);
        if(animated)d.C(12,257,111,m.VramTotal>0?(float)(94*Math.Clamp(m.VramUsed/m.VramTotal,0,1)):0,7,red,3.5f);
        else if(m.VramTotal>0&&m.VramUsed>0)d.C(0,257,111,(float)(94*Math.Clamp(m.VramUsed/m.VramTotal,0,1)),7,red,3.5f);
        // Fixed 0–100% scale; the graph shows actual CPU samples, never decorative data.
        if(cpuHistory!=null&&cpuHistory.Count>1){
            if(animated){d.C(14,13,108,95,13,0);d.C(0,0,0,0,0,0);}
            float X(int i)=>13+(i+30-cpuHistory.Count)*95f/29;float Y(double v)=>119-(float)Math.Clamp(v,0,100)*.11f;
            for(int i=1;i<cpuHistory.Count;i++){
                if(cpuHistory[i]<0||cpuHistory[i-1]<0)continue;
                float x=X(i-1),y=Y(cpuHistory[i-1]),next=Y(cpuHistory[i]);
                d.C(0,x,Math.Min(y,next),95f/29,120-Math.Min(y,next),0x1673EF67);
                d.Line(x,y,95f/29,next-y,green,1.1f);
                d.C(0,x-.55f,y-.55f,1.1f,1.1f,green,.55f);
            }
            if(animated)d.C(15,0,0,0,0,0);
        }
        d.Line(7,126.5f,350,0,divider,.6f);d.Line(182,134,0,20,divider,.6f);
        void Network(double bytes,float center,bool download){
            double bits=bytes*8;string unit=bits>=1000000?"Mbit/s":bits>=1000?"Kbit/s":"bit/s";
            double value=bits>=1000000?bits/1000000:bits>=1000?bits/1000:bits;
            string number=bytes<0?"—":value.ToString(value<10?"0.#":"0",CultureInfo.InvariantCulture);
            float Width(string text,float size)=> (float)new System.Windows.Media.FormattedText(text,CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"),
                System.Windows.FontStyles.Normal,System.Windows.FontWeights.SemiBold,System.Windows.FontStretches.Normal),size,System.Windows.Media.Brushes.White,1).WidthIncludingTrailingWhitespace;
            float numberWidth=Width(number,21),unitWidth=Width(unit,10);
            // Center the actual arrow + number + units, including variable digit counts.
            float left=center-(12+7+numberWidth+5+unitWidth)/2,ax=left+6;
            const uint amber=0xFFFFC354;const float cy=144.25f;
            float top=cy-6.5f,bottom=cy+6.5f;
            d.Line(ax,top,0,bottom-top,amber,2.1f);
            float end=download?bottom:top,other=download?end-5:end+5;
            d.Line(ax,end,-5,other-end,amber,2.1f);d.Line(ax,end,5,other-end,amber,2.1f);
            d.C(0,ax-1.05f,end-1.05f,2.1f,2.1f,amber,1.05f);
            Label(number,left+19,cy-14,numberWidth+1,28,21);
            Label(unit,left+19+numberWidth+5,cy-8,unitWidth+1,16,10,muted);
        }
        Network(m.Down,91,true);Network(m.Up,273,false);
        return d;
    }
}
