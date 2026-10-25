using System.Text;

namespace KnightOnline.Database.World;

public sealed record MapWarp(ushort Id, string Name, string Announcement, uint Fee, ushort Zone,
    float X, float Y, float Z, float Radius, short Nation);

/// <summary>Read-only SMD terrain and warp loader, following SMDFile.cpp/N3ShapeMgr.cpp.</summary>
public sealed class SmdMap
{
    private readonly float[] _heights;
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
        Skip(r, Count(r, 10_000_000) * 36L);
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
