using System;
using System.Collections.Generic;
using UnityEngine;

namespace BAM.UI
{
    /// <summary>
    /// Where the online pages (BAM.Online.UI, only in builds with BAM_ONLINE) plug into the menus. BAM.Online.UI references BAM.UI,
    /// so BAM.UI cannot call it directly: the online side fills these before the game boots (OnlineHookup), and the menus show
    /// an online entry only when its hook is set. Offline every hook is null and the menus look as before.
    /// </summary>
    public static class OnlineHooks
    {
        /// <summary>Android Back: true when an online page or invite was open and closed.</summary>
        public static Func<bool> Back;
        /// <summary>Home's top-left profile chip (the old one, cw(40) x cw(12)), built in the given parent; a tap opens the profile.</summary>
        public static Func<RectTransform, RectTransform> HomeChip;
        public static Action OpenFriends, OpenAccount, OpenPlayFriends, OnlineMatch;
        /// <summary>How many friends are online now (the badge on Home's FRIENDS button).</summary>
        public static Func<int> FriendsOnline;
        /// <summary>Once, after the first match after the tutorial: asks to go online. True when the question was shown.</summary>
        public static Func<bool> AskToGoOnline;
        /// <summary>What the match that just ended gave the account, for the result card (Xp 0 = show nothing).</summary>
        public static Func<MatchGain> LastGain;
        /// <summary>The account level that opens a hero, 0 when it is open (offline builds: null, every hero open).</summary>
        public static Func<BAM.Sim.HeroId, int> HeroLockLevel;
        /// <summary>Home's "+ INVITE" pills beside the hero (Speltest HubInvitePill3): the invite panel, friends first, then everyone online.</summary>
        public static Action OpenInvite;
        /// <summary>How many other players are searching ONLINE MATCH now (the green light on the ONLINE MATCH row).</summary>
        public static Func<int> Searching;
        /// <summary>The XP the day's first win still gives (0 when today's first win is done), for the tag on Home's PLAY.</summary>
        public static Func<int> FirstWinXp;
        /// <summary>The account level now.</summary>
        public static Func<int> Level;
        /// <summary>Matches counted on the account (the HUD hides its TAP/DRAG help after a few).</summary>
        public static Func<int> Matches;

        // ---- gold (BAM.Online Gold.cs, Rickard 2026-11-15): null in offline builds, where Home shows no gold and every skin is open ----
        /// <summary>The player's gold now.</summary>
        public static Func<int> Gold;
        /// <summary>The gold the day's first win still gives (0 once today has a win).</summary>
        public static Func<int> FirstWinGold;
        /// <summary>Today's three goals for Home's DAILY list.</summary>
        public static Func<List<DailyGoal>> DailyGoals;
        /// <summary>What a skin costs to wear (HeroSkins number): 0 = owned, &gt; 0 = gold to buy it first, -1 = coming soon.</summary>
        public static Func<BAM.Sim.HeroId, int, int> SkinPrice;
        /// <summary>Buys the hero's gold skin; the callback gets null when bought, else why not.</summary>
        public static Action<BAM.Sim.HeroId, Action<string>> BuySkin;

        /// <summary>The party on Home (Speltest MenuHome3.HubTeam3): null when this phone is not in a room with friends.</summary>
        public static Func<PartyView> Party;
        /// <summary>PLAY while in a party: the leader starts the match, the others are told to wait. False: not in a party, play as usual.</summary>
        public static Func<bool> PartyPlay;
        /// <summary>LEAVE PARTY on Home: asks first (Speltest HubLeaveConfirm3).</summary>
        public static Action LeaveParty;
        /// <summary>The party's bots and mode, from the mode sheet (only the leader can change them).</summary>
        public static Action<BAM.Sim.Difficulty> PartyBots;
        public static Action<int> PartyMode;
        /// <summary>The party's team size (2 or 3) and map, from the mode sheet (only the leader can change them).</summary>
        public static Action<int> PartySize;
        public static Action<BAM.Sim.MapId> PartyMap;
        /// <summary>A small picture the online side draws with the old menu code into the given rect: "tag0" (the player's name tag
        /// with crown and pencil), "tag1", "tag2" (the party members' name tags) and "leave" (the LEAVE PARTY pill).</summary>
        public static Func<RectTransform, string, RectTransform> Paint;
        /// <summary>The editor's test party (BAM > Test Party), so the painter draws its name tags too; null when there is none.</summary>
        public static Func<PartyView> TestParty;
    }

    /// <summary>One of today's goals on Home: "Win 2 matches", 1 of 2.</summary>
    public struct DailyGoal
    {
        public string Text;
        public int Have, Need;
        public bool Done;
    }

    /// <summary>What Home shows of the party: up to two other players beside the player's hero (Speltest PartyMembers).</summary>
    public sealed class PartyView
    {
        /// <summary>This phone leads the party (it made the room): its PLAY starts the match.</summary>
        public bool Leader;
        /// <summary>0 together against bots, 1 against each other.</summary>
        public int Mode;
        public BAM.Sim.Difficulty Bots;
        /// <summary>Heroes per team: 3 (3v3) or 2 (2v2).</summary>
        public int Size = 3;
        public BAM.Sim.MapId Map;
        /// <summary>How many of Mates are filled (0 to 2).</summary>
        public int Count;
        public readonly PartyMate[] Mates = new PartyMate[2];
    }

    public struct PartyMate
    {
        public string Name;
        public BAM.Sim.HeroId Hero;
        /// <summary>Back on Home (not still in the last match): the ring is mint, else grey.</summary>
        public bool Ready;
    }

    /// <summary>The account XP line on the result card (Speltest HudV3 drawResult): "+160 XP (first win today)", the level bar.</summary>
    public struct MatchGain
    {
        public int Xp, Rating, Level, LevelBefore;
        /// <summary>The gold the match gave (first win, goals, level-ups); 0 = none.</summary>
        public int Gold;
        /// <summary>The Rating part of the line: "+25 Rating", "-10 Rating", or why there is none ("Max Rating from Easy bots").</summary>
        public string RatingText;
        public bool FirstWin;
        /// <summary>0..1 into the current level, and its text: "40 / 150 XP" or "Max level".</summary>
        public float LevelProgress;
        public string LevelText;
        /// <summary>Heroes the level-up opened, e.g. "Moa"; null for none.</summary>
        public string Unlocked;
    }
}
