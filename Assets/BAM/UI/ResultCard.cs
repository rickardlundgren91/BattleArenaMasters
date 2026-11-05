using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The old game's result card (ABC2 HudV3.cs drawResult + drawScores, SimScore.cs MatchScore): a dark sheet over the match, a
    // card with VICTORY or DEFEAT and the difficulty and length under it, then YOUR TEAM and ENEMIES with every hero's portrait,
    // name, role, K/D/A, rating 0-10 and MVP (gold on the winning side, silver on the losing side), and PLAY AGAIN and HOME
    // under the card. The guided match gets the old small card instead: the hero's portrait, name and KILLS / DEATHS / ASSISTS.
    // Everything is measured in the old W and H (the safe area's width and height), like the canvas code it comes from.
    public static partial class Screens
    {
        static readonly Color ResDim = new Color(6 / 255f, 9 / 255f, 26 / 255f, 0.82f), ResCard = Hex(0x151a42), ResWin = Hex(0xffc94a);
        static readonly Color ResLose = Hex(0x6b7199), ResLoseTitle = Hex(0xc9cdee), ResSub = Hex(0x9aa2d6), ResTitleDrop = new Color(5 / 255f, 12 / 255f, 40 / 255f, 0.6f);
        static readonly Color ResBlueHead = Hex(0x7fb2ff), ResRedHead = Hex(0xff8a96), ResBlueRing = Hex(0x4f8cff), ResRedRing = Hex(0xff5a6a);
        static readonly Color ResMe = Hex(0x7af7d6), ResMeRow = new Color(122 / 255f, 247 / 255f, 214 / 255f, 0.12f), ResKda = Hex(0xe6e8ff);
        static readonly Color ResSilver = Hex(0xb9c0e0), ResSilverText = Hex(0xdfe4ff), ResMvpGoldInk = Hex(0x3a2400), ResMvpSilverInk = Hex(0x1a1f4a);
        static readonly Color ResOrange = Hex(0xff9a2e), ResOrangeInk = Hex(0x3a1a00), ResNavy = Hex(0x262d66), ResNavyEdge = new Color(154 / 255f, 162 / 255f, 214 / 255f, 0.5f);
        static readonly Color ResPicBack = Hex(0x2b2350), ResDeaths = Hex(0xff8a96);
        static readonly Color ResXpBack = new Color(1, 1, 1, 0.12f), ResXpFill = Hex(0x3ee6b0), ResLevelUp = Hex(0xffd257);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        sealed class ResRow
        {
            public RectTransform Root, Disc;
            public Image Mine, Ring, Mvp;
            public RawImage Pic;
            public Text Name, Role, Kda, Score, MvpText;
        }

        static RectTransform resBody, resCard, resTable, resSmall;
        static Image resCardBack, resCardEdge, resBigRing;
        static Text resTitle, resTitleDrop, resSub, resBigName;
        static readonly Text[] resHead = new Text[2], resKdaHead = new Text[2], resScoreHead = new Text[2];
        static readonly ResRow[] resRows = new ResRow[6];
        static readonly Text[] resBigValue = new Text[3], resBigLabel = new Text[3];
        static RectTransform resBigDisc;
        static RawImage resBigPic;
        static Button resAgain, resHome;
        static Image resAgainShadow, resHomeShadow, resHomeEdge, resAgainShine;
        static Text resAgainText, resHomeText;
        static CanvasGroup resFade;
        // the account XP under the table or the small card (online builds; Speltest HudV3 drawResult's Progression lines)
        static Text resXp, resLevel, resUnlock;
        static Image resXpBack, resXpFill;
        static float resXpPart;
        static bool resTableMode, resWon;
        static int resRowCount;
        static readonly int[] resRowsPerTeam = new int[2];

        // ---- the match score (SimScore.cs MatchScore), kept per hero by HeroIndex ----

        static Match scoreMatch;
        static readonly float[] scDmgHeroes = new float[6], scDmgTower = new float[6], scTaken = new float[6], scHelped = new float[6];

        /// <summary>
        /// Counts what each hero did in the last Step for the result card's ratings: damage to enemy heroes and to the tower, damage
        /// taken, healing and shields given to other heroes. The game calls it after every Match.Step; a new match starts from zero.
        /// It only reads the events, so it never changes the match.
        /// </summary>
        public static void CountEvents(Match m)
        {
            if (m == null) return;
            if (!ReferenceEquals(m, scoreMatch))
            {
                scoreMatch = m;
                System.Array.Clear(scDmgHeroes, 0, 6); System.Array.Clear(scDmgTower, 0, 6);
                System.Array.Clear(scTaken, 0, 6); System.Array.Clear(scHelped, 0, 6);
            }
            var events = m.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.V <= 0 || e.B == null) continue;
                switch (e.Type)
                {
                    case BAM.Sim.EventType.Damage:
                        int a = ScoreHero(e.A);
                        if (a >= 0 && e.A.Team != e.B.Team)
                        {
                            if (RealHero(e.B)) scDmgHeroes[a] += e.V;
                            else if (e.B.Kind == UnitKind.Tower) scDmgTower[a] += e.V;
                        }
                        if (e.A != null && RealHero(e.B)) scTaken[e.B.HeroIndex] += e.V;
                        break;
                    case BAM.Sim.EventType.Heal:
                    case BAM.Sim.EventType.Shield: // the old game counted what the shield took; the shield's size is what the events give us
                        int b = ScoreHero(e.A);
                        if (b >= 0 && RealHero(e.B) && e.B.HeroIndex != b && e.B.Team == e.A.Team) scHelped[b] += e.V;
                        break;
                }
            }
        }

        static bool RealHero(Unit u) => u != null && u.Kind == UnitKind.Hero && !u.IsClone && u.HeroIndex >= 0 && u.HeroIndex < 6;

        // a clone's or wolf's work counts for Fenn
        static int ScoreHero(Unit u)
        {
            if (u == null) return -1;
            if (u.Owner != null) u = u.Owner;
            return RealHero(u) ? u.HeroIndex : -1;
        }

        struct Rated { public Unit U; public float Rating; public bool Mvp; public string Role; }
        static readonly Rated[] rated = new Rated[6];

        // role weights for damage to heroes, damage to the tower, damage taken, healing + shields (each row sums to 4.5)
        static void RoleWeights(string role, out float d, out float t, out float k, out float h)
        {
            switch (role)
            {
                case "Tank": d = 1.0f; t = 0.5f; k = 2.5f; h = 0.5f; break;
                case "Support": d = 1.0f; t = 0.5f; k = 0.5f; h = 2.5f; break;
                case "Assassin": case "Mage": d = 2.5f; t = 1.0f; k = 0.5f; h = 0.5f; break;
                case "Marksman": d = 2.5f; t = 1.5f; k = 0.25f; h = 0.25f; break;
                default: d = 2.0f; t = 1.0f; k = 1.0f; h = 0.5f; break; // Fighter
            }
        }

        // a hero's share of its team's total: an even share gives 0.5, twice an even share (or more) gives 1
        static float Part(float mine, float sum, int n) => sum <= 0 ? 0 : Mathf.Min(1f, mine / sum * n / 2f);

        // MatchScore.Rate: base 3, +1 for the win, kill participation up to +2, kills up to +1, the four shares up to +4.5,
        // -0.35 per death (at most -2.5), 0-10; each team best first (rating, then kills, then damage to heroes), the best is MVP.
        static int RateHeroes(Match m)
        {
            bool counted = ReferenceEquals(m, scoreMatch);
            int n = 0;
            for (int i = 0; i < m.Heroes.Count && n < 6; i++)
                if (RealHero(m.Heroes[i])) rated[n++] = new Rated { U = m.Heroes[i] };
            for (int team = 0; team < 2; team++)
            {
                int kills = 0, count = 0;
                float sd = 0, st = 0, sk = 0, sh = 0;
                for (int i = 0; i < n; i++)
                {
                    var u = rated[i].U;
                    if (u.Team != team) continue;
                    count++; kills += u.Kills;
                    if (counted) { sd += scDmgHeroes[u.HeroIndex]; st += scDmgTower[u.HeroIndex]; sk += scTaken[u.HeroIndex]; sh += scHelped[u.HeroIndex]; }
                }
                for (int i = 0; i < n; i++)
                {
                    var u = rated[i].U;
                    if (u.Team != team) continue;
                    string role = HeroCatalog.Get(u.Hero).Role;
                    RoleWeights(role, out float wd, out float wt, out float wk, out float wh);
                    float r = 3f + (m.Winner == team ? 1f : 0f);
                    r += 2f * (kills > 0 ? Mathf.Min(1f, (u.Kills + u.Assists) / (float)kills) : 0f);
                    r += Mathf.Min(1f, u.Kills * 0.15f);
                    if (counted)
                    {
                        int h = u.HeroIndex;
                        r += wd * Part(scDmgHeroes[h], sd, count) + wt * Part(scDmgTower[h], st, count) + wk * Part(scTaken[h], sk, count) + wh * Part(scHelped[h], sh, count);
                    }
                    r -= Mathf.Min(2.5f, u.Deaths * 0.35f);
                    rated[i].Rating = Mathf.Round(Mathf.Clamp(r, 0f, 10f) * 10f) / 10f;
                    rated[i].Role = role;
                }
            }
            // best first (a small insertion sort: six rows)
            for (int i = 1; i < n; i++)
            {
                var x = rated[i];
                int j = i - 1;
                while (j >= 0 && Better(x, rated[j])) { rated[j + 1] = rated[j]; j--; }
                rated[j + 1] = x;
            }
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < n; i++)
                    if (rated[i].U.Team == team) { rated[i].Mvp = true; break; }
            return n;
        }

        static bool Better(Rated a, Rated b)
        {
            if (a.Rating != b.Rating) return a.Rating > b.Rating;
            if (a.U.Kills != b.U.Kills) return a.U.Kills > b.U.Kills;
            bool counted = scoreMatch != null;
            return counted && scDmgHeroes[a.U.HeroIndex] > scDmgHeroes[b.U.HeroIndex];
        }

        // ---- building ----

        static void BuildResult()
        {
            resBody = NewScreen("Result", true, out resultScreen);
            var dim = resultScreen.transform.GetChild(0).GetComponent<Image>();
            if (dim != null) dim.color = ResDim;
            resFade = resultScreen.AddComponent<CanvasGroup>();
            resultScreen.AddComponent<ResultFx>();

            resCard = UiKit.NewRect("Card", resBody);
            resCardBack = Pic(resCard, "Back", UiKit.Rounded, ResCard);
            UiKit.Stretch(resCardBack.rectTransform);
            resCardEdge = Pic(resCard, "Edge", null, ResWin);
            UiKit.Stretch(resCardEdge.rectTransform);

            resTitleDrop = ResText(resCard, true, ResTitleDrop, TextAnchor.MiddleCenter);
            resTitle = ResText(resCard, true, ResWin, TextAnchor.MiddleCenter);
            resSub = ResText(resCard, false, ResSub, TextAnchor.MiddleCenter);

            // the score table
            resTable = UiKit.NewRect("Scores", resCard);
            for (int t = 0; t < 2; t++)
            {
                resHead[t] = ResText(resTable, false, t == 0 ? ResBlueHead : ResRedHead, TextAnchor.MiddleLeft);
                resKdaHead[t] = ResText(resTable, false, ResSub, TextAnchor.MiddleCenter);
                resKdaHead[t].text = "K/D/A";
                resScoreHead[t] = ResText(resTable, false, ResSub, TextAnchor.MiddleCenter);
                resScoreHead[t].text = "SCORE";
            }
            for (int i = 0; i < resRows.Length; i++) resRows[i] = MakeResRow();

            // the small card (the guided match)
            resSmall = UiKit.NewRect("Small", resCard);
            resBigDisc = ResDisc(resSmall, out resBigPic, out resBigRing);
            resBigName = ResText(resSmall, true, UiKit.TextColor, TextAnchor.MiddleCenter);
            string[] labels = { "KILLS", "DEATHS", "ASSISTS" };
            for (int i = 0; i < 3; i++)
            {
                resBigValue[i] = ResText(resSmall, true, i == 1 ? ResDeaths : UiKit.TextColor, TextAnchor.MiddleCenter);
                resBigLabel[i] = ResText(resSmall, false, ResSub, TextAnchor.MiddleCenter);
                resBigLabel[i].text = labels[i];
            }

            resXp = ResText(resCard, true, ResMe, TextAnchor.MiddleCenter);
            // one line that shrinks to fit the card: "+210 XP · Max Rating from Normal bots (first win today)" ran about 30 % past
            // the card's edges (bug review 2026-11-02, nr 21). LayoutXp makes its box one line tall, so it is not wrapped
            resXp.horizontalOverflow = HorizontalWrapMode.Wrap;
            resXp.verticalOverflow = VerticalWrapMode.Truncate;
            resXp.resizeTextForBestFit = true;
            resXpBack = Pic(resCard, "XpBack", UiKit.Rounded, ResXpBack);
            resXpFill = Pic(resCard, "XpFill", UiKit.Rounded, ResXpFill);
            resLevel = ResText(resCard, false, ResSub, TextAnchor.MiddleCenter);
            resUnlock = ResText(resCard, false, ResLevelUp, TextAnchor.MiddleCenter);

            resAgain = ResButton("PLAY AGAIN", true, RestartMatch, out resAgainShadow, out resAgainText, out resAgainShine);
            resHome = ResButton("HOME", false, GoToMenu, out resHomeShadow, out resHomeText, out resHomeEdge);
        }

        static ResRow MakeResRow()
        {
            var r = new ResRow();
            r.Root = UiKit.NewRect("Row", resTable);
            r.Mine = Pic(r.Root, "Mine", UiKit.Rounded, ResMeRow);
            r.Disc = ResDisc(r.Root, out r.Pic, out r.Ring);
            r.Name = ResText(r.Root, true, UiKit.TextColor, TextAnchor.MiddleLeft);
            r.Role = ResText(r.Root, false, ResSub, TextAnchor.MiddleLeft);
            r.Kda = ResText(r.Root, false, ResKda, TextAnchor.MiddleCenter);
            r.Score = ResText(r.Root, true, UiKit.TextColor, TextAnchor.MiddleCenter);
            r.Mvp = Pic(r.Root, "Mvp", UiKit.Rounded, ResWin);
            r.MvpText = ResText(r.Mvp.rectTransform, true, ResMvpGoldInk, TextAnchor.MiddleCenter);
            r.MvpText.text = "MVP";
            UiKit.Stretch(r.MvpText.rectTransform, 0, 0, 0, -2);
            return r;
        }

        // a round portrait: the dark disc, the face in it, a coloured ring around it
        static RectTransform ResDisc(Transform parent, out RawImage pic, out Image ring)
        {
            var disc = Pic(parent, "Disc", UiKit.Circle, ResPicBack);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = UiKit.NewRect("Face", disc.rectTransform);
            UiKit.Stretch(face);
            pic = face.gameObject.AddComponent<RawImage>();
            pic.raycastTarget = false;
            pic.uvRect = new Rect(0.115f, 0f, 0.77f, 1f); // the 400x308 portraits: the square around the face
            ring = Pic(parent, "Ring", null, ResWin);
            return disc.rectTransform;
        }

        // Hf (Lilita) or Bold (the body font in bold); sizes are set by LayoutResult
        static Text ResText(Transform parent, bool heading, Color color, TextAnchor anchor)
        {
            var t = UiKit.Label(parent, "", 40, anchor, color);
            if (!heading) { t.font = UiKit.BodyFont; t.fontStyle = FontStyle.Bold; }
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ResBtn: a dark drop under it; PLAY AGAIN orange with a light top half, HOME navy with a pale edge
        static Button ResButton(string label, bool primary, System.Action onClick, out Image shadow, out Text text, out Image extra)
        {
            var root = UiKit.NewRect(label, resBody);
            shadow = Pic(root, "Drop", UiKit.Rounded, new Color(0, 0, 0, 0.4f));
            var face = Pic(root, "Face", UiKit.Rounded, primary ? ResOrange : ResNavy);
            UiKit.Stretch(face.rectTransform);
            face.raycastTarget = true;
            extra = primary ? Pic(face.rectTransform, "Shine", UiKit.Rounded, new Color(1, 1, 1, 0.2f)) : Pic(face.rectTransform, "Edge", null, ResNavyEdge);
            text = ResText(face.rectTransform, true, primary ? ResOrangeInk : Color.white, TextAnchor.MiddleCenter);
            text.text = label;
            UiKit.Stretch(text.rectTransform);
            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(primary ? SfxId.UiConfirm : SfxId.UiTap); onClick(); });
            root.gameObject.AddComponent<ButtonFx>();
            return b;
        }

        // ---- filling ----

        static void FillResult(MatchResult r, Match m)
        {
            resWon = r.Won;
            resTableMode = m != null && !r.Tutorial && m.Heroes.Count > 0;
            string title = r.Quit ? "MATCH LEFT" : r.Won ? "VICTORY" : "DEFEAT";
            resTitle.text = resTitleDrop.text = title;
            resTitle.color = r.Won ? ResWin : ResLoseTitle;
            Color edge = r.Won ? ResWin : ResLose;
            resCardEdge.color = edge;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(r.Seconds));
            string length = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            resSub.text = (r.Tutorial ? "Tutorial" : DifficultyNames[Mathf.Clamp((int)r.Difficulty, 0, DifficultyNames.Length - 1)] + " bots") + " · " + length
                + (r.BotsMoved ? " · next: " + DifficultyNames[Mathf.Clamp((int)BamSettings.LastDifficulty, 0, DifficultyNames.Length - 1)] + " bots" : "")
                + (r.Practice ? " · practice, no XP" : "");

            resTable.gameObject.SetActive(resTableMode);
            resSmall.gameObject.SetActive(!resTableMode);
            resAgain.gameObject.SetActive(!r.Tutorial || !r.Won);
            FillXp(r.Practice);
            if (resTableMode) FillScores(m);
            else
            {
                var info = HeroCatalog.Get(r.Hero);
                resBigPic.texture = Portrait(r.Hero);
                resBigPic.enabled = resBigPic.texture != null;
                resBigRing.color = edge;
                resBigName.text = info.Name;
                int assists = m != null && m.Player != null ? m.Player.Assists : 0;
                resBigValue[0].text = r.Kills.ToString();
                resBigValue[1].text = r.Deaths.ToString();
                resBigValue[2].text = assists.ToString();
            }
            LayoutResult();
        }

        // "+160 XP · +20 Rating  (first win today)", the level bar, "LEVEL UP! Level 4 · 40 / 200 XP"; hidden offline or for a
        // match that gives nothing (the tutorial, a match left, under 45 s) and after PRACTICE > NEW MAP, which never reaches
        // Progress: its LastGain would still be the previous match's, "LEVEL UP!" and "NEW HERO UNLOCKED" included (bug review
        // 2026-11-04 nr 51)
        static void FillXp(bool practice)
        {
            var g = OnlineHooks.LastGain != null && !practice ? OnlineHooks.LastGain() : default;
            bool on = g.Xp > 0;
            resXp.gameObject.SetActive(on); resLevel.gameObject.SetActive(on);
            resXpBack.gameObject.SetActive(on); resXpFill.gameObject.SetActive(on);
            resUnlock.gameObject.SetActive(on && g.Unlocked != null);
            if (!on) return;
            if (g.Unlocked != null) resUnlock.text = "NEW HERO UNLOCKED: " + g.Unlocked.ToUpperInvariant();
            bool up = g.Level > g.LevelBefore;
            resXp.text = "+" + g.Xp + " XP  ·  " + (g.RatingText ?? "+" + g.Rating + " Rating") + (g.FirstWin ? "  (first win today)" : "");
            resLevel.text = (up ? "LEVEL UP! " : "") + "Level " + g.Level + " · " + g.LevelText;
            resLevel.color = up ? ResLevelUp : ResSub;
            resXpPart = Mathf.Clamp01(g.LevelProgress);
        }

        static void FillScores(Match m)
        {
            int n = RateHeroes(m);
            int mine = m.Player != null ? m.Player.Team : Teams.Player;
            resHead[0].text = "YOUR TEAM"; resHead[1].text = "ENEMIES";
            int row = 0;
            for (int side = 0; side < 2; side++)
            {
                int team = side == 0 ? mine : Teams.Other(mine);
                resHead[side].color = side == 0 ? ResBlueHead : ResRedHead;
                resRowsPerTeam[side] = 0;
                for (int i = 0; i < n && row < resRows.Length; i++)
                {
                    var x = rated[i];
                    if (x.U.Team != team) continue;
                    var rr = resRows[row++];
                    resRowsPerTeam[side]++;
                    bool me = x.U.IsPlayer, gold = x.Mvp && team == m.Winner;
                    rr.Root.gameObject.SetActive(true);
                    rr.Mine.enabled = me;
                    rr.Pic.texture = Portrait(x.U.Hero);
                    rr.Pic.enabled = rr.Pic.texture != null;
                    rr.Ring.color = side == 0 ? ResBlueRing : ResRedRing;
                    rr.Name.text = HeroCatalog.Get(x.U.Hero).Name;
                    rr.Name.color = me ? ResMe : Color.white;
                    rr.Role.text = x.Role;
                    rr.Kda.text = x.U.Kills + "/" + x.U.Deaths + "/" + x.U.Assists;
                    rr.Score.text = x.Rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                    rr.Score.color = gold ? ResWin : x.Mvp ? ResSilverText : Color.white;
                    rr.Mvp.gameObject.SetActive(x.Mvp);
                    rr.Mvp.color = gold ? ResWin : ResSilver;
                    rr.MvpText.color = gold ? ResMvpGoldInk : ResMvpSilverInk;
                }
            }
            resRowCount = row;
            for (int i = row; i < resRows.Length; i++) resRows[i].Root.gameObject.SetActive(false);
        }

        // ---- layout, in the old W and H ----

        static void LayoutResult()
        {
            if (resBody == null) return;
            float W = resBody.rect.width, H = resBody.rect.height;
            if (W <= 0 || H <= 0) return;
            bool sc = resTableMode;
            float cw0 = W * 0.86f, cx0 = (W - cw0) / 2, top = sc ? H * 0.1f : H * 0.2f, ch = sc ? H * 0.62f : H * 0.5f;
            TopLeft(resCard, cx0, top, cw0, ch);
            float cr = W * 0.04f;
            Radius(resCardBack, cr);
            resCardEdge.sprite = HomeArt.RoundRing(W * 0.005f / cr);
            RingRadius(resCardEdge, cr);

            // the title with its drop, then "Normal bots · 4:12"
            Size(resTitle, W * 0.1f); Size(resTitleDrop, W * 0.1f); Size(resSub, W * 0.032f);
            Centre(resTitleDrop.rectTransform, cw0 / 2, H * 0.058f, cw0, H * 0.08f);
            Centre(resTitle.rectTransform, cw0 / 2, H * 0.055f, cw0, H * 0.08f);
            Centre(resSub.rectTransform, cw0 / 2, H * 0.1f, cw0, H * 0.04f);

            float ly = sc ? LayoutScores(W, H, cw0) + H * 0.035f : LayoutSmall(W, H, cw0);
            LayoutXp(W, H, cw0, ly);

            LayoutButton(resAgain, resAgainShadow, resAgainText, W * 0.1f, H * 0.74f, W * 0.8f, H * 0.065f);
            resAgainShine.rectTransform.anchorMin = new Vector2(0, 1); resAgainShine.rectTransform.anchorMax = new Vector2(1, 1);
            resAgainShine.rectTransform.pivot = new Vector2(0.5f, 1);
            resAgainShine.rectTransform.offsetMin = new Vector2(3, -3 - H * 0.065f * 0.42f);
            resAgainShine.rectTransform.offsetMax = new Vector2(-3, -3);
            Radius(resAgainShine, H * 0.065f * 0.25f);
            bool again = resAgain.gameObject.activeSelf;
            if (again) LayoutButton(resHome, resHomeShadow, resHomeText, W * 0.1f, H * 0.82f, W * 0.8f, H * 0.055f);
            else LayoutButton(resHome, resHomeShadow, resHomeText, W * 0.1f, H * 0.74f, W * 0.8f, H * 0.065f);
            float hr = (again ? H * 0.055f : H * 0.065f) * 0.3f;
            resHomeEdge.sprite = HomeArt.RoundRing(W * 0.004f / hr);
            UiKit.Stretch(resHomeEdge.rectTransform);
            RingRadius(resHomeEdge, hr);
        }

        static void LayoutXp(float W, float H, float w, float ly)
        {
            Size(resXp, W * 0.045f); Size(resLevel, W * 0.026f);
            // best fit from today's size down to 60 %; the box is 1.5 lines of the full size tall (Lilita's line is 1.14), so a
            // shorter line keeps its size and position and the longest one shrinks to ~72 % on one line (nr 21)
            resXp.resizeTextMaxSize = resXp.fontSize;
            resXp.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(resXp.fontSize * 0.6f));
            Centre(resXp.rectTransform, w / 2, ly, w * 0.94f, resXp.fontSize * 1.5f);
            float bx = w * 0.12f, bw = w * 0.76f, by = ly + H * 0.03f, bh = H * 0.012f;
            TopLeft(resXpBack.rectTransform, bx, by, bw, bh);
            TopLeft(resXpFill.rectTransform, bx, by, Mathf.Max(bh, bw * resXpPart), bh);
            Radius(resXpBack, bh / 2); Radius(resXpFill, bh / 2);
            Centre(resLevel.rectTransform, w / 2, by + H * 0.028f, w, H * 0.03f);
            Size(resUnlock, W * 0.03f);
            Centre(resUnlock.rectTransform, w / 2, by + H * 0.066f, w, H * 0.035f);
        }

        // returns where the table ends, measured from the card's top
        static float LayoutScores(float W, float H, float w)
        {
            float y0 = H * 0.125f, rh = H * 0.042f, hh = H * 0.03f, pad = w * 0.04f;
            float xName = w * 0.17f, xKda = w * 0.58f, xScore = w * 0.79f, xMvp = w * 0.92f;
            TopLeft(resTable, 0, y0, w, H * 0.5f);
            float y = 0;
            int row = 0;
            for (int side = 0; side < 2; side++)
            {
                Size(resHead[side], W * 0.028f); Size(resKdaHead[side], W * 0.024f); Size(resScoreHead[side], W * 0.024f);
                LeftMid(resHead[side].rectTransform, pad, y + hh / 2, w * 0.5f, hh);
                Centre(resKdaHead[side].rectTransform, xKda, y + hh / 2, w * 0.2f, hh);
                Centre(resScoreHead[side].rectTransform, xScore, y + hh / 2, w * 0.16f, hh);
                y += hh;
                for (int k = 0; k < resRowsPerTeam[side]; k++)
                {
                    var r = resRows[row++];
                    TopLeft(r.Root, 0, y, w, rh);
                    float my = rh / 2;
                    TopLeft(r.Mine.rectTransform, pad * 0.5f, 1, w - pad, rh - 2);
                    Radius(r.Mine, rh * 0.3f);
                    float rad = rh * 0.38f, px = pad + rad;
                    Centre(r.Disc, px, my, rad * 2, rad * 2);
                    Centre(r.Ring.rectTransform, px, my, rad * 2, rad * 2);
                    r.Ring.sprite = HomeArt.CircleRing(W * 0.004f / rad);
                    Size(r.Name, W * 0.034f); Size(r.Role, W * 0.022f); Size(r.Kda, W * 0.033f); Size(r.Score, W * 0.048f);
                    LeftMid(r.Name.rectTransform, xName, my - rh * 0.17f, w * 0.4f, rh * 0.6f);
                    LeftMid(r.Role.rectTransform, xName, my + rh * 0.24f, w * 0.4f, rh * 0.5f);
                    Centre(r.Kda.rectTransform, xKda, my, w * 0.2f, rh);
                    Centre(r.Score.rectTransform, xScore, my, w * 0.16f, rh);
                    float bw = w * 0.11f, bh = rh * 0.5f;
                    Centre(r.Mvp.rectTransform, xMvp, my, bw, bh);
                    Radius(r.Mvp, bh * 0.4f);
                    Size(r.MvpText, bh * 0.7f);
                    y += rh;
                }
                y += H * 0.012f;
            }
            return y0 + y;
        }

        // returns where the account XP line goes, measured from the card's top
        static float LayoutSmall(float W, float H, float w)
        {
            TopLeft(resSmall, 0, 0, w, H * 0.5f);
            float py = H * 0.14f, r = W * 0.09f, cx = w / 2;
            Centre(resBigDisc, cx, py + r, r * 2, r * 2);
            Centre(resBigRing.rectTransform, cx, py + r, r * 2, r * 2);
            resBigRing.sprite = HomeArt.CircleRing(W * 0.0075f / r);
            Size(resBigName, W * 0.05f);
            Centre(resBigName.rectTransform, cx, py + r * 2 + H * 0.025f, w, H * 0.05f);
            float sy = py + r * 2 + H * 0.065f, sw = w / 3;
            for (int i = 0; i < 3; i++)
            {
                float sx = sw * (i + 0.5f);
                Size(resBigValue[i], W * 0.07f); Size(resBigLabel[i], W * 0.027f);
                Centre(resBigValue[i].rectTransform, sx, sy, sw, H * 0.05f);
                Centre(resBigLabel[i].rectTransform, sx, sy + H * 0.03f, sw, H * 0.03f);
            }
            return sy + H * 0.075f;
        }

        static void LayoutButton(Button b, Image shadow, Text text, float x, float y, float w, float h)
        {
            var rt = (RectTransform)b.transform;
            TopLeft(rt, x, y, w, h);
            TopLeft(shadow.rectTransform, 0, 3, w, h);
            Radius(shadow, h * 0.3f);
            Radius((Image)b.targetGraphic, h * 0.3f);
            Size(text, h * 0.48f);
        }

        static void Size(Text t, float px) => t.fontSize = Mathf.Max(1, Mathf.RoundToInt(px));

        // centred on (x, y), y measured down from the parent's top
        static void Centre(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        // left edge at x, middle at y down from the parent's top
        static void LeftMid(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 0.5f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>The card fades in over 0.3 s and slides up a little; the buttons answer once it is in (HudV3 drawResult, ResultDown).</summary>
        sealed class ResultFx : MonoBehaviour
        {
            float shownAt;
            Vector2 laidOut;

            void OnEnable()
            {
                shownAt = Time.unscaledTime;
                Step();
            }

            // resBody is the safe-area child, which can resize (late Android insets) without this root changing, so look each frame
            void Update()
            {
                if (resBody != null)
                {
                    var size = resBody.rect.size;
                    if ((size - laidOut).sqrMagnitude >= 0.25f) { laidOut = size; LayoutResult(); }
                }
                Step();
            }

            void Step()
            {
                if (resFade == null || resCard == null) return;
                float k = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.3f);
                resFade.alpha = k;
                bool ready = k >= 1f;
                resAgain.interactable = resHome.interactable = ready;
                var p = resCard.anchoredPosition;
                float H = resBody.rect.height, top = (resTableMode ? H * 0.1f : H * 0.2f) + (1 - k) * H * 0.03f;
                if (Mathf.Abs(p.y + top) > 0.01f) { p.y = -top; resCard.anchoredPosition = p; }
            }
        }
    }
}
