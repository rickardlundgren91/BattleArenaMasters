using BAM.Sim;

namespace BAM.Core
{
    /// <summary>
    /// An online match's link to the other phones (BAM.Online, only in builds with BAM_ONLINE). Every phone runs the same match
    /// from the same controls (lockstep): the game hands this phone's controls to <see cref="Submit"/> 60 times a second and steps
    /// the rules only with what <see cref="Next"/> gives, which may be nothing for a moment (a late packet) or several ticks.
    /// </summary>
    public interface IMatchNet
    {
        /// <summary>The match to create: seats, seed, bots' level, this phone's seat.</summary>
        MatchConfig Config { get; }
        /// <summary>This phone sees the map turned half a circle (it plays the top team).</summary>
        bool Turned { get; }
        /// <summary>This phone's controls for one tick, as the HUD read them.</summary>
        void Submit(in PlayerInput input);
        /// <summary>The next tick's controls for every seat (and any seat turned into a bot, done on m). False = not known yet.</summary>
        bool Next(Match m, PlayerInput[] inputs);
        /// <summary>Ticks waiting beyond what this phone keeps in hand against network jitter; the game steps a little faster while it is above 1.</summary>
        int Backlog { get; }
        /// <summary>After every step.</summary>
        void AfterStep(Match m);
        /// <summary>After the ticks of a frame: send what was gathered.</summary>
        void Flush();
        /// <summary>A short line for the HUD ("Waiting for Kim…"), null while all is well.</summary>
        string Status { get; }
        /// <summary>The match ended (quit: this player left it before the end).</summary>
        void MatchEnded(bool quit);

        // ---- guest prediction (2026-10-31, Rickard: as a guest the hero answered late): the game steps the confirmed match with
        // Next as before (fingerprints, the end), and shows a copy of it (SimMirror) run ahead to Display with Predict's controls

        /// <summary>This phone shows a predicted match (a guest, with a usable SimMirror). False: Next's match is the one shown.</summary>
        bool Predicts { get; }
        /// <summary>Frames the confirmed match has stepped.</summary>
        long Confirmed { get; }
        /// <summary>The tick this phone's next controls are for: the shown match is run up to here.</summary>
        long Display { get; }
        /// <summary>Ticks to run more (+) or fewer (-) this frame, so this phone's controls reach the host just in time.</summary>
        int Adjust();
        /// <summary>The controls to run frame f of the shown match with (this phone's own, the others' last known).</summary>
        void Predict(long f, PlayerInput[] inputs);
        /// <summary>After the shown match stepped frame f (its fingerprint, to check against the confirmed match's).</summary>
        void PredictedStep(long f, Match shown);
        /// <summary>After the confirmed match stepped: true when the shown match must be copied from it again, because a guess since
        /// the last copy was wrong (the others' controls, a late own one, a seat turned into a bot) or a fingerprint differed. False:
        /// the shown match already is the confirmed one run ahead with the right controls, and is kept as it is (no copy, no rerun).</summary>
        bool TakeMiss();
    }
}
