using System.Drawing;
using System.Drawing.Drawing2D;

namespace AxiWidgets;
internal static class PointerArtwork
{
    internal static nint Create()
    {
        using var bitmap=new Bitmap(32,32);
        using(var g=Graphics.FromImage(bitmap)){
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using var p=new GraphicsPath();
            p.AddLine(10,27,4,17);p.AddBezier(4,17,2,13,5,12,7,15);p.AddLine(7,15,10,18);
            p.AddLine(10,18,10,7);p.AddBezier(10,7,10,3,14,3,14,7);p.AddLine(14,7,14,14);
            p.AddLine(14,14,14,4);p.AddBezier(14,4,14,1,18,1,18,4);p.AddLine(18,4,18,14);
            p.AddLine(18,14,18,5);p.AddBezier(18,5,18,2,22,2,22,5);p.AddLine(22,5,22,15);
            p.AddLine(22,15,22,8);p.AddBezier(22,8,22,5,26,5,26,8);p.AddLine(26,8,26,20);
            p.AddBezier(26,20,26,24,24,26,23,28);p.AddLine(23,28,10,27);p.CloseFigure();
            using var rim=new Pen(Color.FromArgb(255,30,30,34),1.5f);g.FillPath(Brushes.White,p);g.DrawPath(rim,p);
        }
        var icon=bitmap.GetHicon();
        try{
            if(!Native.GetIconInfo(icon,out var info))return 0;
            try{info.Icon=0;info.X=15;info.Y=15;return Native.CreateIconIndirect(ref info);}
            finally{if(info.Mask!=0)Native.DeleteObject(info.Mask);if(info.Color!=0)Native.DeleteObject(info.Color);}
        }finally{Native.DestroyIcon(icon);}
    }
}
