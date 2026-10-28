using System.Text;
using System.Numerics;

namespace KnightOnline.Database.World;

public sealed record MapWarp(ushort Id, string Name, string Announcement, uint Fee, ushort Zone,
    float X, float Y, float Z, float Radius, short Nation);

/// <summary>Read-only SMD terrain and warp loader, following SMDFile.cpp/N3ShapeMgr.cpp.</summary>
public sealed class SmdMap
{
    private readonly float[] _heights;
    private readonly MapCollision _collision;
    public int Size { get; }
    public float Spacing { get; }
    public float Extent => (Size - 1) * Spacing;
    public IReadOnlyList<MapWarp> Warps { get; }

    public SmdMap(string path)
    {
        using var stream = File.OpenRead(path);
        using var r = new BinaryReader(stream, Encoding.Latin1);
        Size = Count(r, 4097);
        Spacing = r.ReadSingle();
        if (Size < 2 || !float.IsFinite(Spacing) || Spacing <= 0) throw new InvalidDataException("Invalid terrain dimensions.");
        _heights = new float[checked(Size * Size)];
        for (var i = 0; i < _heights.Length; i++)
        {
            var height = r.ReadSingle();
            if (!float.IsFinite(height)) throw new InvalidDataException($"Non-finite SMD terrain height at sample {i}.");
            _heights[i] = height;
        }
        var width = r.ReadSingle();
        var length = r.ReadSingle();
        if (width != Extent || length != Extent || width > 4096) throw new InvalidDataException("SMD collision dimensions disagree with terrain.");
        _collision = new MapCollision(r, Count(r, 10_000_000), Extent);
        for (var z = 0; z < length; z += 16)
        for (var x = 0; x < width; x += 16)
        {
            if (r.ReadUInt32() == 0) continue;
            Skip(r, Count(r, 1_000_000) * 2L);
            for (var i = 0; i < 16; i++) Skip(r, Count(r, 1_000_000) * 12L);
        }
        Skip(r, Count(r, 100_000) * 24L); // DB K_OBJECTPOS overrides these legacy object records
        Skip(r, Size * (long)Size * 2);
        Skip(r, Count(r, 100_000) * 20L);
        var count = Count(r, 10_000);
        var warps = new List<MapWarp>();
        for (var i = 0; i < count; i++)
        {
            // C++ explicitly permits an incomplete final warp record at EOF.
            if (stream.Length - stream.Position < 320) break;
            var id = r.ReadUInt16();
            var name = Fixed(r, 32);
            var text = Fixed(r, 256);
            r.ReadUInt16();
            var fee = r.ReadUInt32();
            var zone = r.ReadUInt16();
            r.ReadUInt16();
            var x = r.ReadSingle(); var y = r.ReadSingle(); var z = r.ReadSingle(); var radius = r.ReadSingle();
            var nation = r.ReadInt16(); r.ReadUInt16();
            if (id != 0) warps.Add(new MapWarp(id, name, text, fee, zone, x, y, z, radius, nation));
        }
        Warps = warps;
    }

    public bool Contains(float x, float z) => float.IsFinite(x) && float.IsFinite(z) && x >= 0 && z >= 0 && x < Extent && z < Extent;
    public float Height(float x, float z)
    {
        if (!Contains(x, z)) throw new ArgumentOutOfRangeException(nameof(x));
        // Terrain is indexed x * size + z, like the SMD tile arrays.
        var ix = Math.Clamp((int)(x / Spacing), 0, Size - 2);
        var iz = Math.Clamp((int)(z / Spacing), 0, Size - 2);
        var dx = x / Spacing - ix; var dz = z / Spacing - iz;
        var a = _heights[ix * Size + iz]; var b = _heights[(ix + 1) * Size + iz];
        var c = _heights[ix * Size + iz + 1]; var d = _heights[(ix + 1) * Size + iz + 1];
        // Interpolate the checkerboard terrain triangles.
        float value;
        if (((ix + iz) & 1) == 0)
            value = dx + dz <= 1 ? a + dx * (b - a) + dz * (c - a) : d + (1 - dx) * (c - d) + (1 - dz) * (b - d);
        else
            value = dz <= dx ? a + dx * (b - a) + dz * (d - b) : a + dz * (c - a) + dx * (d - c);
        return float.IsFinite(value) ? value : throw new InvalidDataException("Invalid terrain height.");
    }

    /// <summary>Conservative terrain/shape visibility, independent of client-provided Y.</summary>
    public bool HasLineOfSight(float x,float z,float targetX,float targetZ)
    {
        if(!Contains(x,z)||!Contains(targetX,targetZ)) return false;
        var a=new Vector3(x,Height(x,z)+.8f,z);var b=new Vector3(targetX,Height(targetX,targetZ)+.8f,targetZ);
        var distance=Vector3.Distance(a,b);
        // AI queries are local; reject unbounded/malformed segment work.
        if(!float.IsFinite(distance)||distance>160) return false;
        var steps=Math.Max(1,(int)Math.Ceiling(distance));
        for(var i=1;i<steps;i++)
        {
            var p=Vector3.Lerp(a,b,(float)i/steps);
            if(Height(p.X,p.Z)>p.Y-.2f) return false;
        }
        return _collision.Clear(a,b);
    }
    /// <summary>Bounded direct steps only. Stop at walls, steep terrain and map edges.</summary>
    public bool CanMonsterStep(float x,float z,float targetX,float targetZ)
    {
        if(!Contains(x,z)||!Contains(targetX,targetZ)) return false;
        var dx=targetX-x;var dz=targetZ-z;var distance=MathF.Sqrt(dx*dx+dz*dz);
        if(!float.IsFinite(distance)||distance>6) return false;
        if(distance<.0001f) return true;
        var steps=Math.Max(1,(int)Math.Ceiling(distance/.5f));
        var previous=Height(x,z);
        for(var i=1;i<=steps;i++)
        {
            var px=x+dx*i/steps;var pz=z+dz*i/steps;var height=Height(px,pz);
            if(Math.Abs(height-previous)>distance/steps+.15f) return false;
            previous=height;
        }
        // Centre plus body-width rays, below and above waist height. This is
        // conservative collision avoidance, not swept capsule navigation.
        var ox=-dz/distance*.3f;var oz=dx/distance*.3f;
        foreach(var side in new[]{-1,0,1})
        {
            var ax=x+ox*side;var az=z+oz*side;var bx=targetX+ox*side;var bz=targetZ+oz*side;
            if(!Contains(ax,az)||!Contains(bx,bz)) return false;
            foreach(var height in new[]{.35f,1.2f})
                if(!_collision.Clear(new(ax,Height(ax,az)+height,az),new(bx,Height(bx,bz)+height,bz))) return false;
        }
        return true;
    }

    private static int Count(BinaryReader r, int max)
    {
        var value = r.ReadInt32();
        return value >= 0 && value <= max ? value : throw new InvalidDataException("Invalid SMD record count.");
    }
    private static void Skip(BinaryReader r, long bytes)
    {
        if (bytes < 0 || bytes > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException();
        r.BaseStream.Seek(bytes, SeekOrigin.Current);
    }
    private static string Fixed(BinaryReader r, int size) => Encoding.Latin1.GetString(r.ReadBytes(size)).Split('\0')[0];
}
