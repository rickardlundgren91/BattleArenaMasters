# Data safety – answers for the Play Console form

> Not legal advice. These answers describe the build as written on 2026-10-25: offline game, no accounts, no ads, no
> purchases, and Unity Analytics (`com.unity.services.analytics` 6.x) that only starts after the player taps
> "Yes, share" (see `Assets/BAM/Analytics/BamAnalytics.cs`). If anything is added to the game (an SDK, online play,
> accounts, ads), these answers and `privacy-policy.md` must be updated before the next upload.

Play Console: **Policy and programs > App content > Data safety > Start**.

## Step 1: Overview
Read and click **Next**.

## Step 2: Data collection and security

| Question | Answer | Why |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | Optional analytics, when the player allows it. |
| Is all of the user data collected by your app encrypted in transit? | **Yes** | Unity Analytics sends over HTTPS. |
| Which of the following methods of account creation does your app support? | **My app does not allow users to create an account** | No accounts. |
| Do you provide a way for users to request that their data is deleted? | **Yes** | Turning the Settings switch off sends a deletion request; email support@battlearenamasters.com also works. Data deletion URL (if asked): https://battlearenamasters.com/privacy |

## Step 3: Data types

Tick exactly these four and nothing else:

| Category | Data type | Source |
|---|---|---|
| Location | **Approximate location** | Unity estimates country/region from the IP address. |
| App activity | **App interactions** | Our events: match start/end, tutorial step, session start/stop. |
| App info and performance | **Diagnostics** and **Other app performance data** | Device model, OS version, session length and similar data Unity adds. |
| Device or other IDs | **Device or other IDs** | Unity's random installation ID. |

Not collected (leave unticked): Personal info, Financial info, Health and fitness, Messages, Photos and videos,
Audio files, Files and docs, Calendar, Contacts, Web browsing, Precise location, Crash logs, Search history,
Installed apps, Other user-generated content, Other actions.

## Step 4: Data usage and handling (same answers for every data type above)

| Question | Answer |
|---|---|
| Is this data collected, shared, or both? | **Collected** (not shared: Unity acts as our service provider, which Google does not count as sharing) |
| Is this data processed ephemerally? | **No** |
| Is this data required for your app, or can users choose whether it's collected? | **Users can choose whether this data is collected** |
| Why is this user data collected? | **Analytics** (only this one) |

## Step 5: Preview
Check the summary says: data collected (Location, App activity, App info and performance, Device or other IDs),
no data shared, encrypted in transit, you can request that data be deleted. Click **Save**, then **Submit** if asked.

## Other App content answers (same page, other sections)

| Section | Answer |
|---|---|
| Privacy policy | https://battlearenamasters.com/privacy |
| Ads | **No, my app does not contain ads** |
| App access | **All functionality in my app is available without any access restrictions** |
| Advertising ID | **No** (the app does not use the advertising ID). The build thread must check that the merged manifest has no `com.google.android.gms.permission.AD_ID`. |
| Government apps | No |
| Financial features | My app doesn't provide any financial features |
| Health | My app does not have any health features |
| News apps | No |
| Content rating / Target audience | See `content-rating.md` |
