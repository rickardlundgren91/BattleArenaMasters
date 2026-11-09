using System;

namespace BAM.Sim
{
    // Tester Øli (#feedback, reproduced 2026-11-06): the tower shoots him, he steps out, his own minion walks in first, he steps
    // back in and the tower takes him again although the minion lives. TOWER RULE (Rickard 2026-11-05): the tower keeps the first
    // target that enters its reach until it dies or leaves; it switches only to an enemy hero who attacks one of its heroes
    // while standing in its reach. Being hit by a defender, or a fight from outside the reach, is no reason to switch.
    public sealed partial class Match
    {
        /// <summary>Øli's scene: the tower has shot the player, the player stepped out, the player's minion walked in and is the
        /// tower's target now. The defender (an enemy hero) stands just outside the reach. Returns where the player re-enters.</summary>
        static Match ReturnScene(out Unit p, out Unit tw, out Unit mn, out Unit foe, out float inX, out float inY)
        {
            var m = LockScene(out p, out tw);
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
            m.Still(p, tw.X, tw.Y + dir * (tw.Range + p.R + 2f)); // out of the reach
            for (int k = 0; k < 60; k++) m.Step(PlayerInput.None);
            mn = m.NewMinion(MinionKind.Melee, p.Team, tw.X + 1f, tw.Y + dir * (tw.R + 1.2f)); m.Still(mn, mn.X, mn.Y);
            for (int k = 0; k < 60; k++) m.Step(PlayerInput.None);
            int team = p.Team; foe = m.Heroes.Find(h => h.Team != team);
            m.Still(foe, tw.X - 1.5f, tw.Y + dir * (tw.Range + foe.R + 1.5f)); // the defender, just outside the reach
            inX = tw.X + 1.5f; inY = tw.Y + dir * (tw.Range - 1.5f);
            return m;
        }

        static void TowerReturnChecks()
        {
            // A: he simply walks back in
            var m = ReturnScene(out var p, out var tw, out var mn, out var foe, out var x, out var y);
            Check(tw.Target == mn, "RETURN: the tower turned to the minion that walked in after the hero left");
            m.Still(p, x, y);
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            Check(tw.Target == mn, "RETURN: a hero walking back in does not take the tower off his living minion");

            // B: a defender hits him while he walks back in (he is only hit, he does not attack)
            m = ReturnScene(out p, out tw, out mn, out foe, out x, out y);
            m.Still(p, x, y);
            for (int k = 0; k < 120; k++) { if (k % 20 == 0) m.DealDamage(foe, p, 1, DmgKind.Auto); m.Step(PlayerInput.None); }
            Check(tw.Target == mn, "RETURN: being hit by a defender does not draw the tower");

            // C: he hit the defender from outside the reach, then walks in at once
            m = ReturnScene(out p, out tw, out mn, out foe, out x, out y);
            m.DealDamage(p, foe, 1, DmgKind.Auto); m.Step(PlayerInput.None);
            m.Still(p, x, y);
            for (int k = 0; k < 90; k++) m.Step(PlayerInput.None); // 1.5 s: inside the old 2 s fight memory, the tower shoots in it
            Check(tw.Target == mn, "RETURN: a hit dealt outside the reach does not draw the tower after walking in");

            // D: he attacks the defender from inside the reach: now the tower switches to him (the rule's one exception)
            m = ReturnScene(out p, out tw, out mn, out foe, out x, out y);
            m.Still(p, x, y);
            for (int k = 0; k < 30; k++) m.Step(PlayerInput.None);
            m.DealDamage(p, foe, 1, DmgKind.Auto);
            for (int k = 0; k < 30; k++) m.Step(PlayerInput.None);
            Check(tw.Target == p, "RETURN: attacking a defender from inside the reach draws the tower");
            Check(mn.Alive, "RETURN: (the minion is still alive in every case)");
        }
    }
}
