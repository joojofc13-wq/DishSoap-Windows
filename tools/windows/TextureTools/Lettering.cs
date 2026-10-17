using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Only typesetting instructions are distributed. The painted sheets come from
// the owner's import; no original or generated game images are bundled.
internal static class Lettering
{
    public static bool Contains(string asset,int x,int y) => asset=="gfx/maps/maps3.png"?new Rectangle(776,1386,62,22).Contains(x,y):asset=="gfx/panels/panel8.png"&&new Rectangle(4,236,122,170).Contains(x,y);
    public static void Validate(Bitmap before,Bitmap after,string asset)
    {
        if(before.Size!=after.Size) throw new InvalidDataException("Lettering changed sheet dimensions.");
        int changed=0;
        for(int y=0;y<before.Height;y++) for(int x=0;x<before.Width;x++)
        {
            var a=before.GetPixel(x,y);var b=after.GetPixel(x,y);
            if(a.A!=b.A || (a.B>a.R)!=(b.B>b.R)) throw new InvalidDataException("Lettering changed transparency or the bloom classification.");
            if(a.ToArgb()!=b.ToArgb())
            {
                if(!Contains(asset,x,y)) throw new InvalidDataException("Lettering changed artwork outside its reviewed placement.");
                changed++;
            }
        }
        Console.WriteLine($"Lettering audit PASS: {changed} changed pixels; artwork outside the label, alpha and bloom classification preserved.");
    }
    public static Bitmap Restore(Bitmap source, Bitmap enlarged, string asset)
    {
        var result = enlarged.Clone(new Rectangle(0, 0, enlarged.Width, enlarged.Height), PixelFormat.Format32bppArgb);
        if (result.Width != source.Width * 2 || result.Height != source.Height * 2)
            throw new InvalidDataException("Lettering requires the original sheet and its 2x upscale.");
        if (asset == "gfx/maps/maps3.png")
        {
            using var patch = MakePlate();
            Apply(result, patch, new[] { new PointF(388,693), new PointF(419,693), new PointF(419,704), new PointF(388,704) });
        }
        else if (asset == "gfx/panels/panel8.png")
        {
            using var patch = MakeNeon();
            Apply(result, patch, new[] { new PointF(2,200), new PointF(4,118), new PointF(63,121), new PointF(56,203) });
        }
        else throw new ArgumentException("No reviewed lettering placement for " + asset);
        return result;
    }
    private static Bitmap MakePlate()
    {
        var image = new Bitmap(620,220,PixelFormat.Format32bppArgb);
        using var g=Graphics.FromImage(image);
        g.Clear(Color.FromArgb(62,61,61));
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using var font=new FontFamily("Arial");
        DrawFitted(g,"HERBERT",font,FontStyle.Regular,new RectangleF(18,37,584,142),false);
        return image;
    }
    private static Bitmap MakeNeon()
    {
        var image=new Bitmap(800,540,PixelFormat.Format32bppArgb);
        using var g=Graphics.FromImage(image);
        g.Clear(Color.FromArgb(8,6,30));
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using var font=new FontFamily("Arial");
        string[] lines={"CYBORG","ASSIMILATION","MOVEMENT"};
        for(int i=0;i<lines.Length;i++)
            DrawFitted(g,lines[i],font,FontStyle.Regular,new RectangleF(24,20+i*173,752,144),true);
        return image;
    }
    private static void DrawFitted(Graphics g,string text,FontFamily family,FontStyle style,RectangleF area,bool neon)
    {
        using var path=new GraphicsPath();
        path.AddString(text,family,(int)style,100,PointF.Empty,StringFormat.GenericTypographic);
        var bounds=path.GetBounds();
        using var transform=new Matrix();
        transform.Translate(-bounds.X,-bounds.Y);
        transform.Scale(area.Width/bounds.Width,area.Height/bounds.Height,MatrixOrder.Append);
        transform.Translate(area.X,area.Y,MatrixOrder.Append);
        path.Transform(transform);
        if(neon)
        {
            foreach(var pair in new[]{(24f,28),(15f,55),(8f,130)})
            {
                using var glow=new Pen(Color.FromArgb(pair.Item2,70,55,255),pair.Item1){LineJoin=LineJoin.Round};
                g.DrawPath(glow,path);
            }
            using var tube=new Pen(Color.FromArgb(222,222,255),3.8f){LineJoin=LineJoin.Round};
            g.DrawPath(tube,path);
        }
        else { using var ink=new SolidBrush(Color.FromArgb(235,234,228)); g.FillPath(ink,path); }
    }
    private static void Apply(Bitmap target,Bitmap patch,PointF[] quad)
    {
        // Inverse bilinear mapping keeps each letter inside the painted board,
        // including the rotation and perspective of the comic panel.
        var q=quad.Select(p=>new PointF(p.X*2,p.Y*2)).ToArray();
        int left=Math.Max(0,(int)Math.Floor(q.Min(p=>p.X))), top=Math.Max(0,(int)Math.Floor(q.Min(p=>p.Y)));
        int right=Math.Min(target.Width,(int)Math.Ceiling(q.Max(p=>p.X))), bottom=Math.Min(target.Height,(int)Math.Ceiling(q.Max(p=>p.Y)));
        for(int y=top;y<bottom;y++) for(int x=left;x<right;x++)
        {
            double u=.5,v=.5;
            for(int step=0;step<10;step++)
            {
                double px=(1-u)*(1-v)*q[0].X+u*(1-v)*q[1].X+u*v*q[2].X+(1-u)*v*q[3].X;
                double py=(1-u)*(1-v)*q[0].Y+u*(1-v)*q[1].Y+u*v*q[2].Y+(1-u)*v*q[3].Y;
                double ax=(1-v)*(q[1].X-q[0].X)+v*(q[2].X-q[3].X), ay=(1-v)*(q[1].Y-q[0].Y)+v*(q[2].Y-q[3].Y);
                double bx=(1-u)*(q[3].X-q[0].X)+u*(q[2].X-q[1].X), by=(1-u)*(q[3].Y-q[0].Y)+u*(q[2].Y-q[1].Y);
                double determinant=ax*by-ay*bx;
                if(Math.Abs(determinant)<.0001) break;
                double ex=px-(x+.5),ey=py-(y+.5);
                u-=(ex*by-ey*bx)/determinant; v-=(ax*ey-ay*ex)/determinant;
            }
            if(u<0||u>1||v<0||v>1) continue;
            var old=target.GetPixel(x,y);
            // Sample several points so thin vector strokes survive minification.
            double r=0,g=0,b=0;
            for(int sy=0;sy<3;sy++) for(int sx=0;sx<3;sx++)
            {
                var c=patch.GetPixel(Math.Clamp((int)(u*patch.Width)+(sx-1)*2,0,patch.Width-1),Math.Clamp((int)(v*patch.Height)+(sy-1)*2,0,patch.Height-1));
                r+=c.R;g+=c.G;b+=c.B;
            }
            int nr=(int)(r/9),ng=(int)(g/9),nb=(int)(b/9);
            // B > R is a shader mask. Keep its exact Boolean classification so
            // clearer lettering cannot introduce the former purple-pixel bug.
            if(old.B>old.R) { if(nb<=nr) { nr=Math.Min(nr,254); nb=nr+1; } }
            else if(nb>nr) nb=nr;
            target.SetPixel(x,y,Color.FromArgb(old.A,nr,ng,nb));
        }
    }
}
