namespace Ariel.Client.Terrain;

/// <summary>The water-worn rock a waterfall pours over: a rounded slab on the brink that juts out past the cliff's rock
/// columns, on a body that fills the face down to the foot. The model is <c>art/scatter/fall_ledges/Ledge_1.glb</c> in the
/// world mod, written by <c>tools/art/fall_ledge.py</c> to the dimensions here, in its own frame: origin on the brink at the
/// top of the slab, +X out over the drop, Z along the lip, Y up.</summary>
/// <param name="X">Origin, world units, canonical frame of the fall's link.</param>
/// <param name="Y">Height of the top of the slab.</param>
/// <param name="Z">Origin, world units.</param>
/// <param name="DirectionX">Unit direction the water falls in (the model's +X).</param>
/// <param name="DirectionZ">Unit direction the water falls in.</param>
/// <param name="Scale">Uniform scale of the model, which sets the lip's width.</param>
/// <param name="Stretch">Extra vertical scale, so the body reaches the foot.</param>
/// <param name="Foot">Height of the ground or water the sheet lands on.</param>
/// <param name="Reach">How far out past the nose the sheet lands, world units: <see cref="JetReach"/>, or less where the
/// river it falls into runs closer under the wall.</param>
/// <param name="Tile">The raised tile the ledge stands on, for sight.</param>
public readonly record struct FallLedge(double X, double Y, double Z, double DirectionX, double DirectionZ, double Scale,
    double Stretch, double Foot, double Reach, int Tile)
{
    /// <summary>How far out from the nose the sheet lands, world units, where nothing holds it closer: it leaves the nose
    /// moving outward and keeps going as it drops, so it stays clear of the rock columns below the ledge.</summary>
    public const double JetReach = 0.1;
    /// <summary>The least a sheet is thrown out from the nose, world units, however close under the wall the water it
    /// lands in runs.</summary>
    public const double MinReach = 0.03;
    /// <summary>Width of the slab along the lip at scale 1.</summary>
    public const double Width = 0.36;
    /// <summary>How far the slab reaches back over the plateau from the brink at scale 1.</summary>
    public const double Back = 0.1;
    /// <summary>How far the slab's nose juts out past the brink at scale 1.</summary>
    public const double Nose = 0.14;
    /// <summary>How far the top of the slab rounds down to its nose at scale 1, before stretch.</summary>
    public const double NoseDrop = 0.03;
    /// <summary>Depth of the slab under its top at scale 1, before stretch: where the rock columns beneath it stop.</summary>
    public const double SlabDepth = 0.06;
    /// <summary>Height of the model from the top of the slab to the foot of its body at scale 1, before stretch.</summary>
    public const double Height = 0.24;
    /// <summary>How far below the top of the slab the water behind it runs (see <see cref="Brim"/>).</summary>
    public const double BrimDrop = 0.015;
    /// <summary>How far back from the brink the water keeps up to <see cref="Brim"/> (world units): over the reach where the
    /// drawn ground rounds down over the cliff's edge before the ledge rock.</summary>
    public const double BrimReach = 0.35;

    /// <summary>Height of the top of the slab at a distance <paramref name="out"/> past the brink (world units): level over
    /// the plateau, rounding down to the nose.</summary>
    public double TopAt(double @out)
    {
        double x = Math.Clamp(@out / Scale, -Back, Nose) / Nose;
        return Y - (x > 0 ? NoseDrop * x * x : 0) * Scale * Stretch;
    }

    /// <summary>The level the water runs onto the ledge at: the river behind it and the fall's tongue keep up to it where the
    /// drawn ground rounds down over the brink, since a river that pours over a ledge does not sag before it, and its
    /// water lying on that rounded ground lets the ground show through between its vertices.</summary>
    public double Brim => Y - BrimDrop;

    /// <summary>World distance past the brink of the nose, where the water leaves the rock.</summary>
    public double NoseOut => Nose * Scale;

    /// <summary>Where the sheet lands, in the ledge's frame: <see cref="Reach"/> out past the nose.</summary>
    public (double X, double Z) Landing => (X + DirectionX * (NoseOut + Reach), Z + DirectionZ * (NoseOut + Reach));

    /// <summary>World distance behind the brink of the slab's back edge.</summary>
    public double BackIn => Back * Scale;

    /// <summary>The model's turn about Y that points its +X along the fall, as Godot measures it.</summary>
    public double Yaw => Math.Atan2(-DirectionZ, DirectionX);
}
