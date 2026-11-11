using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    public sealed class MatchConfig
    {
        public HeroId PlayerHero = HeroId.Tyra;
        public Difficulty Difficulty = Difficulty.Normal;
        /// <summary>The bots on the player's own team; null = Difficulty.</summary>
        public Difficulty? AllyDifficulty;
        public int Seed = 1;
        /// <summary>The guided first match (Tyra, one enemy tower, no waves).</summary>
        public bool Tutorial;
        /// <summary>With Tutorial: the guided match's first half instead, Fenn on the round training island (old
        /// SimTutorialIsland). When its Tut.Done the game starts the arena half (Tutorial without Island).</summary>
        public bool Island;
        /// <summary>A bot plays the player's hero too (headless test matches).</summary>
        public bool PlayerIsBot;
        /// <summary>Headless tests with PlayerIsBot: that bot counts as the human, so the bots play as they do against a person
        /// (team play with the leader, slower reactions, held specials; Bots/BotTeam.cs).</summary>
        public bool BotIsHuman;
        /// <summary>Optional fixed line-ups: 2 allies and 3 enemies. Null = drawn from the other heroes with the seed.</summary>
        public HeroId[] Allies, Enemies;
        /// <summary>Test matches: every hero at slot i of its team's spawn (left to right) instead of the player in the middle.</summary>
        public bool SlotOrder;
        /// <summary>The player's battle spell (Spells.cs); bots draw theirs with the seed.</summary>
        public SpellId PlayerSpell = SpellId.Blink;
        /// <summary>HERO TEST (HeroTest.cs): the player's hero against dummies, at HeroTestLevel, never ending.</summary>
        public bool HeroTest;
        public int HeroTestLevel = 1;
        /// <summary>An online match (BAM.Online's lockstep): one entry per human, in seat order, the same on every phone.
        /// Null = the single player above. The other places of both teams are bots of Difficulty, drawn with the seed.</summary>
        public SeatConfig[] Seats;
        /// <summary>With Seats: the seat this phone plays. Only Player (camera, HUD, marker) follows it; the rules never read it,
        /// so every phone runs the very same match.</summary>
        public int LocalSeat;
        /// <summary>The arena (Arena.cs). Online and the guided match always play Runes.</summary>
        public MapId Map = MapId.Runes;
    }

    /// <summary>One human of an online match.</summary>
    public sealed class SeatConfig
    {
        public HeroId Hero;
        public int Team;
        public SpellId Spell = SpellId.Blink;
        /// <summary>The player's name, shown over the hero (online). Only for the HUD: the rules never read it.</summary>
        public string Name;
    }

    /// <summary>The player's controls for one tick, in map space (x right, y down the map).</summary>
    public struct PlayerInput
    {
        public float MoveX, MoveY;
        /// <summary>The special button was tapped, or a drag was released, this tick.</summary>
        public bool Special;
        /// <summary>The drag's direction (NaN = a tap) and length 0..1.</summary>
        public float AimAngle, AimK;
        /// <summary>The battle spell button was tapped, or its drag released, this tick; SpellAngle NaN = the way the hero faces.</summary>
        public bool Spell;
        public float SpellAngle, SpellK;
        /// <summary>A team signal called this tick (ATTACK or DEFEND on the emote wheel), else None.</summary>
        public TeamSignal Signal;
        public static PlayerInput None => new PlayerInput { AimAngle = float.NaN, SpellAngle = float.NaN, SpellK = 1 };
    }

    public enum MatchState : byte { Countdown, Play, Over }

    /// <summary>
    /// One match of the rules, without graphics: create it, call Step 60 times a second with the player's input, read Units,
    /// Projs and Events after each Step. Deterministic: the same config (seed) and inputs give the same match.
    /// </summary>
    public sealed partial class Match
    {
        public readonly MatchConfig Config;
        public readonly Arena Map;
        public readonly Rng Rng;
        public float Time;
        public MatchState State;
        /// <summary>The winning team once State is Over (-1 before).</summary>
        public int Winner = -1;
        public int Level = 1;
        public float WaveT;
        public int WaveN;
        public readonly int[] Kills = new int[2], Stars = new int[2];
        public bool NoStars;
        public Unit Player;
        /// <summary>The heroes people play, by seat (the player alone offline; online every phone's hero, Config.Seats).</summary>
        public readonly List<Unit> Humans = new List<Unit>(6);
        public readonly Unit[] Towers = new Unit[2];
        public readonly List<Unit> Units = new List<Unit>(96);
        /// <summary>The real heroes (no clones), by HeroIndex.</summary>
        public readonly List<Unit> Heroes = new List<Unit>(8);
        public readonly List<Proj> Projs = new List<Proj>(160);
        /// <summary>What happened in the last Step (cleared at the start of each Step).</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>(256);
        public Tutorial Tut;
        public long Ticks;
        /// <summary>The player pressed the special and it ran out unanswered at this time (the HUD shakes the button).</summary>
        public float SpecialDeniedAt = -99;

        int nextSerial = 1;
        // each seat's controls (the special is buffered a moment, so a press during a swing is not lost)
        public const int MaxSeats = 6;
        readonly float[] seatMoveX = new float[MaxSeats], seatMoveY = new float[MaxSeats], seatSpecBuf = new float[MaxSeats], seatBufK = new float[MaxSeats];
        readonly float[] seatBufAng = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
        readonly PlayerInput[] oneInput = new PlayerInput[1];
        // the player's (seat 0) buffered special, read by the guided match
        float specialBuf => seatSpecBuf[0];
        // a press while the hero cannot act (stunned, knocked back, busy casting) is kept, not run down, for up to this long after
        // the press, so it fires when the hero can act again instead of vanishing with no feedback (precision CHAINMAP-04)
        const float SpecialHold = 1f;
        readonly float[] seatSpecAt = new float[MaxSeats];
        readonly bool[] seatActed = new bool[MaxSeats];
        // the match clock summed in double, so it does not drift from the tick count (float: 176 ms off after 15 min, CHAINMAP-09);
        // a Time set from outside (tests, the guided match) is taken over
        double timeD; float timeSet = float.NaN;
        float bufAng => seatBufAng[0];
        readonly List<Unit> unitBuf = new List<Unit>(96), atkBuf = new List<Unit>(48), aiBuf = new List<Unit>(48), tmpBuf = new List<Unit>(48);
        readonly List<Proj> projBuf = new List<Proj>(160), projPool = new List<Proj>(160);
        readonly List<Spawn> spawns = new List<Spawn>(16);
        struct Spawn { public float T; public int Team; public MinionKind Kind; }

        public static Match Create(MatchConfig cfg) => new Match(cfg);

        Match(MatchConfig cfg)
        {
            Config = cfg;
            Map = cfg.Tutorial && cfg.Island ? IslandMap() : new Arena(cfg.Tutorial || cfg.Seats != null ? MapId.Runes : cfg.Map);
            Rng = new Rng(cfg.Seed);
            Time = -Rules.Countdown;
            State = MatchState.Countdown;
            WaveT = Rules.WaveFirst;
            for (int tt = 0; tt < 2; tt++) { int t = TopFirst ? 1 - tt : tt; Towers[t] = NewTower(t, Map.TowerX[t], Map.TowerY[t]); }
            Map.TowerTile0 = TileOf(Towers[0]); Map.TowerTile1 = TileOf(Towers[1]);
            if (cfg.Tutorial) { if (cfg.Island) StartIsland(); else StartTutorial(); SeatSingle(); return; }
            if (cfg.HeroTest) { StartHeroTest(); SeatSingle(); return; }
            if (cfg.Seats != null) { StartSeats(); AssignSpells(); return; }

            // the line-up: the player's hero and five others, drawn with the seed (bots never copy the player's hero)
            var pool = new List<HeroId>(7);
            foreach (var id in HeroCatalog.GameIds) if (id != cfg.PlayerHero) pool.Add(id);
            for (int i = pool.Count - 1; i > 0; i--) { int j = Rng.Int(i + 1); var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp; }
            var allies = cfg.Allies ?? new[] { pool[0], pool[1] };
            var enemies = cfg.Enemies ?? new[] { pool[2], pool[3], pool[4] };
            var allyTier = Defs.Tier(cfg.AllyDifficulty ?? cfg.Difficulty);
            var enemyTier = Defs.Tier(cfg.Difficulty);
            int[] mid = { 1, 0, 2 }; // the player in the middle, as in the old game
            if (TopFirst) TeamOfEnemies(enemies, enemyTier);
            for (int i = 0; i < 3; i++)
            {
                var id = i == 0 ? cfg.PlayerHero : allies[i - 1];
                int s = cfg.SlotOrder ? i : mid[i];
                var h = NewHero(id, Teams.Player, Map.SpawnX[0][s], Map.SpawnY[0][s]);
                if (i == 0) { h.IsPlayer = true; Player = h; if (cfg.PlayerIsBot) h.Bot = new BotBrain(h, allyTier, this); }
                else h.Bot = new BotBrain(h, allyTier, this);
            }
            if (!TopFirst) TeamOfEnemies(enemies, enemyTier);
            AssignSpells();
            SeatSingle();
        }

        void TeamOfEnemies(HeroId[] enemies, BotTier enemyTier)
        {
            for (int i = 0; i < enemies.Length && i < 3; i++)
            {
                var h = NewHero(enemies[i], Teams.Enemy, Map.SpawnX[1][i], Map.SpawnY[1][i]);
                h.Bot = new BotBrain(h, enemyTier, this);
            }
        }

        /// <summary>Which team's units come first in Units this match (heroes, then each wave). Whoever is updated last each tick
        /// acts on the other team's fresh moves and wins ties, which gave the top team 55 % of mirror matches (bug review nr 11):
        /// the seed picks the order, so neither side has it every match. From the seed, so it needs no state of its own.</summary>
        bool TopFirst => !Config.Tutorial && ((uint)Config.Seed * 2654435761u >> 31) != 0;

        // offline the player is seat 0
        void SeatSingle()
        {
            if (Player == null || Humans.Count > 0) return;
            Player.Seat = 0; Humans.Add(Player);
        }

        // An online line-up: each team's humans in the middle first (seat order), its other places bots; the bots' heroes are
        // drawn with the seed from those no human plays.
        void StartSeats()
        {
            var seats = Config.Seats;
            var pool = new List<HeroId>(8);
            foreach (var id in HeroCatalog.GameIds)
            {
                bool taken = false;
                for (int s = 0; s < seats.Length; s++) if (seats[s].Hero == id) taken = true;
                if (!taken) pool.Add(id);
            }
            for (int i = pool.Count - 1; i > 0; i--) { int j = Rng.Int(i + 1); var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp; }
            var tier = Defs.Tier(Config.Difficulty);
            var bySeat = new Unit[seats.Length];
            int[] mid = { 1, 0, 2 };
            int next = 0;
            for (int tt = 0; tt < 2; tt++)
            {
                int team = TopFirst ? 1 - tt : tt, place = 0;
                for (int s = 0; s < seats.Length && place < 3; s++)
                {
                    if (seats[s].Team != team) continue;
                    var h = NewHero(seats[s].Hero, team, Map.SpawnX[team][mid[place]], Map.SpawnY[team][mid[place]]);
                    h.Seat = s; bySeat[s] = h; place++;
                }
                for (; place < 3; place++)
                {
                    var id = pool.Count > 0 ? pool[next++ % pool.Count] : HeroId.Bram;
                    var h = NewHero(id, team, Map.SpawnX[team][mid[place]], Map.SpawnY[team][mid[place]]);
                    h.Bot = new BotBrain(h, tier, this);
                }
            }
            for (int s = 0; s < seats.Length; s++) Humans.Add(bySeat[s]); // a seat over a full team stays null (the lobby never makes one)
            int me = Config.LocalSeat;
            if (me >= 0 && me < Humans.Count && Humans[me] != null) { Player = Humans[me]; Player.IsPlayer = true; }
        }

        /// <summary>The human the bots of a team follow: its first seat still played by a person (offline: the player).</summary>
        public Unit LeaderOf(int team)
        {
            for (int i = 0; i < Humans.Count; i++) { var h = Humans[i]; if (h != null && h.Team == team && (h.Bot == null || Config.BotIsHuman)) return h; }
            return null;
        }

        /// <summary>Online: a person left, a bot of the match's level plays their hero from now on. Every phone calls it before
        /// the same tick.</summary>
        public void SeatToBot(int seat)
        {
            if (seat < 0 || seat >= Humans.Count) return;
            var h = Humans[seat];
            if (h == null || h.Bot != null) return;
            h.Bot = new BotBrain(h, Defs.Tier(Config.Difficulty), this);
            seatSpecBuf[seat] = 0; seatMoveX[seat] = seatMoveY[seat] = 0;
        }

        /// <summary>A fingerprint of the match: online phones compare it now and then, so a match that has drifted apart shows.</summary>
        public ulong Checksum()
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, Rng.State); h = Mix(h, (ulong)Ticks); h = Mix(h, (ulong)Units.Count); h = Mix(h, (ulong)Projs.Count);
            h = Mix(h, (ulong)(WaveN * 31 + Kills[0] * 7 + Kills[1]));
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                h = Mix(h, (ulong)u.Serial); h = Mix(h, Bits(u.X)); h = Mix(h, Bits(u.Y)); h = Mix(h, Bits(u.Hp)); h = Mix(h, Bits(u.Charge)); h = Mix(h, Bits(u.Facing));
                h = Mix(h, Bits(u.AtkTimer)); for (int s = 0; s < u.StCount; s++) { h = Mix(h, (ulong)u.Sts[s].Type); h = Mix(h, Bits(u.Sts[s].T)); }
            }
            for (int i = 0; i < Projs.Count; i++) { h = Mix(h, Bits(Projs[i].X)); h = Mix(h, Bits(Projs[i].Y)); h = Mix(h, Bits(Projs[i].T)); }
            return h;
        }
        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
        static ulong Bits(float f) => (uint)BitConverter.SingleToInt32Bits(f);

        int TileOf(Unit u) => (int)Math.Floor(u.Y) * Map.W + (int)Math.Floor(u.X);

        // ---------- making units ----------
        Unit NewUnit(UnitKind kind, int team, float x, float y)
        {
            var u = new Unit { Serial = nextSerial++, Kind = kind, Team = team, X = x, Y = y, PX = x, PY = y, Facing = team == Teams.Player ? -SimMath.PI / 2 : SimMath.PI / 2 };
            u.NoStars = NoStars;
            Units.Add(u);
            return u;
        }

        internal Unit NewHero(HeroId id, int team, float x, float y)
        {
            var h = NewUnit(UnitKind.Hero, team, x, y);
            h.Hero = id; h.HDef = Defs.Hero(id); h.R = h.HDef.Radius; h.Level = Level; h.Charge = 0;
            h.MaxHp = h.StatHp(); h.Hp = h.MaxHp; h.SpawnX = x; h.SpawnY = y;
            h.HeroIndex = Heroes.Count; Heroes.Add(h);
            h.MoX = x; h.MoY = y;
            return h;
        }

        internal Unit NewMinion(MinionKind kind, int team, float x, float y)
        {
            var m = NewUnit(UnitKind.Minion, team, x, y);
            m.Minion = kind; m.UDef = Defs.Minion(kind); m.R = m.UDef.Radius; m.MaxHp = m.UDef.Hp; m.Hp = m.MaxHp;
            m.Wp = 0; m.AiT = Rng.Range(0, 0.3f); m.SpawnX = x; m.SpawnY = y;
            m.MbScanT = Rng.Range(0, MnScan);
            return m;
        }

        Unit NewTower(int team, float x, float y)
        {
            var t = NewUnit(UnitKind.Tower, team, x, y);
            t.UDef = Defs.Tower; t.R = Defs.Tower.Radius; t.MaxHp = Defs.Tower.Hp; t.Hp = t.MaxHp;
            return t;
        }

        // ---------- the tick ----------
        public void Step(in PlayerInput input)
        {
            oneInput[0] = input;
            Step(oneInput);
        }

        /// <summary>One tick with every seat's controls (inputs[i] for Humans[i]; missing ones count as no input).</summary>
        public void Step(PlayerInput[] inputs)
        {
            Events.Clear();
            for (int i = 0; i < Units.Count; i++) { var u = Units[i]; u.PX = u.X; u.PY = u.Y; }
            for (int i = 0; i < Projs.Count; i++) { var p = Projs[i]; p.PX = p.X; p.PY = p.Y; }
            const float dt = Rules.Tick;
            if (State == MatchState.Over) return;
            if (State == MatchState.Countdown)
            {
                Time += dt;
                if (Time >= 0)
                {
                    Time = 0; State = MatchState.Play; Emit(EventType.MatchStart, null, null);
                    if (Tut == null) for (int i = 0; i < Heroes.Count; i++) AddStatus(Heroes[i], St.SpeedPct, Rules.StartSpeedTime, Rules.StartSpeedPct);
                }
                return;
            }
            if (Time != timeSet) timeD = Time;
            timeD += 1.0 / 60.0; Time = timeSet = (float)timeD; Ticks++;
            for (int s = 0; s < Humans.Count && s < inputs.Length && s < MaxSeats; s++)
            {
                var h = Humans[s]; if (h == null) continue;
                var input = inputs[s];
                seatMoveX[s] = input.MoveX; seatMoveY[s] = input.MoveY;
                if (input.Special) { seatSpecBuf[s] = Rules.SpecialBuffer; seatBufAng[s] = input.AimAngle; seatBufK[s] = input.AimK; seatSpecAt[s] = Time; }
                if (input.Spell) CastSpell(h, input.SpellAngle, input.SpellK); // the battle spell (Spells.cs)
                if (input.Signal != TeamSignal.None) CallSignal(h, input.Signal); // a team signal (Signals.cs)
            }

            // minions of a wave come out one by one
            for (int i = spawns.Count - 1; i >= 0; i--)
            {
                var s = spawns[i]; s.T -= dt;
                if (s.T > 0) { spawns[i] = s; continue; }
                spawns.RemoveAt(i);
                if (State != MatchState.Play) continue;
                float x = Map.MinionX[s.Team] + Rng.Range(-0.2f, 0.2f), y = Map.MinionY[s.Team] + Rng.Range(-0.2f, 0.2f);
                NewMinion(s.Kind, s.Team, x, y);
            }
            if (Tut != null) UpdateTutorial(dt);
            if (Config.HeroTest) HeroTestTick(dt);

            int lvl = Tut != null ? 1 : Config.HeroTest ? Level : Math.Min(Rules.MaxLevel, 1 + (int)Math.Floor(Time / Rules.SecondsPerLevel));
            if (lvl != Level)
            {
                Level = lvl;
                for (int i = 0; i < Units.Count; i++) if (Units[i].Kind == UnitKind.Hero) Units[i].SetLevel(lvl);
                Emit(EventType.LevelUp, null, null, lvl);
            }
            if (Tut == null && !Config.HeroTest) WaveT -= dt;
            if (WaveT <= 0) { WaveT += Rules.WavePeriod * PacingWaveMult(); SpawnWave(); }

            unitBuf.Clear(); unitBuf.AddRange(Units);
            // every other tick back to front: the unit updated last acts on fresh moves and wins ties, so neither team may always
            // be last (bug review nr 11; with TopFirst the mirror match is 50/50 for either order)
            for (int i = 0, nb = unitBuf.Count; i < nb; i++)
            {
                var u = unitBuf[(Ticks & 1) == 0 ? i : nb - 1 - i];
                if (u.Kind == UnitKind.Hero && !u.Alive && !u.IsClone)
                {
                    u.RespawnT -= dt;
                    if (u.RespawnT <= 0) Revive(u);
                    continue;
                }
                if (!u.Alive) { if (!float.IsNaN(u.DeadT)) u.DeadT -= dt; continue; }
                u.RevealT = Math.Max(0, u.RevealT - dt);
                BurnTick(u, dt);
                DotTick(u, dt);
                for (int k = u.StCount - 1; k >= 0; k--)
                {
                    u.Sts[k].T -= dt;
                    if (!(u.Sts[k].T > 0) || (u.Sts[k].Type == St.Shield && u.Sts[k].Amount <= 0)) u.RemoveAt(k);
                }
                if (!u.Alive) continue; // a burn or poison tick killed it
                if (u.KnockT > 0) KnockStep(u, dt);
                else if (u.Kind == UnitKind.Hero && Map.PadCount > 0) PadCheck(u);
                if (u.IsClone)
                {
                    u.LifeT -= dt;
                    if (u.LifeT <= 0 || !u.Owner.Alive) { EndClone(u); continue; }
                }
                if (u.Kind == UnitKind.Wolf)
                {
                    u.LifeT -= dt;
                    if (u.LifeT <= 0 || !u.Owner.Alive) { u.Alive = false; u.Hp = 0; u.DeadT = 0.6f; Emit(EventType.Death, null, u); continue; }
                }
                if (u.Kind == UnitKind.Hero)
                {
                    if (!u.IsClone) { TelegraphTick(u); UpdateSpecial(u, dt); SpellTick(u); if (u.Hero == HeroId.Kugg) KuggTick(u, dt); else if (u.Hero >= HeroId.Ragn) Hero8Tick(u, dt); }
                    if (!u.InFight(Time) && !u.IsClone)
                    {
                        u.RegenT += dt;
                        if (u.RegenT >= Rules.RegenTick) { u.RegenT = 0; Heal(u, u.MaxHp * Rules.RegenAmount, u); }
                    }
                    else u.RegenT = 0;
                    if (CanAct(u) && !u.Has(St.Casting) && !(u.KnockT > 0)) // a hero in a knockback slide (Hilde's keg) does nothing else till it ends
                    {
                        if (u.Bot != null) { if (u.SpellChanEnd < 0) u.Bot.Think(dt); } // a bot stands still while it recalls
                        else if (u.Seat >= 0) PlayerAct(u, dt);
                        else if (u.IsClone) AiClone(u, dt);
                    }
                    if (!u.IsClone) TrackMotion(u, dt);
                }
                else if (u.Kind == UnitKind.Minion) { if (CanAct(u)) AiMinion(u, dt); }
                else if (u.Kind == UnitKind.Wolf)
                {
                    if (u.Wolf == WolfMode.Fly) WolfFly(u, dt); // a thrown wolf lands even when stunned in the air (the old forced rush)
                    else if (CanAct(u)) AiWolf(u, dt);
                }
                else if (u.Kind == UnitKind.Tower)
                {
                    if (!u.InCombat(Time) && u.Hp < u.MaxHp * Rules.TowerRegenBelow)
                    {
                        u.RegenT += dt;
                        if (u.RegenT >= Rules.TowerRegenTick) { u.RegenT = 0; u.Hp = Math.Min(u.MaxHp * Rules.TowerRegenBelow, u.Hp + u.MaxHp * Rules.TowerRegenAmount); }
                    }
                }
                UpdateAttack(u, dt);
            }
            for (int s = 0; s < MaxSeats; s++)
            {
                if (seatActed[s] || Time - seatSpecAt[s] >= SpecialHold) seatSpecBuf[s] = Math.Max(0, seatSpecBuf[s] - dt);
                seatActed[s] = false;
            }

            // minions gone 0.6 s after death, clones at once
            int w = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                bool gone = (u.IsMinionLike && !u.Alive && u.DeadT <= 0) || (u.IsClone && !u.Alive);
                if (!gone) Units[w++] = u;
            }
            if (w < Units.Count) Units.RemoveRange(w, Units.Count - w);
            Separate();
            UpdateProjectiles(dt);
            FlushClones();
        }

        void PlayerAct(Unit u, float dt)
        {
            int s = u.Seat;
            seatActed[s] = true;
            MoveUnit(u, seatMoveX[s], seatMoveY[s], dt);
            if (seatSpecBuf[s] > 0)
            {
                bool cast = !OneTap ? TryCastSpecial(u, seatBufAng[s], seatBufK[s])
                    : float.IsNaN(seatBufAng[s]) || !DragAimOn ? TryTapSpecial(u) // a tap (or drag aim off): the random target in reach
                    : TryAimSpecial(u, seatBufAng[s], seatBufK[s]); // a drag: the player's own aim (OneTap.cs)
                if (cast) seatSpecBuf[s] = 0;
                // a press buffered while the special waits in its warning (Tikta walks in hers, so she gets here) is no denial: the
                // special is on its way (full review 2026-11-09 nr 106)
                else if (seatSpecBuf[s] <= dt && u.SpecialReady && !Telegraphing(u)) { if (u == Player) SpecialDeniedAt = Time; Emit(EventType.SpecialDenied, u, null); }
            }
        }

        // ---------- waves ----------
        static readonly MinionKind[] WaveKinds = { MinionKind.Melee, MinionKind.Siege, MinionKind.Ranged };
        static readonly int[] WaveCount = { 3, 1, 1 }, WaveEvery = { 1, 3, 1 };

        void SpawnWave()
        {
            WaveN++;
            for (int tt = 0; tt < 2; tt++)
            {
                int team = TopFirst ? 1 - tt : tt, k = 0;
                for (int e = 0; e < WaveKinds.Length; e++)
                {
                    if ((WaveN - 1) % WaveEvery[e] != 0) continue;
                    for (int i = 0; i < WaveCount[e]; i++) spawns.Add(new Spawn { T = (k++) * Rules.WaveStagger, Team = team, Kind = WaveKinds[e] });
                }
            }
        }

        float PacingWaveMult() => Tut != null ? 1 : Time >= 330 ? 0.7f : Time >= 210 ? 0.85f : 1;
        float PacingMinionTowerMult() => Tut != null ? 1 : Time >= 330 ? 1.4f : Time >= 270 ? 1.2f : 1;

        // ---------- what a team sees ----------
        /// <summary>Can team see u? Own units always; a recent hit shows the attacker for 2 s (and a target hit outside grass); a tower sees its range; a unit in
        /// grass is seen only by enemies within 2.5 tiles.</summary>
        public bool IsVisibleTo(Unit u, int team)
        {
            if (!u.Alive) return false;
            if (u.Team == team) return true;
            if (u.RevealT > 0) return true;
            var tw = Towers[team];
            if (tw != null && tw.Alive && SimMath.Dist(tw, u) <= tw.UDef.Range) return true;
            if (Map.InGrass(u.X, u.Y))
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    var o = Units[i];
                    if (o.Alive && o.Team == team && SimMath.Dist(o, u) <= (o.KuggShots > 0 ? KuggS.GrassSight : Rules.GrassSight)) return true; // Kugg's Anchor sees further into grass (Hero7.cs)
                }
                return false;
            }
            return true;
        }
        /// <summary>No enemy can see u right now (the HUD's crossed-out eye; ambush).</summary>
        public bool HiddenFromFoes(Unit u) => u.Alive && !IsVisibleTo(u, Teams.Other(u.Team));

        public enum Pick : byte { Any, Heroes, MinionLike, HeroOrMinion }

        static bool PickOk(Unit u, Pick p)
        {
            switch (p)
            {
                case Pick.Heroes: return u.Kind == UnitKind.Hero;
                case Pick.MinionLike: return u.Kind == UnitKind.Minion || u.Kind == UnitKind.Wolf;
                case Pick.HeroOrMinion: return u.Kind != UnitKind.Tower;
                default: return true;
            }
        }

        /// <summary>Living enemies of src that src's team can see, within range of src (plus their radius), not untargetable;
        /// los = only those with a clear sight line. Fills into and returns it.</summary>
        public List<Unit> EnemiesInRange(Unit src, float range, Pick pick, bool los, List<Unit> into)
        {
            into.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Team == src.Team || !PickOk(u, pick) || SimMath.Dist(src, u) > range + u.R || u.Has(St.Untargetable)) continue;
                if (!IsVisibleTo(u, src.Team)) continue;
                if (los && !Map.LosClear(src, u)) continue;
                into.Add(u);
            }
            return into;
        }

        public static Unit Nearest(List<Unit> l, float x, float y)
        {
            Unit best = null; float bd = float.MaxValue;
            for (int i = 0; i < l.Count; i++) { float d = SimMath.Dist(l[i], x, y); if (d < bd) { bd = d; best = l[i]; } }
            return best;
        }

        internal T PickRandom<T>(List<T> l) => l[Rng.Int(l.Count)];

        // ---------- events ----------
        internal void Emit(EventType type, Unit a, Unit b, float v = 0, byte kind = 0, bool big = false, float x = 0, float y = 0, DmgKind dk = DmgKind.Auto)
        {
            if (Events.Count >= 2048) return; // a safety net only: the game reads events every tick
            Events.Add(new SimEvent { Type = type, A = a, B = b, ASerial = a != null ? a.Serial : 0, BSerial = b != null ? b.Serial : 0, V = v, Kind = kind, Big = big, X = x, Y = y, Dmg = dk, Shot = emitShot });
        }

        /// <summary>The shot being updated (UpdateProjectiles), stamped on every event it sends (SimEvent.Shot).</summary>
        int emitShot;

        // ---------- projectiles ----------
        internal Proj NewProj(ProjMode mode, Unit src, float x, float y)
        {
            Proj p;
            if (projPool.Count > 0) { p = projPool[projPool.Count - 1]; projPool.RemoveAt(projPool.Count - 1); p.Reset(); }
            else p = new Proj();
            p.Serial = nextSerial++; p.Mode = mode; p.Src = src; p.Team = src != null ? src.Team : 0; p.X = p.PX = x; p.Y = p.PY = y;
            Projs.Add(p);
            return p;
        }

        // ---------- the end ----------
        void EndMatch(int winner)
        {
            if (State == MatchState.Over) return;
            State = MatchState.Over; Winner = winner;
            Emit(EventType.MatchOver, null, null, winner);
        }
    }
}
