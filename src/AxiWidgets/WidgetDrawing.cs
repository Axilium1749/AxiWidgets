using System;
using System.Collections.Generic;
using System.Globalization;
using AxiWidgets.Core;

namespace AxiWidgets;
internal sealed class WidgetDrawing
{
    internal readonly List<NativeCard.Command> Commands=[];
    const uint White=0xFFFFFFFF,Muted=0xA6FFFFFF,Orange=0xFFFF7835,Yellow=0xFFFFC328;
    void C(int kind,float x,float y,float w,float h,uint color,float r=0,float size=1,string text="",int align=0)=>Commands.Add(new(){Kind=kind,X=x,Y=y,W=w,H=h,R=r,Size=size,Color=color,Text=text,Align=align});
    void Text(string text,float x,float y,float w,float h,float size,uint color=White,int align=0)=>C(2,x,y,w,h,color,size:size,text:text,align:align);
    void Panel(float x,float y,float w,float h,float r=14){C(0,x,y,w,h,0x12FFFFFF,r);C(1,x,y,w,h,0x26FFFFFF,r,.6f);}
    void Line(float x,float y,float w,float h,uint color=0x26FFFFFF,float size=.6f)=>C(3,x,y,w,h,color,size:size);
    void Base(uint accent){C(0,30,10,25,3,accent,1.5f);C(0,309,371,25,3,accent,1.5f);}
    internal static WidgetDrawing Calendar(DateTime now)
    {
        var d=new WidgetDrawing();var culture=CultureInfo.CurrentCulture;var cal=culture.DateTimeFormat.Calendar;
        d.Base(Orange);d.Panel(16,18,332,34,16);
        d.Text(now.ToString("MMMM",culture).ToUpper(culture),28,18,230,34,16,Orange);d.Text(now.ToString("yyyy",culture),270,18,65,34,16,Muted,1);
        d.Panel(18,62,328,58);d.C(0,32,72,6,38,Orange,3);
        d.Text(now.ToString("dddd",culture),52,64,210,54,24);d.Text(cal.GetDayOfMonth(now).ToString(culture),265,64,65,54,28,Orange);
        d.Panel(18,130,328,230);var names=culture.DateTimeFormat.ShortestDayNames;
        for(int col=0;col<7;col++)d.Text(names[(col+1)%7],26+col*44,139,44,24,13,Muted,2);
        d.Line(27,169,310,0);
        var days=CalendarGrid.Build(now,culture);
        for(int i=0;i<42;i++){float x=26+i%7*44,y=174+i/7*29;var day=days[i];if(day.Today)d.C(0,x+5,y+1,34,27,Orange,6);d.Text(cal.GetDayOfMonth(day.Date).ToString(culture),x,y,44,29,14,day.CurrentMonth?White:0x65FFFFFF,2);}
        return d;
    }
    internal static WidgetDrawing Reminders(IReadOnlyList<ReminderItem> items,int offset)
    {
        var d=new WidgetDrawing();d.Base(Yellow);d.Panel(16,18,332,58);
        d.C(0,26,29,34,34,Yellow,17);for(int i=0;i<3;i++)d.Line(37,40+i*5,i==2?7:12,0,0xFF252525,2);
        d.Text("Reminders",74,20,170,54,23);d.C(0,265,33,72,29,0x16FFFFFF,14);d.Text($"{items.Count} tasks",265,33,72,29,13,Muted,2);
        d.Panel(16,86,332,222);
        if(items.Count==0)d.Text("No reminders yet",40,160,284,40,16,Muted,2);
        for(int row=0;row<4&&offset+row<items.Count;row++){
            var item=items[offset+row];float y=92+row*52;
            d.C(item.Done?0:1,36,y+15,20,20,item.Done?Yellow:0xA6FFFFFF,5,item.Done?1:1.5f);
            if(item.Done){d.Line(41,y+25,4,4,0xFF242424,2);d.Line(45,y+29,7,-9,0xFF242424,2);}
            d.Text(item.Text,76,y+2,224,48,15,item.Done?Muted:White);
            if(item.Done)d.Line(76,y+27,Math.Min(item.Text.Length*7,220),0,Muted,1);
            d.C(1,315,y+20,9,12,Muted,1,1);d.Line(313,y+18,13,0,Muted,1);d.Line(318,y+15,4,0,Muted,1);
            if(row<3&&offset+row+1<items.Count)d.Line(29,y+51,306,0);
        }
        if(items.Count>4){float h=196f*4/items.Count,y=98+(196-h)*offset/(items.Count-4);d.C(0,341,y,2,h,0x60FFFFFF,1);}
        d.Panel(18,322,328,45);d.Text("+",34,322,26,45,28,Yellow);d.Line(70,334,0,21,Muted,1);d.Text("Add a reminder",84,322,240,45,15,Muted);
        return d;
    }
}
