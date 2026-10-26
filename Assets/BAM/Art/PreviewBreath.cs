using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The menu hero's living idle, laid on top of its idle clip after the Animator (old Speltest HeroMixamo.MxBreath): a breath
    /// in the chest and shoulders, the weight rocking from side to side, a loose right arm, a head that looks about, and every
    /// 6 s a real look to one side and back. Some idle clips are one held pose (Blast's, Moa's, Bram's barely move), so without
    /// this the menu hero stands stiff (Rickard 2026-10-26: "goblin i menyn står helt stel"). Menu previews only.
    /// </summary>
    sealed class PreviewBreath
    {
        readonly Transform spine, chest, upperChest, neck, head, lShoulder, rShoulder, rUpperArm, rLowerArm;
        readonly float phase; // each hero out of step with the others; Tyra 0, as in the old game

        PreviewBreath(Animator an, int heroIndex)
        {
            spine = an.GetBoneTransform(HumanBodyBones.Spine);
            chest = an.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = an.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = an.GetBoneTransform(HumanBodyBones.Neck);
            head = an.GetBoneTransform(HumanBodyBones.Head);
            lShoulder = an.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rShoulder = an.GetBoneTransform(HumanBodyBones.RightShoulder);
            rUpperArm = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rLowerArm = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            phase = heroIndex == (int)BAM.Sim.HeroId.Tyra ? 0f : ((heroIndex * 97 + 31) & 255) * 0.05f;
        }

        /// <summary>null when the model has no humanoid avatar (nothing to move).</summary>
        internal static PreviewBreath For(Animator an, int heroIndex)
        {
            if (an == null || !an.isHuman) return null;
            var b = new PreviewBreath(an, heroIndex);
            return b.spine != null ? b : null;
        }

        /// <summary>After the Animator has posed the body this frame. w: 1 at rest, 0 while a swing plays.</summary>
        internal void Apply(float time, float w)
        {
            if (w <= 0.001f) return;
            float t = time + phase, br = Mathf.Sin(t * 1.9f) * w, sw = Mathf.Sin(t * 0.7f) * w, sh = Mathf.Sin(t * 0.45f + 1f) * w, lk = Mathf.Sin(t * 0.31f) * w;
            Turn(spine, 2.2f * br, 6f * sh, 5f * sw);
            Turn(chest, 2.8f * br, 4f * sh, -3f * sw);
            Turn(upperChest, 1.5f * br, 0f, 0f);
            Turn(head, -1.5f * br + 2f * lk, 14f * lk + 6f * sw, 5f * sh);
            Turn(lShoulder, 0f, 0f, 3.5f * br);
            Turn(rShoulder, 0f, 0f, -3.5f * br);
            Turn(rUpperArm, 8f * sw, 0f, 6f * sh);
            Turn(rLowerArm, 0f, 0f, 9f * sh);
            // every 6 s, 2.4 s long, the head and chest turn about 28 degrees; the side changes each time
            float gc = t / 6f, gp = (gc - Mathf.Floor(gc)) * 6f, gs = ((int)Mathf.Floor(gc) & 1) == 0 ? 1f : -1f;
            float ge = gp < 2.4f ? Mathf.Sin(gp / 2.4f * Mathf.PI) : 0f;
            ge = ge * ge * gs * w;
            Turn(chest, 0f, 9f * ge, 0f);
            Turn(neck, 0f, 8f * ge, 0f);
            Turn(head, -3f * Mathf.Abs(ge), 11f * ge, 0f);
        }

        static void Turn(Transform b, float x, float y, float z)
        {
            if (b != null) b.localRotation *= Quaternion.Euler(x, y, z);
        }
    }
}
