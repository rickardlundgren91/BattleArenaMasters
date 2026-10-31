using System;
using System.Collections.Generic;
using System.Reflection;

// A second Match that follows the first (online guest prediction, Rickard 2026-10-31: "som gäst var det långsamt ... fixa detta
// oavsett hur stort projekt det är"). The guest keeps the confirmed match (stepped only with the host's frames, the one the
// fingerprints are taken from) and shows a mirror of it run a little ahead with its own controls at once. Every time new frames
// come, the mirror is made equal to the confirmed match again (Sync) and run ahead anew.
// Sync copies every field of every object of the match into the mirror's own objects, which stay the same objects from sync to
// sync (the views keep pointing at them). The copying itself is generated code (SimCopy.g.cs, one method per sim class; reflection
// would be too slow on a phone 60 times a second): Fields() below lists what it must cover, and a hash of that list is compared
// with the one the generated file was made from. When they differ (a field was added since), Available is false and the game plays
// as before without prediction, until the file is made again (BAM > Online > Regenerate SimCopy, done by itself in the editor;
// headless: bash bam-work/online/tools/simcopy/run.sh).
namespace BAM.Sim
{
    public sealed partial class SimMirror
    {
        /// <summary>The generated file matches the sim classes as they are now: prediction can be used.</summary>
        public static bool Available { get { if (ok < 0) ok = Generated && GeneratedHash == Hash() ? 1 : 0; return ok == 1; } }
        static int ok = -1;
        static bool Generated;
        static string GeneratedHash = "";
        static partial void Init(); // SimCopy.g.cs sets Generated, GeneratedHash and Copy/Twin below
        static SimMirror() { Init(); }

        // the generated dispatch: a twin of an object (a shallow copy whose own containers are new), and copy src into dst
        static Func<object, SimMirror, object> TwinOf;
        static Action<object, object, SimMirror> CopyInto;

        public readonly Match Src, Dst;
        readonly Dictionary<object, Twin> twins = new Dictionary<object, Twin>(512, RefEq.I);
        readonly Queue<object> todo = new Queue<object>(256);
        readonly List<object> gone = new List<object>(64);
        int pass;
        sealed class Twin { public object Obj; public int Pass; }

        /// <summary>dst: a match made with the same config (Match.Create), which then only ever follows src.</summary>
        public SimMirror(Match src, Match dst)
        {
            Src = src; Dst = dst;
            Pair(src, dst); Pair(src.Map, dst.Map); Pair(src.Rng, dst.Rng);
        }
        void Pair(object s, object d) => twins[s] = new Twin { Obj = d, Pass = -1 };

        /// <summary>Makes Dst equal to Src (every field, every unit, shot and bot). False when the generated copy is not usable.</summary>
        public bool Sync()
        {
            if (!Available) return false;
            pass++;
            Map(Src);
            while (todo.Count > 0) { var s = todo.Dequeue(); CopyInto(twins[s].Obj, s, this); }
            // objects src no longer has (a dead minion, a shot gone from the pool) let go of their twins
            gone.Clear();
            foreach (var kv in twins) if (kv.Value.Pass != pass && kv.Value.Pass >= 0) gone.Add(kv.Key);
            foreach (var k in gone) twins.Remove(k);
            return true;
        }

        /// <summary>The twin of a sim object (made the first time), queued for copying once per Sync.</summary>
        public T Map<T>(T s) where T : class
        {
            if (s == null) return null;
            if (!twins.TryGetValue(s, out var t)) { t = new Twin { Obj = TwinOf(s, this), Pass = 0 }; twins[s] = t; }
            if (t.Pass != pass) { t.Pass = pass; todo.Enqueue(s); }
            return (T)t.Obj;
        }

        // ---------- helpers the generated code calls ----------
        public static void Arr<T>(T[] d, T[] s) { if (s != null && d != null) Array.Copy(s, d, Math.Min(s.Length, d.Length)); }
        public T[] ArrNew<T>(T[] d, T[] s)
        {
            if (s == null) return null;
            if (d == null || d.Length != s.Length || ReferenceEquals(d, s)) d = new T[s.Length];
            Array.Copy(s, d, s.Length); return d;
        }
        public void ArrMap<T>(T[] d, T[] s) where T : class { if (s == null || d == null) return; for (int i = 0; i < s.Length && i < d.Length; i++) d[i] = Map(s[i]); }
        public T[] ArrMapNew<T>(T[] d, T[] s) where T : class
        {
            if (s == null) return null;
            if (d == null || d.Length != s.Length || ReferenceEquals(d, s)) d = new T[s.Length];
            ArrMap(d, s); return d;
        }
        public static void Jag<T>(T[][] d, T[][] s) { if (s == null || d == null) return; for (int i = 0; i < s.Length && i < d.Length; i++) Arr(d[i], s[i]); }
        public static void List<T>(List<T> d, List<T> s) { if (s == null || d == null) return; d.Clear(); d.AddRange(s); }
        public void ListMap<T>(List<T> d, List<T> s) where T : class { if (s == null || d == null) return; d.Clear(); for (int i = 0; i < s.Count; i++) d.Add(Map(s[i])); }
        public static void Dict<K, V>(Dictionary<K, V> d, Dictionary<K, V> s) { if (s == null || d == null) return; d.Clear(); foreach (var kv in s) d[kv.Key] = kv.Value; }
        public static void Set<K>(HashSet<K> d, HashSet<K> s) { if (s == null || d == null) return; d.Clear(); foreach (var k in s) d.Add(k); }
        public static void Que<K>(Queue<K> d, Queue<K> s) { if (s == null || d == null) return; d.Clear(); foreach (var k in s) d.Enqueue(k); }

        // a readonly field of a new twin that must point at its own object, not src's (once per twin, so reflection is fine)
        static readonly Dictionary<(Type, string), FieldInfo> roFields = new Dictionary<(Type, string), FieldInfo>();
        public static void SetRo(object o, string name, object v)
        {
            var k = (o.GetType(), name);
            if (!roFields.TryGetValue(k, out var f)) roFields[k] = f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            f.SetValue(o, v);
        }
        public static List<T> NewList<T>(List<T> s) => s == null ? null : new List<T>(s.Capacity);
        public static Dictionary<K, V> NewDict<K, V>(Dictionary<K, V> s) => s == null ? null : new Dictionary<K, V>();
        public static HashSet<K> NewSet<K>(HashSet<K> s) => s == null ? null : new HashSet<K>();
        public static Queue<K> NewQue<K>(Queue<K> s) => s == null ? null : new Queue<K>();
        public static T[] NewArr<T>(T[] s) => s == null ? null : new T[s.Length];
        public static T[][] NewJag<T>(T[][] s) { if (s == null) return null; var d = new T[s.Length][]; for (int i = 0; i < s.Length; i++) d[i] = s[i] == null ? null : new T[s[i].Length]; return d; }

        sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq I = new RefEq();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        // ---------- what the copy must cover (shared by the generator and the check) ----------

        /// <summary>Sim classes that never change during a match: shared, not copied.</summary>
        public static bool Shared(Type t) => t == typeof(string) || t == typeof(HeroDef) || t == typeof(UnitDef) || t == typeof(BotTier)
            || t == typeof(HeroInfo) || t == typeof(MatchConfig) || t == typeof(SeatConfig) || t == typeof(SeatConfig[]);
        /// <summary>A sim class whose objects get a twin.</summary>
        public static bool Mapped(Type t) => t.IsClass && !t.IsArray && t.Assembly == typeof(Match).Assembly && !Shared(t);

        public const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Every sim class and struct reachable from Match (mapped classes, and the structs inside them), in a fixed order.</summary>
        public static List<Type> AllTypes()
        {
            var seen = new HashSet<Type>(); var list = new List<Type>(); var q = new Queue<Type>(); q.Enqueue(typeof(Match));
            while (q.Count > 0)
            {
                var t = q.Dequeue(); if (!seen.Add(t)) continue;
                list.Add(t);
                foreach (var f in t.GetFields(All)) Walk(f.FieldType, q);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return list;
        }
        /// <summary>The mapped classes (each gets generated copy code).</summary>
        public static List<Type> Types() { var l = AllTypes(); l.RemoveAll(t => !Mapped(t)); return l; }
        static void Walk(Type t, Queue<Type> q)
        {
            if (t.IsArray) { Walk(t.GetElementType(), q); return; }
            if (t.IsGenericType) foreach (var a in t.GetGenericArguments()) Walk(a, q);
            if (t.Assembly == typeof(Match).Assembly && !t.IsEnum && !t.IsArray && !t.IsGenericType && !Shared(t)) q.Enqueue(t);
        }

        /// <summary>A fingerprint of every field the copy covers (names, types, readonly), to see whether the generated file is current.</summary>
        // a type's name without assembly names (they differ between Unity and the headless tools)
        static string Name(Type t)
        {
            if (t.IsArray) return Name(t.GetElementType()) + "[]";
            if (!t.IsGenericType) return t.FullName;
            var a = t.GetGenericArguments(); var n = new string[a.Length];
            for (int i = 0; i < a.Length; i++) n[i] = Name(a[i]);
            return t.GetGenericTypeDefinition().FullName + "<" + string.Join(",", n) + ">";
        }

        public static string Hash()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in AllTypes())
            {
                sb.Append(t.FullName).Append('{');
                var fs = t.GetFields(All); Array.Sort(fs, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var f in fs) sb.Append(f.IsInitOnly ? "ro " : "").Append(Name(f.FieldType)).Append(' ').Append(f.Name).Append(';');
                sb.Append('}');
            }
            ulong h = 14695981039346656037UL;
            foreach (char c in sb.ToString()) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16");
        }
    }
}
