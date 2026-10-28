using System.Numerics;

namespace KnightOnline.Database.World;

/// <summary>Read-only, double-sided segment tests over SMD shape triangles; not a navmesh.</summary>
internal sealed class MapCollision
{
    private readonly record struct Triangle(Vector3 A,Vector3 B,Vector3 C);
    private readonly Triangle[] _triangles;
    private readonly Dictionary<(int,int),List<int>> _cells=[];
    private readonly List<int> _wide=[];
    public MapCollision(BinaryReader reader,int count,float extent)
    {
        if(count*36L>reader.BaseStream.Length-reader.BaseStream.Position) throw new EndOfStreamException();
        _triangles=new Triangle[count];
        Vector3 Vertex()
        {
            var v=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)||Math.Abs(v.X)>1_000_000||Math.Abs(v.Y)>1_000_000||Math.Abs(v.Z)>1_000_000)
                throw new InvalidDataException("Invalid SMD collision vertex.");
            return v;
        }
        long references=0;
        for(var i=0;i<count;i++)
        {
            var t=new Triangle(Vertex(),Vertex(),Vertex());_triangles[i]=t;
            var min=Vector3.Min(t.A,Vector3.Min(t.B,t.C));var max=Vector3.Max(t.A,Vector3.Max(t.B,t.C));
            if(max.X<0||max.Z<0||min.X>=extent||min.Z>=extent) continue;
            var x0=(int)(Math.Clamp(min.X,0,extent)/16);var x1=(int)(Math.Clamp(max.X,0,extent)/16);
            var z0=(int)(Math.Clamp(min.Z,0,extent)/16);var z1=(int)(Math.Clamp(max.Z,0,extent)/16);
            if((x1-x0+1)*(z1-z0+1)>64) { _wide.Add(i);continue; }
            for(var x=x0;x<=x1;x++) for(var z=z0;z<=z1;z++)
            {
                if(++references>4_000_000) throw new InvalidDataException("SMD collision index is too large.");
                if(!_cells.TryGetValue((x,z),out var cell)) _cells[(x,z)]=cell=[];
                cell.Add(i);
            }
        }
    }
    public bool Clear(Vector3 from,Vector3 to)
    {
        var min=Vector3.Min(from,to);var max=Vector3.Max(from,to);
        var candidates=new HashSet<int>(_wide);
        for(var x=(int)(min.X/16);x<=(int)(max.X/16);x++)
        for(var z=(int)(min.Z/16);z<=(int)(max.Z/16);z++)
            if(_cells.TryGetValue((x,z),out var cell)) candidates.UnionWith(cell);
        var direction=to-from;
        foreach(var i in candidates)
        {
            var t=_triangles[i];var e1=t.B-t.A;var e2=t.C-t.A;
            var p=Vector3.Cross(direction,e2);var determinant=Vector3.Dot(e1,p);
            if(Math.Abs(determinant)<.000001f) continue;
            var inv=1/determinant;var s=from-t.A;var u=Vector3.Dot(s,p)*inv;
            if(u<0||u>1) continue;
            var q=Vector3.Cross(s,e1);var v=Vector3.Dot(direction,q)*inv;
            if(v<0||u+v>1) continue;
            var fraction=Vector3.Dot(e2,q)*inv;
            if(fraction>=0&&fraction<=1) return false;
        }
        return true;
    }
}
