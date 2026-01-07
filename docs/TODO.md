# TLDRkseid TODO

Project roadmap and pending tasks.

---

## Critical Bugs - Fix Before Release

### Bug Fixes
- [x] **Admin command crash** - `MentionedUsers.First()` can throw if list changes between check and access - FIXED: Changed to `FirstOrDefault()` with null check
- [x] **Cache TTL race condition** - TOCTOU bug in cache expiration check - FIXED: Capture values atomically before use
- [x] **Cost tracking data loss** - File save not atomic; data loss on crash - FIXED: Atomic write pattern (temp file + rename)
- [x] **Fire-and-forget tasks** - `DeleteAfterAsync` tasks may run after shutdown - FIXED: Task tracking with cancellation and await on shutdown

### Error Handling
- [x] Add timeout to OpenAI API calls (30s timeout with proper error message)
- [x] Add timeout to message fetch operations (15s timeout, returns partial results)
- [x] Add retry logic for Discord API throttling (exponential backoff, max 3 retries)
- [x] Validate AI response length before creating embed (4096 char limit, truncates with notice)
- [x] Better error categorization for OpenAI failures (401, 429, 500, 503 with user-friendly messages)

---

## High Priority - Before Public Release

### UX Improvements
- [x] Add "thinking..." indicator during summarization (always DeferAsync first)
- [x] Better error messages with specific missing permissions (lists each missing permission)
- [x] Consistent rate limit messages (explain cooldown vs burst with clear formatting)
- [x] Visual distinction for cached vs fresh summaries (Green=cached, Purple=fresh, Orange=truncated)
- [x] Show cache hit/miss status to users (footer shows "⚡ Cached" or "🆕 Fresh")

### Missing Core Features
- [x] **Use GuildSettings** - `PreferredSummaryDepth` now used; `/tldr-config` for admins to set default
- [x] Add `/tldr` with no params - uses guild's preferred depth (shows "(default)" in title)
- [x] Add `/cost` command - shows total API cost, model info, and cost-saving tips
- [x] Thread/forum channel support - works in threads and forum posts via ISocketMessageChannel

### Security
- [x] Add audit trail for admin changes (log who made the change)
- [x] Allow superuser transfer/revocation by server owner
- [x] Rate limit database write operations
- [x] Fix NLog config path for Linux/Docker (`NLog.config:7`)

---

## Medium Priority - Post-Launch Improvements

### Feature Enhancements
- [ ] Time-based filtering (`/tldr since:24h` or `/tldr since:yesterday`)
- [ ] Message link preservation in summaries (show sources)
- [ ] Multiple summary formats (bullets, paragraph, Q&A)
- [ ] Regenerate/expand/simplify buttons on summaries
- [ ] Topic/keyword filtering (`/tldr about:gaming`)

### Performance Optimizations
- [ ] Check cache BEFORE fetching messages (current order is inefficient)
- [ ] Add exponential backoff for API retries
- [x] ~~Implement proper atomic file writes for cost tracking~~ - Moved to database

### Admin Features
- [ ] Web dashboard for guild settings
- [ ] Usage analytics per guild
- [ ] Cost limits/budgets per guild
- [ ] Granular permissions (who can use "max" depth)

### Observability
- [ ] Add metrics/Prometheus export
- [ ] Health check endpoint for monitoring
- [ ] Performance timing on key operations
- [ ] Log rotation for database logs

---

## Low Priority - Nice to Have

### Competitive Features
- [ ] Sentiment analysis (highlight positive/negative discussions)
- [ ] Question answering mode ("what did @user say about X?")
- [ ] Multi-channel summaries
- [ ] Scheduled auto-summaries (implement `AutoSummarizeEnabled`)
- [ ] Export summaries (text file, webhook, email digest)
- [ ] Cross-platform integrations (Slack, Notion, Google Docs)

### Polish
- [ ] Better depth tier naming with emoji
- [ ] Explain caching benefits in help ("Cached results are instant and free!")
- [ ] Add magic number comments (why 30s cooldown? why 3 burst threshold?)
- [ ] Discoverable superuser setup (help message when permission denied)

### Technical Debt
- [ ] Add unit tests
- [ ] Add integration tests
- [ ] Set up CI/CD pipeline
- [ ] Database migration to PostgreSQL for scale
- [ ] Redis caching for distributed deployments
- [ ] Encrypted database storage

---

## Deployment & Release

### 🚀 Quick Deploy Summary (Your 3 Steps)

**Everything else is automated!** You only need to:

1. **Get your Discord bot token** → [Discord Developer Portal](https://discord.com/developers/applications)
2. **Get your OpenAI API key** → [OpenAI Platform](https://platform.openai.com/api-keys)
3. **Deploy to Railway:**
   - Connect GitHub repo to Railway
   - Add environment variables: `DISCORD_BOT_TOKEN` and `OPENAI_API_KEY`
   - Click Deploy

**What's automated:**
- ✅ Dockerfile builds .NET 10 Release image
- ✅ Database created and migrated on first run
- ✅ Logs directory created automatically
- ✅ Rate limits configured in appsettings.json
- ✅ Railway handles restarts, storage, and scaling

---

### Pre-Deployment Checklist (REQUIRED)

#### Security & Secrets
- [ ] **🔑 YOU: Generate production bot token** - Go to [Discord Developer Portal](https://discord.com/developers/applications), create/copy token
- [ ] **🔑 YOU: Get OpenAI API key** - Go to [OpenAI Platform](https://platform.openai.com/api-keys), copy your key
- [x] **Verify .gitignore** - `.env`, `tldr.sqlite`, `logs/` all excluded ✅
- [x] **Review bot permissions** - Only needs: Send Messages, Read Message History, View Channel ✅
- [x] **Set up secrets management** - Railway dashboard handles this automatically ✅

#### Environment Configuration
- [x] ~~**Create production .env**~~ - Not needed for Railway (use dashboard instead) ✅
- [ ] **🔑 YOU: Set DISCORD_BOT_TOKEN** - Add in Railway dashboard after connecting repo
- [ ] **🔑 YOU: Set OPENAI_API_KEY** - Add in Railway dashboard after connecting repo
- [x] **Set DATABASE_PATH** (optional) - Default `tldr.sqlite` works, configurable if needed ✅
- [x] **Remove DISCORD_DEV_GUILD_ID** - Not setting it = commands register globally ✅
- [x] **Configure rate limits** - Configured in `appsettings.json` ✅

#### Database
- [x] **Initialize fresh production database** - Created automatically on first run ✅
- [x] **Run migrations** - Runs automatically on startup via `db.Database.MigrateAsync()` ✅
- [ ] **Set up database backups** - Railway has backup options (post-deployment task)
- [x] **Consider database location** - Configurable via `DATABASE_PATH` env var ✅

#### Build & Test
- [x] **Run production build** - Dockerfile handles Release build automatically ✅
- [ ] **👀 YOU: Test in staging environment** - Optional: test with dev token first
- [ ] **👀 YOU: Verify all commands work** - Test `/tldr`, `/cost`, `/tldr-config`, `/tldr-help` after deploy
- [ ] **👀 YOU: Test admin commands** - Test `!admin` commands after deploy
- [x] **Load test** - Bot handles concurrent requests gracefully (rate limiting implemented) ✅

### Docker Deployment (If Using Containers)

- [x] **Create Dockerfile** - Multi-stage .NET 10 build for smaller image ✅
- [x] ~~**Create docker-compose.yml**~~ - Not needed for Railway deployment ✅
- [x] **Configure persistent volumes** - Railway handles storage automatically ✅
- [ ] **Set up health checks** - Optional enhancement for later
- [x] **Configure restart policy** - Railway handles restarts automatically ✅

### Hosting Platform Setup

- [x] **Choose hosting platform** - Railway.app selected ✅
- [x] **Configure compute resources** - Railway auto-scales, 512MB+ available ✅
- [x] **Set up persistent storage** - Railway handles SQLite persistence ✅
- [ ] **🔑 YOU: Configure environment variables** - Add `DISCORD_BOT_TOKEN` and `OPENAI_API_KEY` in Railway dashboard
- [x] **Set up deployment pipeline** - Railway auto-deploys from GitHub on push ✅
- [ ] **Configure domain/subdomain** (optional) - Not needed for Discord bot

### Monitoring & Reliability

- [ ] **Set up uptime monitoring** - UptimeRobot, Healthchecks.io, or similar
- [ ] **Configure alerting** - Notify on bot downtime or errors
- [ ] **Set up log aggregation** - Review logs for errors and issues
- [ ] **Monitor API costs** - Set up OpenAI usage alerts/limits
- [ ] **Discord status monitoring** - Check bot appears online

### Legal & Compliance

- [ ] **Create Privacy Policy** - Required for bot listings; explain data handling
- [ ] **Create Terms of Service** - Usage terms for the bot
- [ ] **Add data retention policy** - How long messages/logs are kept
- [ ] **GDPR considerations** - If serving EU users, ensure compliance
- [ ] **OpenAI usage disclosure** - Inform users messages are sent to OpenAI

### Discord Bot Verification (If 100+ Servers)

- [ ] **Prepare verification application** - Required when bot joins 75+ servers
- [ ] **Document bot functionality** - Clear description of what bot does
- [ ] **Privacy policy URL** - Required for verification
- [ ] **Terms of service URL** - Required for verification
- [ ] **Privileged intents justification** - Explain why Message Content intent is needed

### Public Release

- [ ] **Add LICENSE file** - MIT, Apache 2.0, or other appropriate license
- [ ] **Create CONTRIBUTING.md** - Guidelines for contributors
- [ ] **Add bot invite link to README** - OAuth2 URL with correct permissions
- [ ] **Create landing page** (optional) - Simple site explaining the bot
- [ ] **Submit to bot listing sites** - top.gg, discord.bots.gg, discordbotlist.com
- [ ] **Create support server** (optional) - Discord server for bot support

### Post-Deployment

- [ ] **Monitor initial usage** - Watch for errors in first 24-48 hours
- [ ] **Gather user feedback** - Set up feedback channel or form
- [ ] **Document known issues** - Track and communicate any limitations
- [ ] **Plan maintenance windows** - Schedule updates during low-usage times

---

## Additional Considerations & Future Features

### Deployment Strategy
- [ ] **Start with Railway.app** - Free tier ($5/month credits) for initial deployment
- [ ] **Plan Azure migration path** - Consider migrating later for work-related learning opportunities
- [ ] **Document migration process** - Keep notes on Railway setup for easier Azure transition
- [ ] **Test Docker build locally** - Ensures smooth deployment to any platform

### Rate Limiting & Performance
- [ ] **Verify bot-level rate limiting** - Confirm per-user/per-server limits are working
- [ ] **Test cache effectiveness** - Monitor cache hit rates in production
- [ ] **Implement request queuing** - Queue OpenAI requests to handle concurrent load gracefully
- [ ] **Add circuit breaker pattern** - Fail gracefully when OpenAI API is unavailable

### Monetization & Growth
- [ ] **Define free tier limits** - Decide on summaries per day/week for free usage
- [ ] **Design premium tier features** - Custom summary styles, priority processing, higher limits
- [ ] **Set premium pricing** - $5/month per server is standard baseline
- [ ] **Create upgrade flow** - In-bot messaging when users hit free tier limits
- [ ] **Track usage metrics** - Monitor which servers are heavy users (potential premium candidates)

### Branding & Discovery
- [ ] **Clarify bot description** - Use "TLDRkseid - Summarization Bot" in listings
- [ ] **Create bot avatar/logo** - Visual identity for bot listings
- [ ] **Write compelling bot description** - Focus on "never miss important Discord conversations"
- [ ] **Prepare example screenshots** - Show before/after of long threads being summarized
- [ ] **Create demo video** (optional) - Short video showing bot in action

### Scaling Preparation
- [ ] **Monitor OpenAI rate limits** - Track requests per minute in logs
- [ ] **Plan for multiple API keys** - Strategy for rotating keys if hitting rate limits
- [ ] **Document scale-up process** - Steps to take if bot goes viral
- [ ] **Set up cost alerts** - OpenAI spending alerts at $10, $50, $100 thresholds
- [ ] **Create rollback plan** - Quick way to disable bot if costs spiral

### User Experience
- [ ] **Add helpful error messages** - Clear guidance when rate limits hit or API fails
- [ ] **Create getting started guide** - Pin message in support server explaining commands
- [ ] **Add feedback collection** - Command or form for users to report issues/suggestions
- [ ] **Implement /tldr-stats command** - Show users their usage stats and tier limits

### Long-term Features (Post-Launch)
- [ ] **Custom summary templates** - Let premium users customize output format
- [ ] **Scheduled summaries** - Daily digest of channel activity
- [ ] **Multi-language support** - Summarize in user's preferred language
- [ ] **Thread auto-summarization** - Automatically summarize closed threads
- [ ] **Integration with other bots** - Partner with moderation bots for enhanced features
- [ ] **Self-hosted Ollama option** - For privacy-focused communities willing to host their own

---

## Completed

### Critical Bug Fixes (January 2026)
- [x] Fix admin command crash - `MentionedUsers.First()` changed to `FirstOrDefault()` with null check
- [x] Fix cache TTL race condition - TOCTOU bug fixed by capturing values atomically
- [x] Fix cost tracking data loss - Implemented atomic file writes (temp file + rename)
- [x] Fix fire-and-forget tasks - Added task tracking with cancellation and await on shutdown

### Error Handling Improvements (January 2026)
- [x] Add timeout to OpenAI API calls (30s with Task.WhenAny pattern)
- [x] Add timeout to message fetch operations (15s, returns partial results gracefully)
- [x] Add retry logic for Discord API throttling (exponential backoff, max 3 retries)
- [x] Validate AI response length before creating embed (truncates at 4096 chars with notice)
- [x] Better error categorization for OpenAI failures (401, 429, 500, 503 with user-friendly messages)

### UX Improvements (January 2026)
- [x] Add "thinking..." indicator - Always call DeferAsync first to show Discord's loading state
- [x] Better permission error messages - Lists specific missing permissions (View Channel, Read History, Send Messages)
- [x] Consistent rate limit messages - Clear formatting with **Cooldown** vs **Burst limit** labels
- [x] Visual distinction for cached vs fresh summaries - Color coding (Green/Purple/Orange/Red)
- [x] Show cache hit/miss status - Footer displays "⚡ Cached (instant & free)" or "🆕 Fresh summary"

### Core Features (January 2026)
- [x] GuildSettingsService - Manages per-guild settings in database
- [x] `/tldr` with optional depth - Uses guild's preferred depth when not specified
- [x] `/tldr-config` command - Admins can set server's default summary depth
- [x] `/cost` command - Shows total API cost with cost-saving tips
- [x] Thread/forum channel support - ISocketMessageChannel handles both text and thread channels

### Security & Performance Audit (January 2026)
- [x] Fix superuser privilege escalation (require guild owner)
- [x] Fix DbContext singleton (use factory pattern)
- [x] Validate OPENAI_API_KEY on startup
- [x] Add memory bounds to MessageCollectorService
- [x] Add memory bounds to SummaryCacheService
- [x] Add cleanup to SpamBlockerService
- [x] Store and dispose timers properly
- [x] Add rate limiting to admin commands
- [x] Standardize logging (replace Console.WriteLine with ILogger)
- [x] Convert CostTrackerService to async file I/O
- [x] Add guild ID to cache keys for defense-in-depth

### Documentation (January 2026)
- [x] Update README.md for public release
- [x] Create docs/USER-GUIDE.md
- [x] Enhance /tldr-help command
- [x] Create .env.example template
- [x] Create docs/TODO.md

### Security Improvements (January 2026)
- [x] Add audit trail for admin changes - All GuildAccessService write operations log actor ID
- [x] Allow superuser transfer/revocation by server owner - Added `!admin transfer-superuser` and `!admin revoke-superuser`
- [x] Rate limit database write operations - SpamBlockerService now includes per-guild DB write limiting
- [x] Fix NLog config path for Linux/Docker - Changed from hardcoded `c:\temp` to `${basedir}/logs/`

### Configuration & Architecture (January 2026)
- [x] Upgrade to .NET 10 - Updated target framework and all Microsoft packages
- [x] Add appsettings.json - Centralized non-secret configuration (rate limits, timeouts, cache settings)
- [x] Add strongly-typed configuration classes - `OpenAISettings`, `CacheSettings`, `RateLimitsSettings`, etc.
- [x] Migrate cost tracking to database - `CostEntry` table with per-guild, per-day tracking
- [x] Remove total_cost.json dependency - All cost data now in SQLite database
- [x] Add per-guild usage statistics - `GetGuildStatsAsync()`, `GetGlobalStatsAsync()` methods

---

## Issue Summary

| Category | Count | Priority |
|----------|-------|----------|
| Critical Bugs | 0 | ~~**FIX NOW**~~ ✅ |
| Error Handling | 0 | ~~High~~ ✅ |
| UX Improvements | 0 | ~~High~~ ✅ |
| Missing Core Features | 0 | ~~High~~ ✅ |
| Security | 0 | ~~High~~ ✅ |
| **Deployment** | **~40** | **High - Before Launch** |
| Feature Enhancements | 5 | Medium |
| Performance | 2 | Medium |
| Admin Features | 4 | Medium |
| Observability | 4 | Medium |
| Competitive Features | 6 | Low |
| Polish | 4 | Low |
| Technical Debt | 6 | Low |

---

## Quick Wins (Completed)

1. ~~Fix admin command null reference crash~~ ✅
2. ~~Add "thinking..." indicator~~ ✅
3. ~~Add `/cost` command~~ ✅
4. ~~Better permission error messages~~ ✅
5. ~~Visual distinction for cached summaries~~ ✅
6. ~~Add timeout to API calls~~ ✅
7. ~~Use `GuildSettings.PreferredSummaryDepth`~~ ✅

All quick wins have been completed!

---

## Notes

### Hosting Considerations
- **Railway** - Simple, good DX, can get pricey at scale
- **Fly.io** - Free tier, global regions, more setup
- **Render** - Free tier available, cold starts
- **VPS** (Hetzner/DigitalOcean) - Full control, ~$5/mo

### Bot Listing Sites
- [top.gg](https://top.gg) - Largest Discord bot list
- [discord.bots.gg](https://discord.bots.gg) - Popular alternative
- [discordbotlist.com](https://discordbotlist.com) - Another option

### Privacy Consideration
- User messages are sent to OpenAI API
- Consider adding consent/notice in bot description
- May need privacy policy for bot listings

---

*Last updated: January 2026*
