# TLDRkseid TODO

Project roadmap and pending tasks.

---

## Critical Bugs - Fix Before Release

### Bug Fixes
- [ ] **Admin command crash** - `MentionedUsers.First()` can throw if list changes between check and access (`MessageCommandHandler.cs:141, 169`)
- [ ] **Cache TTL race condition** - TOCTOU bug in cache expiration check (`SummaryCacheService.cs:52-64`)
- [ ] **Cost tracking data loss** - File save not atomic; data loss on crash (`CostTrackerService.cs:19-31`)
- [ ] **Fire-and-forget tasks** - `DeleteAfterAsync` tasks may run after shutdown (`MessageCommandHandler.cs:86-87`)

### Error Handling
- [ ] Add timeout to OpenAI API calls (Discord interaction expires after 3s)
- [ ] Add timeout to message fetch operations
- [ ] Add retry logic for Discord API throttling
- [ ] Validate AI response length before creating embed (4096 char limit)
- [ ] Better error categorization for OpenAI failures (401, 429, 500, 503)

---

## High Priority - Before Public Release

### UX Improvements
- [ ] Add "thinking..." indicator during summarization
- [ ] Better error messages with specific missing permissions
- [ ] Consistent rate limit messages (explain cooldown vs burst)
- [ ] Visual distinction for cached vs fresh summaries (different embed colors)
- [ ] Show cache hit/miss status to users

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
| Critical Bugs | 4 | **FIX NOW** |
| Error Handling | 5 | High |
| UX Improvements | 5 | High |
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

1. Fix admin command null reference crash
2. Add "thinking..." indicator
3. Add `/cost` command
4. Better permission error messages
5. Visual distinction for cached summaries
6. Add timeout to API calls
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
