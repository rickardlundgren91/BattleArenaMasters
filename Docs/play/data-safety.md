# Data safety – answers for the Play Console form (version 0.2, online)

> Not legal advice. These answers describe 0.2 (version code 6 and later): optional sign-in with Google Play Games,
> Unity Gaming Services as game server (Authentication, Cloud Code, Cloud Save, Leaderboards, Friends, Lobby, Relay),
> online matches, friends, invites, reports, in-game account deletion, and Unity Analytics that only starts after the
> player taps "Yes, share". No ads, no purchases, no chat. The 0.1 answers are in git history. Swedish click guide:
> `play-console-sv.md` step 7, "NYTT för 0.2".

Play Console: **Policy and programs > App content > Data safety > Manage / Edit**.

## Step 2: Data collection and security

| Question | Answer | Why |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | Analytics (with consent) and the optional online account. |
| Is all of the user data collected by your app encrypted in transit? | **Yes** | Unity Gaming Services and Google use HTTPS; match traffic goes over encrypted Relay (DTLS). |
| Which methods of account creation does your app support? | **OAuth** only | Sign in with Google Play Games. No username or password. |
| Delete account URL | **https://battlearenamasters.com/delete-account** | Google requires a web page for this (site PR #12). |
| Can users request that some or all data is deleted without deleting the account? | **Yes** | Settings > Share usage data off sends a deletion request; email support@battlearenamasters.com. |

## Step 3: Data types (tick exactly these)

| Category | Data type | What it is in BAM |
|---|---|---|
| Location | **Approximate location** | Unity Analytics estimates country/region from the IP address. |
| Personal info | **Name** | The player name the player chooses (public nickname). |
| Personal info | **User IDs** | Unity player ID and the linked Google Play Games player ID. |
| App activity | **App interactions** | Analytics events and match results sent to the game server. |
| App activity | **Other actions** | Friends, friend requests, blocks, invites, reports. |
| App info and performance | **Diagnostics** and **Other app performance data** | Device model, OS version, session length (Unity Analytics). |
| Device or other IDs | **Device or other IDs** | Unity's random installation ID. |

Not collected: email address, phone, address, contacts (in-game friends are not phone contacts), messages (no chat),
photos, audio, files, calendar, precise location, financial info, health, web browsing, crash logs, search history,
installed apps, other user-generated content.

## Step 4: Usage and handling

Every type: **Collected** (not shared: Unity and Google are service providers; other players seeing a name is part of a
feature the user turned on), **not processed ephemerally**, **Users can choose whether this data is collected** (the
whole game works offline without signing in and without analytics).

| Data type | Why collected (tick) |
|---|---|
| Approximate location | Analytics |
| Name | App functionality, Account management |
| User IDs | App functionality, Account management, Fraud prevention, security, and compliance |
| App interactions | App functionality, Analytics, Fraud prevention, security, and compliance |
| Other actions | App functionality, Fraud prevention, security, and compliance |
| Diagnostics | Analytics |
| Other app performance data | Analytics |
| Device or other IDs | Analytics |

## Step 5: Preview
The summary should say: data collected (Location, Personal info, App activity, App info and performance, Device or
other IDs), no data shared, encrypted in transit, you can request that data be deleted. **Save**.

## Other App content answers

| Section | Answer |
|---|---|
| Privacy policy | https://battlearenamasters.com/privacy |
| Ads | No |
| App access | **All or some functionality is restricted** + instructions (see `play-console-sv.md` step 7) |
| Advertising ID | No |
| Content rating | Redo for 0.2: users interact = Yes (see `content-rating.md`) |
| Target audience | Unchanged: 13-15, 16-17, 18+ |
