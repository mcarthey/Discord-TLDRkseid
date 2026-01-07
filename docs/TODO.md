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
- [ ] **Use GuildSettings** - `PreferredSummaryDepth` and `AutoSummarizeEnabled` exist but are never used
- [ ] Add `/tldr` with no params - use guild/user default depth
- [ ] Add `/cost` command - show personal/guild usage statistics
- [ ] Thread/forum channel support

### Security
- [ ] Add audit trail for admin changes (log who made the change)
- [ ] Allow superuser transfer/revocation by server owner
- [ ] Rate limit database write operations
- [ ] Fix NLog config path for Linux/Docker (`NLog.config:7`)

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
- [ ] Implement proper atomic file writes for cost tracking

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

- [ ] Create landing page for public bot
- [ ] Choose and configure hosting platform
- [ ] Set up production environment variables
- [ ] Configure custom domain (optional)
- [ ] Submit to bot listing sites (top.gg, discord.bots.gg)
- [ ] Add LICENSE file
- [ ] Create CONTRIBUTING.md
- [ ] Add bot invite link to README

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

---

## Issue Summary

| Category | Count | Priority |
|----------|-------|----------|
| Critical Bugs | 0 | ~~**FIX NOW**~~ ✅ |
| Error Handling | 0 | ~~High~~ ✅ |
| UX Improvements | 0 | ~~High~~ ✅ |
| Missing Core Features | 4 | High |
| Security | 4 | High |
| Feature Enhancements | 5 | Medium |
| Performance | 3 | Medium |
| Admin Features | 4 | Medium |
| Observability | 4 | Medium |
| Competitive Features | 6 | Low |
| Polish | 4 | Low |
| Technical Debt | 6 | Low |

---

## Quick Wins (Easy + High Impact)

1. ~~Fix admin command null reference crash~~ ✅
2. Add "thinking..." indicator
3. Add `/cost` command
4. Better permission error messages
5. Visual distinction for cached summaries
6. ~~Add timeout to API calls~~ ✅
7. Use `GuildSettings.PreferredSummaryDepth`

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
