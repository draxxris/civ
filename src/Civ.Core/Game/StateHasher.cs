using System.Text;

namespace Civ.Game;

/// <summary>FNV-1a over a canonical walk of the state. Used to detect desyncs between peers.</summary>
public static class StateHasher
{
    public static ulong Hash(GameState s, DeterministicRng rng)
    {
        var h = new Fnv();
        h.Int(s.Turn);
        h.Int(s.NextCityId);

        foreach (var kv in s.Terrain.OrderBy(k => k.Key.Q).ThenBy(k => k.Key.R))
        {
            h.Int(kv.Key.Q); h.Int(kv.Key.R); h.Str(kv.Value);
        }
        foreach (var p in s.Players.OrderBy(p => p.Id))
        {
            h.Int(p.Id); h.Str(p.Civilization); h.Str(p.Ai); h.Str(p.Researching); h.Long(p.ResearchProgress); h.Long(p.Culture); h.Int(p.EndedTurn ? 1 : 0);
            foreach (var cv in p.Civics) { h.Str(cv.Key); h.Str(cv.Value); }
            foreach (var r in p.Resources) { h.Str(r.Key); h.Long(r.Value); }
            foreach (var t in p.Techs) h.Str(t); // SortedSet: stable
        }
        foreach (var c in s.Cities.OrderBy(c => c.Id))
        {
            h.Int(c.Id); h.Int(c.Owner); h.Int(c.Position.Q); h.Int(c.Position.R);
            h.Str(c.Building); h.Long(c.BuildProgress); h.Str(c.Training); h.Long(c.TrainProgress);
            h.Int(c.Population); h.Long(c.FoodStored);
            foreach (var w in c.Worked) { h.Int(w.Q); h.Int(w.R); }
            foreach (var sp in c.Specialists) { h.Str(sp.Key); h.Int(sp.Value); } h.Long(c.Culture);
            foreach (var b in c.Buildings) h.Str(b);
            foreach (var r in c.Resources) { h.Str(r.Key); h.Long(r.Value); }
            foreach (var i in c.Institutions) h.Str(i);
        }
        foreach (var (id, inst) in s.Institutions) { h.Str(id); h.Int(inst.Founder); h.Int(inst.HomeCity); h.Int(inst.FoundedTurn); }
        h.Int(s.NextUnitId);
        foreach (var u in s.Units.OrderBy(u => u.Id))
        {
            h.Int(u.Id); h.Int(u.Owner); h.Str(u.Type); h.Int(u.Position.Q); h.Int(u.Position.R);
            h.Int(u.Experience); h.Int(u.Health); h.Int(u.MovesLeft);
            foreach (var p in u.Promotions) h.Str(p);
        }
        var (a, b2, c2, d) = rng.State;
        h.Long((long)a); h.Long((long)b2); h.Long((long)c2); h.Long((long)d);
        return h.Value;
    }

    private struct Fnv
    {
        public ulong Value = 14695981039346656037UL;
        public Fnv() { }
        private void Byte(byte b) { Value ^= b; Value *= 1099511628211UL; }
        public void Long(long v) { for (int i = 0; i < 8; i++) Byte((byte)(v >> (i * 8))); }
        public void Int(int v) => Long(v);
        public void Str(string? s)
        {
            if (s == null) { Byte(0); return; }
            Byte(1);
            var bytes = Encoding.UTF8.GetBytes(s);
            Int(bytes.Length);
            foreach (var x in bytes) Byte(x);
        }
    }
}
