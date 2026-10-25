# Battle Arena Masters – battlearenamasters.com

The landing page for the closed playtest. Everything the site shows is in `public/`.

- `public/index.html` – the page. The Google Play test link and the Discord invite are set at the bottom of the file (`LINKS`). While a link is empty, the Discord buttons are hidden and the test button shows "Test link coming soon".
- `public/assets/` – logo, hero portraits, screenshots and the arena background.
- `wrangler.jsonc` – lets Cloudflare Workers serve `public/` as a static site.
