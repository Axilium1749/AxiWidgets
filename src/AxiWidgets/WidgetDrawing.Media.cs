using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Threading;
namespace AxiWidgets;
[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
internal struct MediaState {
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Title;
 [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Artist;
 public double Position,Duration,Rate;public int Playing,Available,Controls;public uint Color;public int Error;public uint Process;
}
[StructLayout(LayoutKind.Sequential)]
internal struct AudioLevels {public float Level,Bass,Hit;public int Error;public ulong Packets;public uint Process;public int Fallback;}
internal static class MediaNative {
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_AudioSelfTest",CallingConvention=CallingConvention.Cdecl)]internal static extern int AudioSelfTest();
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_AudioPulse",CallingConvention=CallingConvention.Cdecl)]internal static extern int AudioPulse(nint hwnd,out AudioLevels levels);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Palette",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Palette(uint color,int segment);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Timeline",CallingConvention=CallingConvention.Cdecl)]internal static extern int Timeline(nint hwnd,[In]NativeCard.Command[] commands,int count,float fraction,double speed,bool reset);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Attach",CallingConvention=CallingConvention.Cdecl)]internal static extern int Attach(nint hwnd,bool attach);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Read",CallingConvention=CallingConvention.Cdecl)]internal static extern int Read(out MediaState state);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Command",CallingConvention=CallingConvention.Cdecl)]internal static extern int Command(int action,double position);
 [DllImport("AxiWidgets.Native.dll",EntryPoint="AxiMedia_Visual",CallingConvention=CallingConvention.Cdecl)]internal static extern int Visual(nint hwnd,bool active,uint color,int feedback,int pressed,uint paletteSeed=0);
}
internal sealed partial class WidgetDrawing {
 internal static WidgetDrawing MediaCard(MediaState m,double position,bool preview=true){
 var d=new WidgetDrawing();uint source=m.Color==0?0xff70bfff:m.Color;uint accent=MediaNative.Palette(source,-1);
 string title=m.Available!=0?(string.IsNullOrWhiteSpace(m.Title)?"Unknown track":m.Title):m.Error<0?"Media unavailable":"Nothing playing";
 string artist=m.Available!=0?m.Artist:"";
 float titleWidth=320;
 if(!string.IsNullOrWhiteSpace(artist)){
  var measured=new System.Windows.Media.FormattedText(title,System.Globalization.CultureInfo.CurrentCulture,System.Windows.FlowDirection.LeftToRight,
   new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"),System.Windows.FontStyles.Normal,System.Windows.FontWeights.SemiBold,System.Windows.FontStretches.Normal),16,System.Windows.Media.Brushes.White,1);
  titleWidth=(float)Math.Clamp(measured.WidthIncludingTrailingWhitespace+2,20,210);
 }
 d.C(8,22,10,titleWidth,24,White,size:16,text:title);
 if(!string.IsNullOrWhiteSpace(artist))d.Text(artist,22+titleWidth+12,10,308-titleWidth,24,11,Muted,1);
 if(preview){d.C(0,162,61,40,40,accent,20);for(int i=0;i<40;i++){double a=i*Math.PI/20;float x=182+(float)Math.Sin(a)*32,y=81-(float)Math.Cos(a)*32;d.C(0,x-1.3f,y-1.3f,2.6f,2.6f,MediaNative.Palette(source,i),1.3f);}}
 d.Text(m.Playing!=0?"Ⅱ":"▶",163,61,38,40,19,0xff17232a,2);
 void Skip(float x,bool next,int flag){
  uint ink=(m.Controls&flag)!=0?White:0x55ffffff;
  float edge=next?x+23:x+9;
  d.Line(edge,73,0,16,ink,2);
  // Filled vector triangle: no font-dependent side bearings or baseline alignment.
  d.C(10,next?x+7:x+12,73,12,16,ink,align:next?1:0);
 }
 Skip(49,false,2);Skip(283,true,4);
 d.Text("↶",97,60,32,34,28,(m.Controls&8)!=0?White:0x55ffffff,2);
 d.Text("↷",235,60,32,34,28,(m.Controls&8)!=0?White:0x55ffffff,2);
 d.Text("15",97,93,32,16,11,Muted,2);d.Text("15",235,93,32,16,11,Muted,2);
 d.C(0,61,138,242,3,0x40ffffff,1.5f);
 if(preview&&m.Duration>0){float end=(float)(242*Math.Clamp(position/m.Duration,0,1));d.C(0,61,138,end,3,accent,1.5f);d.C(0,58+end,136,7,7,accent,3.5f);}
 string Time(double t)=>TimeSpan.FromSeconds(Math.Max(0,t)).ToString(t>=3600?@"h\:mm\:ss":@"m\:ss");
 d.Text(Time(position),15,129,42,20,10,Muted,2);d.Text(m.Duration>0?Time(m.Duration):"—",307,129,42,20,10,Muted,2);
 return d;
 }
}
internal sealed partial class ClockWindow {
 private uint mediaPaletteSeed=(uint)Random.Shared.Next(1,int.MaxValue);private bool mediaProgressReset=true;
 private MediaState media;private string mediaContentKey="";private bool mediaWasVisible;
 private uint mediaAudioProcess=uint.MaxValue;private int mediaAudioError;private readonly DispatcherTimer mediaAudioTick=new(){Interval=TimeSpan.FromMilliseconds(1000.0/30)};
 private readonly DispatcherTimer mediaTick=new(){Interval=TimeSpan.FromSeconds(1)};
 private readonly DispatcherTimer mediaTransition=new(){Interval=TimeSpan.FromMilliseconds(310)};
 private long mediaStamp;private int mediaHover=-1,mediaPressed=-1;private bool mediaSeeking;private double mediaSeek;
 private double MediaPosition=>mediaSeeking?mediaSeek:Math.Clamp(media.Position+(media.Playing!=0?Stopwatch.GetElapsedTime(mediaStamp).TotalSeconds*media.Rate:0),0,Math.Max(0,media.Duration));
 private void MediaInit(){mediaAudioTick.Tick+=(_,_)=>{if(closed)return;NativeCard.Check(MediaNative.AudioPulse(Handle,out var levels));if(levels.Error<0&&levels.Error!=mediaAudioError)Log.Write($"Media audio HRESULT=0x{levels.Error:X8}");if(mediaAudioProcess!=levels.Process){mediaAudioProcess=levels.Process;Log.Write($"Media audio source: {(levels.Process==0?"system fallback":"process "+levels.Process)}; fallback HRESULT=0x{levels.Fallback:X8}");}mediaAudioError=levels.Error;};mediaTick.Tick+=(_,_)=>MediaDraw();mediaTransition.Tick+=(_,_)=>{mediaTransition.Stop();MediaVisual();};NativeCard.Check(MediaNative.Attach(Handle,true));}
 private void MediaClose(){mediaTick.Stop();mediaTransition.Stop();MediaUp(false);mediaAudioTick.Stop();NativeCard.Check(MediaNative.Attach(Handle,false));}
 private void MediaRefresh(){if(State.Kind!="Media"||closed)return;var old=media;NativeCard.Check(MediaNative.Read(out media));mediaStamp=Stopwatch.GetTimestamp();if(media.Error<0&&media.Error!=old.Error)Log.Write($"Media session HRESULT=0x{media.Error:X8}");
 bool changed=old.Title!=media.Title||old.Artist!=media.Artist;
 if(changed){mediaPaletteSeed=(uint)Random.Shared.Next(1,int.MaxValue);mediaProgressReset=true;NativeCard.Check(MediaNative.Visual(Handle,false,old.Color,mediaHover,0));mediaTransition.Stop();mediaTransition.Start();}
 MediaVisibility();}
 private void MediaVisibility(){if(State.Kind!="Media"||closed)return;mediaTick.Stop();if(IsVisible){if(!mediaWasVisible){mediaContentKey="";}mediaWasVisible=true;MediaDraw();if(media.Playing!=0&&media.Available!=0)mediaTick.Start();}else{mediaAudioTick.Stop();mediaWasVisible=false;mediaTransition.Stop();NativeCard.Check(MediaNative.Visual(Handle,false,media.Color,-1,0));}}
 private void MediaDraw(){if(closed||!IsVisible)return;var d=WidgetDrawing.MediaCard(media,MediaPosition,false);string key=$"{media.Title}|{media.Artist}|{media.Playing}|{media.Controls}|{media.Available}|{media.Color}|{media.Error}";
 if(key!=mediaContentKey){mediaContentKey=key;var content=d.Commands.Where(c=>c.Y<125).ToArray();NativeCard.Check(NativeCard.SetContent(Handle,content,content.Length));}
 MediaVisual();var line=d.Commands.Where(c=>c.Y>=125).Select(c=>{c.Y-=125;return c;}).ToArray();NativeCard.Check(MediaNative.Timeline(Handle,line,line.Length,media.Duration>0?(float)(MediaPosition/media.Duration):-1,!mediaSeeking&&media.Playing!=0&&media.Duration>0?Math.Max(0,media.Rate)/media.Duration:0,mediaProgressReset||mediaSeeking));mediaProgressReset=false;}
 private void MediaVisual(){if(closed)return;bool active=IsVisible&&media.Playing!=0&&media.Available!=0&&!mediaTransition.IsEnabled;if(active){if(!mediaAudioTick.IsEnabled)mediaAudioTick.Start();}else mediaAudioTick.Stop();NativeCard.Check(MediaNative.Visual(Handle,active,media.Color,mediaHover,mediaPressed>=0?1:0,mediaPaletteSeed));}
 private int MediaHit(){var(x,y)=LocalPointer();if(y>=125&&y<=152&&x>=55&&x<=309&&(media.Controls&8)!=0&&media.Duration>0)return 5;if(y<57||y>108)return -1;
 int id=x>=157&&x<=207?0:x>=45&&x<=85?1:x>=279&&x<=319?2:x>=93&&x<=133?3:x>=231&&x<=271?4:-1;
 int flag=id==0?1:id==1?2:id==2?4:8;return id>=0&&(media.Controls&flag)!=0?id:-1;}
 private void MediaMove(bool leave=false){if(State.Kind!="Media"||closed)return;if(mediaSeeking){var(x,_)=LocalPointer();mediaSeek=Math.Clamp((x-61)/242,0,1)*media.Duration;MediaDraw();return;}int hit=leave||Editing?-1:MediaHit();if(hit==5)hit=-1;if(mediaHover!=hit){mediaHover=hit;MediaVisual();}}
 private void MediaDown(){if(State.Kind!="Media")return;mediaPressed=MediaHit();if(mediaPressed<0)return;Native.SetCapture(Handle);mediaSeeking=mediaPressed==5;MediaMove();MediaVisual();}
 private void MediaUp(bool commit){if(State.Kind!="Media"||mediaPressed<0)return;int pressed=mediaPressed;bool seek=mediaSeeking;double position=mediaSeek;mediaPressed=-1;mediaSeeking=false;if(Native.GetCapture()==Handle)Native.ReleaseCapture();
 if(commit&&(seek||MediaHit()==pressed)){int action=pressed<=2?pressed:3;double target=seek?position:Math.Clamp(MediaPosition+(pressed==3?-15:15),0,Math.Max(0,media.Duration));NativeCard.Check(MediaNative.Command(action,target));}
 MediaVisual();}
}

internal sealed partial class Manager {
 internal async System.Threading.Tasks.Task MediaSmoke(){
 ClockWindow? card=null;
 try{
 NativeCard.Check(MediaNative.AudioSelfTest());
 card=new ClockWindow(this,new AxiWidgets.Core.ClockState{Kind="Media"},"desktop");card.Show();
 if(card.WidgetWidth!=364||card.WidgetHeight!=162||!Native.IsWindowEnabled(card.Handle))throw new InvalidOperationException("Media geometry/input");
 await System.Threading.Tasks.Task.Delay(1000);
 NativeCard.Check(MediaNative.Read(out var state));Log.Write($"MEDIA session available={state.Available} controls={state.Controls}");
 if(Environment.GetCommandLineArgs().Contains("--media-controls-check")&&state.Available!=0){
 var original=state;var started=Stopwatch.GetTimestamp();
 try{
 if(state.Playing!=0){NativeCard.Check(MediaNative.Command(0,0));await System.Threading.Tasks.Task.Delay(800);NativeCard.Check(MediaNative.Read(out state));if(state.Playing!=0)throw new InvalidOperationException("Pause was not acknowledged");}
 if((state.Controls&8)!=0&&state.Duration>40){double target=Math.Clamp(state.Position+15,0,state.Duration-5);NativeCard.Check(MediaNative.Command(3,target));await System.Threading.Tasks.Task.Delay(800);NativeCard.Check(MediaNative.Read(out state));if(Math.Abs(state.Position-target)>3)throw new InvalidOperationException("Seek was not acknowledged");}
 Log.Write("MEDIA CONTROLS PASS: live session acknowledged pause and seek.");
 }finally{
 if((original.Controls&8)!=0)NativeCard.Check(MediaNative.Command(3,Math.Min(original.Duration,original.Position+(original.Playing!=0?Stopwatch.GetElapsedTime(started).TotalSeconds:0))));
 NativeCard.Check(MediaNative.Read(out var latest));if(latest.Playing!=original.Playing)NativeCard.Check(MediaNative.Command(0,0));await System.Threading.Tasks.Task.Delay(800);
 }
 }
 for(int i=0;i<5;i++){NativeCard.Check(MediaNative.Visual(card.Handle,true,0xff55ddc7,i,0));NativeCard.Check(MediaNative.Visual(card.Handle,true,0xff55ddc7,i,1));}
 await System.Threading.Tasks.Task.Delay(800);
 NativeCard.Check(MediaNative.AudioPulse(card.Handle,out var audio));
 Log.Write($"MEDIA AUDIO: packets={audio.Packets}, level={audio.Level:F3}, bass={audio.Bass:F3}, hit={audio.Hit:F3}, HRESULT=0x{audio.Error:X8}, process={audio.Process}, fallback=0x{audio.Fallback:X8}");
 if(audio.Error<0)throw new InvalidOperationException($"Audio capture failed: 0x{audio.Error:X8}");
 NativeCard.Check(MediaNative.Visual(card.Handle,false,0xffff8855,-1,0));await System.Threading.Tasks.Task.Delay(500);
 Native.ShowWindow(card.Handle,0);await System.Threading.Tasks.Task.Delay(100);
 card.Close();card=null;
 var preview=new WidgetPreview{Kind="Media",Width=364,Height=162};preview.Measure(new System.Windows.Size(364,162));preview.Arrange(new System.Windows.Rect(0,0,364,162));
 var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(728,324,192,192,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(preview);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(Log.Folder,"media-preview.png")))encoder.Save(file);
 Log.Write("MEDIA SMOKE PASS: native creation, media subscription, animation/feedback/palette transitions, hide and disposal. Playback commands run only with --media-controls-check.");
 }catch(Exception e){Log.Write("MEDIA SMOKE FAIL: "+e);Environment.ExitCode=1;}finally{card?.Close();System.Windows.Application.Current.Shutdown(Environment.ExitCode);}
 }
}



internal sealed partial class Manager {
 internal async System.Threading.Tasks.Task MediaBench(){
 ClockWindow? card=null;MediaState original=default;bool restore=false;
 try{
 foreach(var window in windows.ToArray())window.Close();windows.Clear();
 NativeCard.Check(MediaNative.AudioSelfTest());
 card=new ClockWindow(this,new AxiWidgets.Core.ClockState{Kind="Media"},"desktop");card.Show();
 await System.Threading.Tasks.Task.Delay(2000);NativeCard.Check(MediaNative.Read(out original));
 if(original.Available==0)throw new InvalidOperationException("No media session for benchmark");
 if(original.Playing==0){NativeCard.Check(MediaNative.Command(0,0));restore=true;}
 await System.Threading.Tasks.Task.Delay(3000);NativeCard.Check(MediaNative.Read(out var running));if(running.Playing==0)throw new InvalidOperationException("Media is not playing");
 async System.Threading.Tasks.Task Sample(string phase){
  using var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;var clock=Stopwatch.StartNew();long memory=0,working=0;float bass=0,level=0;
  Log.Write("MEDIA BENCH phase="+phase);
  for(int i=0;i<15;i++){await System.Threading.Tasks.Task.Delay(1000);process.Refresh();memory+=process.PrivateMemorySize64;working+=process.WorkingSet64;NativeCard.Check(MediaNative.AudioPulse(card.Handle,out var audio));bass=Math.Max(bass,audio.Bass);level=Math.Max(level,audio.Level);}
  process.Refresh();double cores=(process.TotalProcessorTime-cpu).TotalSeconds/clock.Elapsed.TotalSeconds;
  Log.Write($"MEDIA BENCH result phase={phase} CPU_total_pct={cores/Environment.ProcessorCount*100:F3} CPU_one_core_pct={cores*100:F2} private_MiB={memory/15.0/1048576:F1} working_MiB={working/15.0/1048576:F1} peak_level={level:F3} peak_bass={bass:F3}");
 }
 await Sample("playing");NativeCard.Check(MediaNative.Command(0,0));await System.Threading.Tasks.Task.Delay(1500);NativeCard.Check(MediaNative.Read(out var paused));if(paused.Playing!=0)throw new InvalidOperationException("Pause was not acknowledged");restore=!restore;
 await Sample("paused");Native.ShowWindow(card.Handle,0);await System.Threading.Tasks.Task.Delay(1000);await Sample("hidden");
 }catch(Exception e){Log.Write("MEDIA BENCH FAIL: "+e);Environment.ExitCode=1;}
 finally{if(restore){MediaNative.Command(0,0);await System.Threading.Tasks.Task.Delay(800);}card?.Close();System.Windows.Application.Current.Shutdown(Environment.ExitCode);}
 }
}
