namespace StoneshardCompanion;

public readonly record struct HudRect(double X,double Y,double Width,double Height);
public readonly record struct HudGrid(int Columns,int Rows,double CellWidth,double CellHeight);
public enum HudCorner { Move,TopLeft,TopRight,BottomLeft,BottomRight }

/// <summary>All coordinates are game-client DIPs, independent of screen origin and DPI.</summary>
public static class HudGeometry
{
    public const double NavigationSize=108,NavigationGap=8;
    public const double HeaderHeight=30;
    public const double MinWidth=356,MinHeight=208,DefaultWidth=650,DefaultHeight=224;
    public static HudRect Clamp(HudRect value,double viewportWidth,double viewportHeight)
    {
        double vw=Math.Max(1,Finite(viewportWidth,1920)),vh=Math.Max(1,Finite(viewportHeight,1080));
        double w=Math.Clamp(Finite(value.Width,DefaultWidth),Math.Min(MinWidth,vw),vw);
        double h=Math.Clamp(Finite(value.Height,DefaultHeight),Math.Min(MinHeight,vh),vh);
        return new(Math.Clamp(Finite(value.X,16),0,Math.Max(0,vw-w)),Math.Clamp(Finite(value.Y,vh-h-160),0,Math.Max(0,vh-h)),w,h);
    }
    public static HudRect Drag(HudRect start,HudCorner corner,double dx,double dy,double vw,double vh)
    {
        if(corner==HudCorner.Move)return Clamp(start with {X=start.X+dx,Y=start.Y+dy},vw,vh);
        bool left=corner is HudCorner.TopLeft or HudCorner.BottomLeft,top=corner is HudCorner.TopLeft or HudCorner.TopRight;
        double w=Math.Clamp(start.Width+(left?-dx:dx),Math.Min(MinWidth,vw),Math.Max(1,left?start.X+start.Width:vw-start.X));
        double h=Math.Clamp(start.Height+(top?-dy:dy),Math.Min(MinHeight,vh),Math.Max(1,top?start.Y+start.Height:vh-start.Y));
        return Clamp(new(left?start.X+start.Width-w:start.X,top?start.Y+start.Height-h:start.Y,w,h),vw,vh);
    }
    public static HudGrid Grid(double width,double height,int count)
    {
        if(count<1)return new(1,1,Math.Max(1,width),Math.Max(1,height));
        width=Math.Max(1,width);height=Math.Max(1,height);
        int best=1;double score=double.MinValue;
        for(int columns=1;columns<=count;columns++){
            int rows=(count+columns-1)/columns;
            double scale=Math.Min(width/columns/64,height/rows/48);
            double candidate=scale-(columns*rows-count)*.003;
            if(candidate>score){score=candidate;best=columns;}
        }
        int totalRows=(count+best-1)/best;return new(best,totalRows,width/best,height/totalRows);
    }
    private static double Finite(double value,double fallback)=>double.IsFinite(value)?value:fallback;
}
