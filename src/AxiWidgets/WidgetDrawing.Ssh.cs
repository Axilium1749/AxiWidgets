using System;
using System.Collections.Generic;
using System.Linq;
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
 private bool sshPlusPressed;
 private string sshHover="";
 private void SshPress(string id,bool down){if(State.Kind=="SSH"&&!closed){sshHover="";NativeCard.Check(NativeCard.Press(Handle,id,down));}}
 private void SshHover(bool leave=false){
  if(State.Kind!="SSH"||closed||listPressed||sshPlusPressed)return;
  string id="";
  if(!leave&&!Editing){var(x,y)=LocalPointer();
   if(x>=322&&x<=352&&y>=7&&y<35)id="+";
   else if(x>=8&&x<=348&&y>=35&&y<157){int i=(int)Math.Floor((y-35+scroll)/40);if(i>=0&&i<manager.Ssh.Store.Items.Count)id=manager.Ssh.Store.Items[i].Id.ToString();}
  }
  if(id==sshHover)return;sshHover=id;NativeCard.Check(NativeCard.Hover(Handle,id,id.Length==0?0:2));
 }
 private bool IsList=>State.Kind is "Reminders" or "SSH";
 private double RowHeight=>State.Kind=="SSH"?40:52;
 private double ListTop=>State.Kind=="SSH"?35:92;
 private double ListHeight=>State.Kind=="SSH"?122:208;
 private int ListCount=>State.Kind=="SSH"?manager.Ssh.Store.Items.Count:manager.Reminders.Items.Count;
 private void SshChanged(){if(closed)return;ResizeScroll();if(scroll>scrollPhysics.Maximum)SettleScroll();RefreshClock();}
 private void ListClick(){if(State.Kind!="SSH"){ReminderClick();return;}var(x,y)=LocalPointer();if(y<35)return;int index=(int)Math.Floor((y-35+scroll)/40);if(y<157&&index>=0&&index<manager.Ssh.Store.Items.Count)SshService.Connect(manager.Ssh.Store.Items[index]);}
 internal void SmokeSshScroll(){
  var original=Native.Bounds(Handle);var p=Display.For(original);Native.GetCursorPos(out var cursor);
  try{
   SetEdit(false);Native.SetCursorPos((int)(original.X+337*p.Scale),(int)(original.Y+21*p.Scale));BeginListPointer();
   if(!sshPlusPressed||Native.GetCapture()!=Handle)throw new InvalidOperationException("SSH plus press capture failed");
   EndListPointer(false);if(sshPlusPressed||Native.GetCapture()==Handle)throw new InvalidOperationException("SSH plus cancellation failed");
   Native.SetCursorPos((int)(original.X+100*p.Scale),(int)(original.Y+70*p.Scale));BeginListPointer();
   if(!relativeInput)throw new InvalidOperationException("SSH capture failed");
   RelativeListPointer(0,-70*p.Scale);if(!listDragging||scroll<=0||Native.Bounds(Handle)!=original)throw new InvalidOperationException("SSH drag failed");
   EndListPointer(false);StopScrollMotion();
   scrollPhysics.Jump(0);PresentScroll();Native.SetCursorPos((int)(original.X+100*p.Scale),(int)(original.Y+70*p.Scale));BeginListPointer();RelativeListPointer(0,45*p.Scale);
   if(scroll>=0)throw new InvalidOperationException("SSH overscroll failed");EndListPointer(false);StopScrollMotion();SettleScroll();
   if(relativeInput||listPressed)throw new InvalidOperationException("SSH pointer cleanup failed");
  }finally{EndListPointer(false);StopScrollMotion();Native.SetCursorPos(cursor.X,cursor.Y);}
 }
}

internal sealed partial class Manager {
 internal void SshFeedbackSmoke(){
  var saved=Ssh.Store.Items.ToArray();ClockWindow? card=null;
  try{
   if(gallery!=null||editing)throw new InvalidOperationException("Startup opened gallery unexpectedly");
   Ssh.Store.Save(Enumerable.Range(0,3).Select(i=>new SshConnection{Name="Feedback test "+i,Host="127.0.0.1",Port=2200+i}));
   card=new ClockWindow(this,new ClockState{Kind="SSH"},"desktop");card.Show();
   NativeCard.Check(NativeCard.Press(card.Handle,Ssh.Store.Items[0].Id.ToString(),true));
   NativeCard.Check(NativeCard.Press(card.Handle,"",false));
   NativeCard.Check(NativeCard.Press(card.Handle,"+",true));
   NativeCard.Check(NativeCard.Press(card.Handle,"",false));
   var settings=new SshSettingsWindow(Ssh);settings.Show();settings.SmokeRender();settings.Close();
   Log.Write("SSH FEEDBACK PASS: native row/header press and release; reorder animation active; draft order/capture cleanup verified without cursor movement.");
  }catch(Exception e){Log.Write("SSH FEEDBACK FAIL: "+e);Environment.ExitCode=1;}
  finally{card?.Close();Ssh.Store.Save(saved);System.Windows.Application.Current.Shutdown(Environment.ExitCode);}
 }
}
