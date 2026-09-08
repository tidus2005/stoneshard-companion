using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace StoneshardCompanion;

public static class HudIcon
{
    private static readonly Dictionary<string,Geometry> Cache=[];
    public static FrameworkElement Create(string name,double size=23,Brush? color=null)
    {
        if(!Cache.TryGetValue(name,out var geometry)) {
            string? builtIn=name switch{
                "speed-normal"=>"M 8,3 L 21,12 8,21 Z",
                "speed-fast"=>"M 2,3 L 12,12 2,21 Z M 13,3 L 23,12 13,21 Z",
                "speed-rapid"=>"M 1,3 L 8,12 1,21 Z M 9,3 L 16,12 9,21 Z M 17,3 L 24,12 17,21 Z",
                "save-backup"=>"M 2,3 L 8,3 8,5 4,5 4,20 20,20 20,5 16,5 16,3 22,3 22,22 2,22 Z M 11,1 L 13,1 13,10 16,7 18,9 12,15 6,9 8,7 11,10 Z",
                "save-history"=>"M 2,4 L 9,4 12,7 22,7 22,21 2,21 Z M 4,9 L 4,19 20,19 20,9 Z M 7,12 L 17,12 17,14 7,14 Z M 7,16 L 14,16 14,18 7,18 Z",_=>null};
            if(builtIn is not null){geometry=Geometry.Parse(builtIn);geometry.Freeze();Cache[name]=geometry;}
            else {
            var uri=new Uri($"pack://application:,,,/Assets/Icons/{name}.svg");
            using var stream=Application.GetResourceStream(uri).Stream;
            var document=XDocument.Load(stream);
            var shapes=document.Descendants().Where(e=>e.Name.LocalName=="path"&&(string?)e.Attribute("fill")=="#fff").Select(e=>Geometry.Parse((string)e.Attribute("d")!));
            var group=new GeometryGroup{FillRule=FillRule.Nonzero};foreach(var shape in shapes)group.Children.Add(shape);group.Freeze();geometry=group;Cache[name]=geometry;
            }
        }
        return new System.Windows.Shapes.Path{Data=geometry,Fill=color??new SolidColorBrush(Color.FromRgb(204,205,216)),Stretch=Stretch.Uniform,Width=size,Height=size,IsHitTestVisible=false};
    }
}
