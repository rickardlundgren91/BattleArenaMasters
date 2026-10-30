# Battle Arena Masters – battlearenamasters.com

The landing page for the closed playtest. Everything the site shows is in `public/`.

- `public/index.html` – the page. The Google Play test link, the Google Group for testers and the Discord invite are set at the bottom of the file (`LINKS`). While a link is empty, the Discord buttons are hidden and the test button shows "Test link coming soon".
- `public/assets/` – logo, hero portraits, screenshots and the arena background.
- `public/version.json` – the newest Android version code on Google Play (`{"latest": 12}`). The game reads it at start; a player whose app has a lower code gets a "New version" dialog with an UPDATE button to Google Play. Raise the number only once that build is live on Google Play for the testers, or players are sent to an update that is not there yet.
- `wrangler.jsonc` – lets Cloudflare Workers serve `public/` as a static site.
