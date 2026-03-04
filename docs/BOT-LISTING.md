# Bot Listing Descriptions

Ready-to-use descriptions for bot listing sites.

---

## Short Description (200 chars)

> AI-powered conversation summaries with time filters, smart caching, and per-user filtering. Never ask "what did I miss?" again. Private, fast, and free to use.

## Top.gg / Discordbotlist.com Description

### TLDRkseid — AI Conversation Summaries for Discord

**Never miss what matters.** TLDRkseid uses AI to summarize your Discord conversations so you can catch up in seconds instead of scrolling for hours.

#### What Makes TLDRkseid Different

- **Time-based summaries** — `/tldr since:1h`, `/tldr since:24h`, `/tldr since:7d`. Summarize exactly the time window you missed.
- **Smart caching** — If the conversation hasn't changed, cached summaries are instant and free. No wasted API calls.
- **Per-user filtering** — Want to know what one person said? `/tldr user:@someone`
- **5 depth levels** — From a quick 100-message skim to a 500-message deep dive
- **100% private** — Every response is ephemeral (only you see it). No messages are stored.
- **Per-server cost tracking** — `/cost` shows exactly how much your server has used

#### Commands

| Command | What It Does |
|---------|-------------|
| `/tldr` | Summarize using your server's default depth |
| `/tldr depth:brief` | Summarize ~200 recent messages |
| `/tldr since:24h` | Summarize the last 24 hours |
| `/tldr user:@someone` | Summarize one person's messages |
| `/cost` | View API usage stats for your server |
| `/invite` | Get the bot invite link |
| `/about` | Bot info and links |
| `/tldr-help` | Full command reference |

#### Admin Features

Server owners and admins can:
- Set a default summary depth for the server
- Manage bot admin roles with `$admin` commands
- All admin commands auto-delete for privacy

#### Privacy & Security

- Messages are fetched on-demand, never stored
- Summaries are ephemeral (only the requester sees them)
- Rate limiting prevents abuse
- OpenAI API data is not used for training ([OpenAI API policy](https://openai.com/policies/api-data-usage-policies))

#### Links

- [GitHub](https://github.com/mcarthey/Discord-TLDRkseid) (open source, Apache 2.0)
- [Privacy Policy](https://github.com/mcarthey/Discord-TLDRkseid/blob/main/PRIVACY.md)
- [Terms of Service](https://github.com/mcarthey/Discord-TLDRkseid/blob/main/TERMS-OF-SERVICE.md)

---

## Tags/Categories for Listings

**Primary category:** Utility
**Tags:** ai, summary, tldr, summarize, catch-up, openai, gpt, conversations, text, utility

---

## Listing Checklist

Before submitting to any bot list:

- [ ] Bot is online and responding to commands
- [ ] `/tldr`, `/cost`, `/invite`, `/about`, `/tldr-help` all work
- [ ] Bot has an avatar/profile picture set
- [ ] Privacy Policy URL is set in Discord Developer Portal
- [ ] Terms of Service URL is set in Discord Developer Portal
- [ ] Support server exists (or GitHub Issues link is available)
- [ ] Description is 200+ characters (Top.gg requirement)

### Top.gg Submission

1. Go to https://top.gg/bot/submit
2. Enter bot ID: `1360355381875970239`
3. Use the short description above
4. Use the long description above (supports markdown)
5. Set prefix to `/` (slash commands)
6. Add tags: ai, utility, summary
7. Link Privacy Policy and ToS from GitHub

### Discordbotlist.com Submission

1. Go to https://discordbotlist.com/bots/new
2. Similar process — use the descriptions above
3. They support markdown in descriptions
