using System;
namespace BAM.Sim
{
    public sealed partial class Match
    {
        /// <summary>Rickard 2026-11-12: "ravnas special ska man inte sikta på någon den använder man och färdas i samma riktning som
        /// man går helt enkelt", "precis som ron". A tap with nobody in reach sets the flock off the way she walks.</summary>
        public static void RavnaRunTable()
        {
            bool was = NightFlightOn; NightFlightOn = true;
            foreach (var (mx, my, name) in new[] { (0f, 1f, "up"), (1f, 0f, "right"), (-0.7f, -0.7f, "down-left") })
            {
                var m = OneTapScene(HeroId.Ravna, out var p, out _, out _);
                p.Charge = p.HDef.MaxCharge; m.Telegraph = true;   // charged; the warning is on, as in a real match: Night Flight skips it
                float x0 = p.X, y0 = p.Y; bool flew = false; int at = -1;
                for (int k = 0; k < 40; k++)
                {
                    var i = k == 10 ? TapIn() : PlayerInput.None; i.MoveX = mx; i.MoveY = my;
                    m.Step(i);
                    if (p.RavnaFlying && !flew) { flew = true; at = k; }
                    if (k == 10) { x0 = p.X; y0 = p.Y; }
                }
                Check(flew && at <= 11, $"RAVNA: the tap flies at once, no target, no warning ({name}, tick {at})");
                float dx = p.X - x0, dy = p.Y - y0, len = (float)Math.Sqrt(dx * dx + dy * dy);
                float dot = len > 0 ? (dx * mx + dy * my) / (len * (float)Math.Sqrt(mx * mx + my * my)) : 0;
                Check(len > 1f && dot > 0.9f, $"RAVNA: the flock goes the way she walks ({name}: {len:0.0} tiles, cos {dot:0.00})");
            }
            NightFlightOn = was;
        }
    }
}
