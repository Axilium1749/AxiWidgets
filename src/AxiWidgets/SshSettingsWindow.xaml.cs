using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media.Animation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AxiWidgets.Core;
namespace AxiWidgets;
internal partial class SshSettingsWindow : Window {
 readonly SshService service;readonly ObservableCollection<SshConnection> items;SshConnection? current;bool loading;
 internal SshSettingsWindow(SshService service){this.service=service;items=new(service.Store.Items.Select(x=>x with{}));InitializeComponent();SourceInitialized+=(_,_)=>DesktopHost.GalleryBackdrop(new WindowInteropHelper(this).Handle);Servers.ItemsSource=items;Servers.SelectedIndex=items.Count>0?0:-1;Fields.IsEnabled=items.Count>0;
 service.Changed+=RefreshRows;Loaded+=(_,_)=>RefreshRows();Servers.ItemContainerGenerator.StatusChanged+=ContainersChanged;
 ServerName.TextChanged+=(_,_)=>{if(!loading&&current!=null){current.Name=ServerName.Text.Trim();RefreshRows();}};
 dragTimer.Tick+=(_,_)=>DragTick();Deactivated+=(_,_)=>EndListDrag();
 Closed+=(_,_)=>{EndListDrag();service.Changed-=RefreshRows;Servers.ItemContainerGenerator.StatusChanged-=ContainersChanged;};
 }
 void Capture(){if(current==null||loading)return;current.Name=ServerName.Text.Trim();current.Host=Host.Text.Trim();current.Port=int.TryParse(Port.Text,out int p)?p:0;current.User=User.Text.Trim();current.Key=Key.Text.Trim();current.Options=Options.Text.Trim();}
 void Selected(object sender,SelectionChangedEventArgs e){Capture();current=Servers.SelectedItem as SshConnection;loading=true;Fields.IsEnabled=current!=null;ServerName.Text=current?.Name??"";Host.Text=current?.Host??"";Port.Text=(current?.Port??22).ToString();User.Text=current?.User??"";Key.Text=current?.Key??"";Options.Text=current?.Options??"";loading=false;}
 void Add(object sender,RoutedEventArgs e){Capture();var c=new SshConnection();items.Add(c);Servers.SelectedItem=c;ServerName.Focus();}
 void Remove(object sender,RoutedEventArgs e){if(current==null)return;int index=Servers.SelectedIndex;items.Remove(current);Servers.SelectedIndex=Math.Min(index,items.Count-1);}
 readonly DispatcherTimer dragTimer=new(){Interval=TimeSpan.FromMilliseconds(50)};
 SshConnection? dragged;Point press;bool dragging;
 void ContainersChanged(object? sender,EventArgs e)=>RefreshRows();
 void RefreshRows(){
  foreach(var c in items)if(Servers.ItemContainerGenerator.ContainerFromItem(c) is ListBoxItem row){
   row.ApplyTemplate();
   if(row.Template.FindName("ServerTitle",row) is TextBlock name)name.Text=c.Name;
   if(row.Template.FindName("Dot",row) is System.Windows.Shapes.Ellipse dot){
    bool valid=service.Store.Items.Any(x=>x.Id==c.Id&&x.Host==c.Host);
    dot.Fill=new SolidColorBrush(valid&&service.Latency.TryGetValue(c.Id,out var ms)?ms.HasValue?Color.FromRgb(56,238,101):Color.FromRgb(255,73,86):Color.FromRgb(132,146,172));
   }
  }
 }
 void ListDown(object sender,MouseButtonEventArgs e){
  if(ItemsControl.ContainerFromElement(Servers,e.OriginalSource as DependencyObject) is not ListBoxItem row)return;
  Capture();Servers.SelectedItem=row.DataContext;dragged=row.DataContext as SshConnection;press=e.GetPosition(Servers);e.Handled=true;
 }
 void ListMove(object sender,MouseEventArgs e){
  if(dragged==null)return;if(e.LeftButton!=MouseButtonState.Pressed){EndListDrag();return;}
  var point=e.GetPosition(Servers);
  if(!dragging){if(Math.Abs(point.Y-press.Y)<SystemParameters.MinimumVerticalDragDistance&&Math.Abs(point.X-press.X)<SystemParameters.MinimumHorizontalDragDistance)return;
   dragging=true;Servers.CaptureMouse();Servers.Cursor=Cursors.SizeNS;dragTimer.Start();
  }
  ReorderAt(point);e.Handled=true;
 }
 Dictionary<Guid,double> RowPositions(){
  var result=new Dictionary<Guid,double>();
  foreach(var c in items)if(Servers.ItemContainerGenerator.ContainerFromItem(c) is ListBoxItem row)result[c.Id]=row.TranslatePoint(new Point(),Servers).Y;
  return result;
 }
 void AnimateOrder(Dictionary<Guid,double> before){
  Servers.UpdateLayout();
  foreach(var c in items)if(Servers.ItemContainerGenerator.ContainerFromItem(c) is ListBoxItem row){
   var transform=row.RenderTransform as TranslateTransform;
   double top=row.TranslatePoint(new Point(),Servers).Y-(transform?.Y??0);
   if(transform==null){transform=new TranslateTransform();row.RenderTransform=transform;}
   double offset=before.TryGetValue(c.Id,out var old)?old-top:0;
   transform.BeginAnimation(TranslateTransform.YProperty,null);transform.Y=0;
   if(Math.Abs(offset)>.1)transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(offset,0,TimeSpan.FromMilliseconds(190)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
  }
 }
 void ReorderAt(Point point){
  if(dragged==null)return;
  // Hit logical slots rather than animated visuals to avoid repeated swaps mid-animation.
  SshConnection? target=null;
  foreach(var c in items)if(Servers.ItemContainerGenerator.ContainerFromItem(c) is ListBoxItem row){
   double top=row.TranslatePoint(new Point(),Servers).Y-(row.RenderTransform is TranslateTransform t?t.Y:0);
   if(point.Y>=top&&point.Y<top+row.ActualHeight){target=c;break;}
  }
  if(target==null)return;int from=items.IndexOf(dragged),to=items.IndexOf(target);
  if(from>=0&&to>=0&&from!=to){var before=RowPositions();Capture();items.Move(from,to);Servers.SelectedItem=dragged;AnimateOrder(before);RefreshRows();}
 }
 static T? Child<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);if(c is T result)return result;if(Child<T>(c) is {} found)return found;}return null;}
 void DragTick(){
  if(!dragging||Mouse.LeftButton!=MouseButtonState.Pressed){EndListDrag();return;}
  var point=Mouse.GetPosition(Servers);var scroll=Child<ScrollViewer>(Servers);
  if(point.Y<24)scroll?.ScrollToVerticalOffset(Math.Max(0,scroll.VerticalOffset-12));
  else if(point.Y>Servers.ActualHeight-24)scroll?.ScrollToVerticalOffset(scroll.VerticalOffset+12);
  ReorderAt(new Point(Math.Clamp(point.X,8,Math.Max(8,Servers.ActualWidth-8)),Math.Clamp(point.Y,4,Math.Max(4,Servers.ActualHeight-4))));
 }
 void EndListDrag(){dragTimer.Stop();dragging=false;dragged=null;Servers.Cursor=null;if(Servers.IsMouseCaptured)Servers.ReleaseMouseCapture();}
 void ListUp(object sender,MouseButtonEventArgs e){EndListDrag();}
 void ListLostCapture(object sender,MouseEventArgs e){EndListDrag();}
 void Import(object sender,RoutedEventArgs e){try{Capture();var copy=items.ToList();int count=SshStore.Merge(copy,SshConfig.Import(SshService.ConfigPath));foreach(var item in copy.Skip(items.Count))items.Add(item);if(Servers.SelectedIndex<0&&items.Count>0)Servers.SelectedIndex=0;Status.Text=$"Imported {count} new connections.";}catch(Exception ex){Status.Text=ex.Message;}}
 void Browse(object sender,RoutedEventArgs e){var dialog=new Microsoft.Win32.OpenFileDialog {Title="Select SSH private key"};if(dialog.ShowDialog(this)==true)Key.Text=dialog.FileName;}
 void Save(object sender,RoutedEventArgs e){try{Capture();service.Store.Save(items);Close();}catch(Exception ex){Status.Text=ex.Message;}}
 internal void SmokeRender(){
  if(items.Count>=2){
   var first=items[0];Servers.SelectedItem=first;UpdateLayout();
   if(Servers.ItemContainerGenerator.ContainerFromIndex(1) is not ListBoxItem row)throw new InvalidOperationException("SSH row missing");
   dragged=first;dragging=true;
   var point=row.TranslatePoint(new Point(row.ActualWidth/2,row.ActualHeight/2),Servers);ReorderAt(point);
   if(Servers.ItemContainerGenerator.ContainerFromItem(first) is not ListBoxItem animatedRow||animatedRow.RenderTransform is not TranslateTransform animation||!animation.HasAnimatedProperties)throw new InvalidOperationException("SSH reorder animation missing");
   EndListDrag();
   if(items[1]!=first||dragTimer.IsEnabled||Servers.IsMouseCaptured)throw new InvalidOperationException("SSH reorder/cleanup failed");
   // Cancelled editing must leave the saved order unchanged.
   if(service.Store.Items[0].Id!=first.Id)throw new InvalidOperationException("Draft order saved prematurely");
  }
  RefreshRows();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(760,470,96,96,System.Windows.Media.PixelFormats.Pbgra32);UpdateLayout();bitmap.Render(this);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
  using var file=System.IO.File.Create(System.IO.Path.Combine(Log.Folder,"ssh-settings.png"));encoder.Save(file);
 }
 void DragTitle(object sender,MouseButtonEventArgs e){
  if(e.LeftButton!=MouseButtonState.Pressed)return;
  for(DependencyObject? node=e.OriginalSource as DependencyObject;node!=null&&node!=sender;){
   if(node is System.Windows.Controls.Primitives.ButtonBase)return;
   node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
  }
  EndListDrag();DragMove();e.Handled=true;
 }
 void Cancel(object sender,RoutedEventArgs e)=>Close();
}
