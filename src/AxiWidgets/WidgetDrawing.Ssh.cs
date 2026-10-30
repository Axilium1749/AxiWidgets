using System;
using System.Collections.Generic;
using AxiWidgets.Core;
namespace AxiWidgets;
internal sealed partial class WidgetDrawing {
 internal static WidgetDrawing Ssh(IReadOnlyList<SshConnection> items,IReadOnlyDictionary<Guid,long?> latency,double scroll,bool animated=true){var d=new WidgetDrawing();const uint green=0xFF38EE65;
  d.Text(">",16,7,15,23,21,green);d.C(8,31,7,170,23,White,size:17,text:"SSH");d.Line(337,14,0,14,green,1.8f);d.Line(330,21,14,0,green,1.8f);d.Line(1,34,362,0,0x305F739A,.6f);
  if(animated)d.C(6,8,(float)scroll,340,122,0,r:35);
  if(items.Count==0)d.Text("Add servers with +",20,65,324,50,13,Muted,2);
  int first=Math.Max(0,(int)(scroll/40)-5),last=Math.Min(items.Count,first+15);
  for(int i=first;i<last;i++){var c=items[i];float y=35+i*40-(animated?0:(float)scroll);if(animated)d.C(4,0,y,364,40,0,text:c.Id.ToString());bool known=latency.TryGetValue(c.Id,out var ms);uint status=known?(ms.HasValue?green:0xFFFF4956):0xFF8492AC;
   d.C(0,14,y+15,11,11,status,5.5f);d.C(8,36,y+2,231,20,White,size:14,text:c.Name);d.Text((c.User.Length>0?c.User+"@":"")+c.Host,36,y+21,231,17,11,0xFFB2C2E4);d.C(8,271,y+6,65,29,0xFFB2C2E4,size:13,text:ms.HasValue?$"{ms} ms":"—",align:1);if(i+1<items.Count)d.Line(6,y+39,340,0,0x305F739A,.6f);if(animated)d.C(5,0,0,0,0,0);
  }
  if(animated)d.C(7,353,39,3,Math.Max(12,115f*122/Math.Max(122,items.Count*40)),0x608B96B2,Math.Max(0,items.Count*40-122),115);
  return d;
 }
}
internal sealed partial class ClockWindow {
 private bool IsList=>State.Kind is "Reminders" or "SSH";
 private double RowHeight=>State.Kind=="SSH"?40:52;
 private double ListTop=>State.Kind=="SSH"?35:92;
 private double ListHeight=>State.Kind=="SSH"?122:208;
 private int ListCount=>State.Kind=="SSH"?manager.Ssh.Store.Items.Count:manager.Reminders.Items.Count;
 private void SshChanged(){if(closed)return;ResizeScroll();if(scroll>scrollPhysics.Maximum)SettleScroll();RefreshClock();}
 private void ListClick(){if(State.Kind!="SSH"){ReminderClick();return;}var(x,y)=LocalPointer();if(y<35){if(x>=316)manager.Ssh.OpenSettings();return;}int index=(int)Math.Floor((y-35+scroll)/40);if(y<157&&index>=0&&index<manager.Ssh.Store.Items.Count)SshService.Connect(manager.Ssh.Store.Items[index]);}
 internal void SmokeSshScroll(){
  var original=Native.Bounds(Handle);var p=Display.For(original);Native.GetCursorPos(out var cursor);
  try{
   Native.SetCursorPos((int)(original.X+100*p.Scale),(int)(original.Y+70*p.Scale));SetEdit(false);BeginListPointer();
   if(!relativeInput)throw new InvalidOperationException("SSH capture failed");
   RelativeListPointer(0,-70*p.Scale);if(!listDragging||scroll<=0||Native.Bounds(Handle)!=original)throw new InvalidOperationException("SSH drag failed");
   EndListPointer(false);StopScrollMotion();
   scrollPhysics.Jump(0);PresentScroll();Native.SetCursorPos((int)(original.X+100*p.Scale),(int)(original.Y+70*p.Scale));BeginListPointer();RelativeListPointer(0,45*p.Scale);
   if(scroll>=0)throw new InvalidOperationException("SSH overscroll failed");EndListPointer(false);StopScrollMotion();SettleScroll();
   if(relativeInput||listPressed)throw new InvalidOperationException("SSH pointer cleanup failed");
  }finally{EndListPointer(false);StopScrollMotion();Native.SetCursorPos(cursor.X,cursor.Y);}
 }
}
