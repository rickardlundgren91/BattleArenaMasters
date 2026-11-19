using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BAM.Art
{
    // The end of a match (MatchViews, as the tower falls): the winners dance and the losers shake their heads, played in place of
    // the idle until the result card comes up (Rickard 2026-11-19: "ta det du tror kommer göra stor skillnad för spelet").
    // The clips are Quaternius' Universal Animation Library 1 and 2 (CC0, quaternius.com): Dance_Loop and Idle_No_Loop, each cut
    // to its own small file in Resources/BAMArt/Poses (Victory.fbx, Defeat.fbx; imported as humanoid by PoseClipImport), so they
    // play on every hero's Mixamo rig. A body without them (the code-built ones, a missing file) keeps its idle.
    sealed partial class BodyView
    {
        static AnimationClip victoryClip, defeatClip;
        static bool posesLoaded;
        AnimationClip idleSrc; // the hero's own idle, put back by EndPoseReset
        int endPose;

        static AnimationClip PoseClip(string name)
        {
            var all = Resources.LoadAll<AnimationClip>("BAMArt/Poses/" + name);
            foreach (var c in all) if (c != null && !c.name.StartsWith("__preview__")) return c;
            if (all.Length == 0) Debug.LogWarning("[BAM.Art] no clip in Resources/BAMArt/Poses/" + name + ": the heroes keep their idle at the end");
            return null;
        }

        public override void SetEndPose(int pose)
        {
            if (pose == endPose || clips == null || !graph.IsValid()) return;
            if (!posesLoaded) { posesLoaded = true; victoryClip = PoseClip("Victory"); defeatClip = PoseClip("Defeat"); }
            if (idleSrc == null) idleSrc = clips[CIdle].GetAnimationClip();
            AnimationClip c = pose == 1 ? victoryClip : pose == 2 ? defeatClip : idleSrc;
            if (c == null) return;
            endPose = pose;
            SwapIdle(c);
        }

        void EndPoseReset()
        {
            if (endPose == 0) return;
            endPose = 0;
            if (idleSrc != null && clips != null && graph.IsValid()) SwapIdle(idleSrc);
        }

        // a new playable for the idle's input: the mixer keeps its weight, AnimateImported keeps looping it (Wrap)
        void SwapIdle(AnimationClip c)
        {
            float w = mixer.GetInputWeight(CIdle);
            graph.Disconnect(mixer, CIdle);
            clips[CIdle].Destroy();
            clips[CIdle] = AnimationClipPlayable.Create(graph, c);
            clips[CIdle].SetApplyFootIK(true);
            clipLen[CIdle] = Mathf.Max(0.1f, c.length);
            graph.Connect(clips[CIdle], 0, mixer, CIdle);
            mixer.SetInputWeight(CIdle, w);
        }
    }
}
