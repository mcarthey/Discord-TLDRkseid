# Production Release Checklist — Owner Manual Steps

*Last updated: March 2026*

This document tracks every manual step **you** need to complete for the production release and public listing of TLDRkseid. Items are grouped by priority.

---

## BLOCKER — Must Complete Before Public Listing

### OpenAI Cost Controls
- [ ] **Set OpenAI spending hard limit** — Go to [OpenAI Dashboard → Settings → Limits](https://platform.openai.com/account/limits) and set a hard monthly cap (e.g., $50/month)
- [ ] **Enable email alerts** — Set soft limit alerts at $10, $25, $50 so you're warned before hitting the cap

### Discord Developer Portal
- [ ] **Set Privacy Policy URL** — In [Discord Developer Portal](https://discord.com/developers/applications) → your app → General Information → Privacy Policy URL → set to `https://github.com/mcarthey/Discord-TLDRkseid/blob/main/PRIVACY.md`
- [ ] **Set Terms of Service URL** — Same page → Terms of Service URL → set to `https://github.com/mcarthey/Discord-TLDRkseid/blob/main/TERMS-OF-SERVICE.md`
- [ ] **Verify Message Content intent is enabled** — Bot → Privileged Gateway Intents → Message Content Intent must be toggled ON

### Test New Features in Production
- [ ] **Test `/tldr since:1h`** — Verify time-based filtering works
- [ ] **Test `/tldr since:24h`** — Larger time window
- [ ] **Test `/invite`** — Verify invite link is correct
- [ ] **Test `/about`** — Verify server count and links display correctly
- [ ] **Test `/cost`** — Verify per-guild stats show (today's requests, all-time cost)
- [ ] **Test `$admin` commands** — `$admin add-superuser @user`, `$admin add @user`, `$admin list`, `$admin whoami`, `$admin transfer-superuser @user`, `$admin revoke-superuser`

### Railway Volume Persistence
- [ ] **Verify volume is mounted** — Railway Dashboard → your service → Settings → Volumes → confirm mount at `/data`
- [ ] **Verify DATABASE_PATH is set** — Variables tab → confirm `DATABASE_PATH=/data/tldr.sqlite`
- [ ] **Test persistence** — Redeploy and confirm data survives (check `/cost` shows previous stats)

---

## HIGH — Before Submitting to Bot Listing Sites

### Bot Identity
- [ ] **Create bot avatar/logo** — Upload in Discord Developer Portal → your app → General Information. Top.gg reviewers expect a custom avatar.
- [ ] **Set bot description** — Discord Developer Portal → your app → General Information → Description (use the short description from `docs/BOT-LISTING.md`)

### Support Infrastructure
- [ ] **Create a support Discord server** — Bot listing sites expect a support server link. Set up a simple server with channels: `#announcements`, `#support`, `#feature-requests`
- [ ] **Add support server invite link** — Once created, add the invite link to the `/about` command embed and `docs/BOT-LISTING.md`

### Submit to Bot Listing Sites
- [ ] **Submit to Top.gg** — Follow steps in `docs/BOT-LISTING.md` (review takes 1-2 weeks, bot must stay online)
- [ ] **Submit to discordbotlist.com** — Same descriptions, follow their submission process
- [ ] **Submit to discords.com** — Additional exposure

### Monitoring (set up before listing, traffic will increase)
- [ ] **Set up uptime monitoring** — Point [UptimeRobot](https://uptimerobot.com) (free) at your Railway health endpoint: `https://your-app.railway.app/health`
- [ ] **Check Railway logs** — After listing, monitor logs daily for the first week for unexpected errors

---

## MEDIUM — First Week After Listing

- [ ] **Monitor usage patterns** — Watch `/cost` stats and Railway logs for the first 48 hours
- [ ] **Respond to bot list reviews** — Top.gg reviewers may test your bot and leave feedback
- [ ] **Set up database backups** — Railway supports volume snapshots; configure periodic backups
- [ ] **Announce the bot** — Post in relevant Discord communities (bot development servers, etc.)

---

## LOW — Nice to Have / Future

- [ ] **Create landing page** — Simple GitHub Pages site explaining the bot
- [ ] **Prepare Discord verification application** — Required at 75+ servers. You'll need:
  - Privacy Policy URL ✅ (already have)
  - Terms of Service URL ✅ (already have)
  - Bot functionality documentation (use README)
  - Message Content intent justification: *"TLDRkseid reads message content only when users explicitly invoke the /tldr command. Messages are sent to OpenAI for summarization and are not stored."*
- [ ] **Plan Azure migration** — For when you're ready to move off Railway
- [ ] **Design premium tier** — If you want to monetize via Discord Premium Apps

---

## Already Completed (for reference)

These items were handled in code and don't need manual action:

- [x] Privacy Policy created (`PRIVACY.md`)
- [x] Terms of Service created (`TERMS-OF-SERVICE.md`)
- [x] Bot invite link in README
- [x] CONTRIBUTING.md created
- [x] Bot listing descriptions prepared (`docs/BOT-LISTING.md`)
- [x] GatewayIntents tightened to minimum required
- [x] Debug logging cleaned up for production
- [x] Configuration wired to appsettings.json
- [x] Time-based filtering added (`/tldr since:`)
- [x] `/invite` command added
- [x] `/about` command added
- [x] `/cost` enhanced with per-guild stats
- [x] `$admin` prefix updated in README
- [x] `transfer-superuser` and `revoke-superuser` documented
- [x] Health check endpoints (`/health`, `/health/live`, `/health/ready`)
- [x] CI/CD pipeline (GitHub Actions)
- [x] Apache 2.0 license
- [x] Dockerfile with multi-stage build
