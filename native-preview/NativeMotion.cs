using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

public static class ClearDesktopMotion {
 static readonly Dictionary<Window,int> Versions=new Dictionary<Window,int>();
 static readonly HashSet<Window> Closing=new HashSet<Window>();
 static readonly Dictionary<UIElement,double> MagnificationTargets=new Dictionary<UIElement,double>();
 static bool Enabled {get{return SystemParameters.ClientAreaAnimation;}}
 static DoubleAnimation Motion(double value,int ms) {
  return new DoubleAnimation(value,TimeSpan.FromMilliseconds(ms)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}};
 }
 static TransformGroup IconTransforms(UIElement icon) {
  var group=icon.RenderTransform as TransformGroup;
  if(group!=null && group.Children.Count==4)return group;
  group=new TransformGroup();group.Children.Add(new ScaleTransform(1,1));group.Children.Add(new TranslateTransform());group.Children.Add(new TranslateTransform());group.Children.Add(new ScaleTransform(1,1));
  icon.RenderTransformOrigin=new Point(.5,1);icon.RenderTransform=group;return group;
 }
 static UIElement Icon(Button button) {
  var panel=button.Content as StackPanel;
  return panel!=null && panel.Children.Count>0?panel.Children[0]:null;
 }
 static void Magnify(UIElement icon,double scale) {
  double previous;if(MagnificationTargets.TryGetValue(icon,out previous)&&Math.Abs(previous-scale)<.006)return;MagnificationTargets[icon]=scale;
  var group=IconTransforms(icon);var size=(ScaleTransform)group.Children[0];var lift=(TranslateTransform)group.Children[1];
  size.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(scale,100));size.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(scale,100));lift.BeginAnimation(TranslateTransform.YProperty,Motion(-(scale-1)*15,100));
 }
 public static void AttachDock(StackPanel dock) {
  var moveClock=System.Diagnostics.Stopwatch.StartNew();
  foreach(var item in dock.Children) {
   var button=item as Button;if(button==null)continue;var icon=Icon(button);if(icon==null)continue;IconTransforms(icon);
   button.PreviewMouseLeftButtonDown+=(s,e)=>{if(!Enabled)return;var press=(ScaleTransform)IconTransforms(icon).Children[3];press.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(.93,60));press.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(.93,60));};
   button.PreviewMouseLeftButtonUp+=(s,e)=>ResetPress(icon);
   button.MouseLeave+=(s,e)=>ResetPress(icon);
   button.Click+=(s,e)=>Bounce(icon);
  }
  dock.MouseMove+=(s,e)=>{
   if(!Enabled||moveClock.ElapsedMilliseconds<12)return;moveClock.Restart();double x=e.GetPosition(dock).X;
   foreach(var item in dock.Children){var button=item as Button;if(button==null)continue;var icon=Icon(button);if(icon==null)continue;double center=button.TranslatePoint(new Point(button.ActualWidth/2,0),dock).X;double distance=Math.Abs(x-center);double weight=distance>=105?0:(1+Math.Cos(Math.PI*distance/105))/2;Magnify(icon,1+.32*weight);}
  };
  dock.MouseLeave+=(s,e)=>{foreach(var item in dock.Children){var button=item as Button;if(button==null)continue;var icon=Icon(button);if(icon!=null)Magnify(icon,1);}};
 }
 static void ResetPress(UIElement icon){var press=(ScaleTransform)IconTransforms(icon).Children[3];press.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(1,120));press.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(1,120));}
 static void Bounce(UIElement icon) {
  if(!Enabled)return;ResetPress(icon);var jump=(TranslateTransform)IconTransforms(icon).Children[2];var frames=new DoubleAnimationUsingKeyFrames();
  frames.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
  frames.KeyFrames.Add(new SplineDoubleKeyFrame(-13,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)),new KeySpline(.2,.6,.4,1)));
  frames.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(290)),new KeySpline(.4,0,.8,1)));
  frames.KeyFrames.Add(new SplineDoubleKeyFrame(-4,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(390))));
  frames.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(510))));
  jump.BeginAnimation(TranslateTransform.YProperty,frames);
 }
 static int Advance(Window w){int value;Versions.TryGetValue(w,out value);Versions[w]=++value;return value;}
 public static void ShowLauncher(Window window) {
  Advance(window);Closing.Remove(window);var surface=window.Content as FrameworkElement;
  if(surface==null){window.Show();return;}surface.RenderTransformOrigin=new Point(.5,.8);var scale=surface.RenderTransform as ScaleTransform;
  if(scale==null){scale=new ScaleTransform(1,1);surface.RenderTransform=scale;}
  surface.BeginAnimation(UIElement.OpacityProperty,null);scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
  surface.Opacity=Enabled?0:1;scale.ScaleX=scale.ScaleY=Enabled?.96:1;window.Show();
  if(Enabled){surface.BeginAnimation(UIElement.OpacityProperty,Motion(1,200));scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(1,220));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(1,220));}
 }
 public static void HideLauncher(Window window) {
  if(!window.IsVisible || Closing.Contains(window))return;
  if(!Enabled){window.Hide();return;}int version=Advance(window);Closing.Add(window);var surface=window.Content as FrameworkElement;
  if(surface==null){Closing.Remove(window);window.Hide();return;}var fade=Motion(0,140);
  fade.Completed+=(s,e)=>{if(Versions[window]==version){window.Hide();Closing.Remove(window);}};
  surface.BeginAnimation(UIElement.OpacityProperty,fade);var scale=surface.RenderTransform as ScaleTransform;
  if(scale!=null){scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(.97,140));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(.97,140));}
 }
}
