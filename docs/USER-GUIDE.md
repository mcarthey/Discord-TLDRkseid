# TLDRkseid User Guide

A complete guide for server administrators and users.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Using the Bot](#using-the-bot)
3. [Admin Setup](#admin-setup)
4. [Understanding Summaries](#understanding-summaries)
5. [Rate Limits](#rate-limits)
6. [Troubleshooting](#troubleshooting)
7. [FAQ](#faq)

---

## Getting Started

### Inviting the Bot

1. Click the invite link (provided by the bot owner or from the repository)
2. Select your server from the dropdown
3. Authorize the requested permissions
4. The bot will appear in your server's member list

### Required Permissions

The bot needs these permissions to function:

| Permission | Why It's Needed |
|------------|-----------------|
| Read Messages / View Channels | To see messages that need summarizing |
| Send Messages | To send summary responses |
| Read Message History | To fetch older messages for deeper summaries |
| Manage Messages | To auto-delete admin commands (keeps channels clean) |
| Use Application Commands | To register and respond to slash commands |

### First-Time Setup

After inviting the bot, the **server owner** should:

1. Go to any text channel
2. Type: `!admin add-superuser @YourName`
3. This assigns you as the superuser (can only be done once)

---

## Using the Bot

### Basic Usage

Get a summary of recent conversation:

```
/tldr depth:standard
```

The bot will respond with an AI-generated summary of the channel's recent messages.

### Depth Levels

Choose how far back to summarize:

| Depth | Messages | When to Use |
|-------|----------|-------------|
| `recent` | ~100 | Quick catch-up, just missed a few minutes |
| `brief` | ~200 | Missed an hour or two |
| `standard` | ~300 | **Recommended** - Daily catch-up |
| `deep` | ~400 | Missed most of the day |
| `max` | ~500 | Full deep-dive (may be less focused) |

**Example:**
```
/tldr depth:brief
```

### Filtering by User

Want to see what a specific person said?

```
/tldr depth:standard user:@username
```

This summarizes only messages from that user.

### Getting Help

Forgot the depth options? Use:

```
/tldr-help
```

---

## Admin Setup

### Role Hierarchy

TLDRkseid has a simple two-tier admin system:

```
Server Owner
    └── Superuser (1 per server)
            └── Admins (unlimited)
                    └── Regular Users
```

### Admin Commands

All admin commands:
- Start with `!admin`
- Auto-delete after 10 seconds
- Are visible only briefly in the channel

| Command | Who Can Use | Description |
|---------|-------------|-------------|
| `!admin add-superuser @user` | Server Owner | One-time setup to assign superuser |
| `!admin add @user` | Superuser | Add a new admin |
| `!admin remove @user` | Superuser | Remove an admin |
| `!admin list` | Superuser | Show all current admins |
| `!admin whoami` | Anyone | Check your own role |
| `!admin refresh` | Superuser/Admin | Re-sync slash commands |

### What Can Admins Do?

Currently, admins have these privileges:
- Bypass rate limits when using `/tldr`
- Use `!admin refresh` to re-sync commands

### Transferring Superuser

The superuser role cannot be transferred through commands. If you need to change the superuser, the bot operator would need to update the database directly.

---

## Understanding Summaries

### What Gets Summarized

- Text messages from users (not bots)
- Messages within the selected depth range
- The current channel only

### What's NOT Included

- Bot messages (filtered out)
- Embeds, attachments, reactions
- Messages from other channels
- Deleted messages (if already deleted)

### Summary Format

Summaries are formatted as 3-5 bullet points covering:
- Key topics discussed
- Notable quotes or insights
- Recurring themes

### Caching

The bot caches summaries to save API costs:
- Cache lasts 1 hour
- Cache invalidates when new messages arrive
- You'll see "Cached from [tier]" if using a cached summary

---

## Rate Limits

To prevent abuse and manage API costs, TLDRkseid has built-in rate limits:

### For Regular Users

| Limit | Duration | Description |
|-------|----------|-------------|
| Cooldown | 30 seconds | Wait between uncached requests |
| Burst | 3 requests per 10 seconds | Prevents rapid-fire requests |
| Cached Cooldown | 10 seconds | Even cached results have a short cooldown |

### For Admins

Admins bypass rate limits and can use the bot more frequently.

### If You Hit a Rate Limit

You'll see a message like:
- "Slow down! Try again in Xs."
- "You're requesting cached summaries too fast."

Just wait the indicated time and try again.

---

## Troubleshooting

### "This command only works in text channels"

**Cause:** You're trying to use `/tldr` in a DM or non-text channel.

**Fix:** Use the command in a regular text channel.

### "I don't have permission to post summaries in this channel"

**Cause:** The bot lacks Send Messages permission in that channel.

**Fix:** Ask a server admin to check the bot's channel permissions.

### Slash commands aren't showing up

**Cause:** Commands may not have synced yet.

**Fix:**
1. Wait a few minutes (Discord can be slow to propagate)
2. Try `!admin refresh` if you're an admin
3. Kick and re-invite the bot as a last resort

### "AI summarization failed"

**Cause:** OpenAI API issue (rate limit, outage, or error).

**Fix:** Wait a moment and try again. If persistent, the bot operator should check API status.

### Bot doesn't respond at all

**Possible causes:**
1. Bot is offline
2. Bot lacks View Channel permission
3. Bot lacks Read Message History permission

**Fix:** Check bot permissions or contact the bot operator.

---

## FAQ

### Is my data stored?

**No.** Messages are fetched on-demand from Discord's API, summarized, and the summary is returned to you. The bot only stores:
- Guild admin/superuser assignments
- API cost totals (aggregate, not per-user)

### Can others see my summaries?

**No.** All `/tldr` responses are ephemeral (only visible to you).

### How much does each summary cost?

The footer of each summary shows:
- Cost for that specific summary
- Total API spend across all summaries

Costs are typically fractions of a cent per summary.

### Can I use this in DMs?

Currently, TLDRkseid only works in server text channels.

### Why "TLDRkseid"?

It's a portmanteau of:
- **TL;DR** - "Too Long; Didn't Read"
- **Darkseid** - The DC Comics villain

Because summarizing conversations is serious business.

### How do I self-host?

See the [Self-Hosting section in the README](../README.md#self-hosting).

### How do I report a bug or request a feature?

Open an issue on [GitHub](https://github.com/mcarthey/Discord-TLDRkseid/issues).

---

## Need More Help?

- **GitHub Issues:** [Report bugs or request features](https://github.com/mcarthey/Discord-TLDRkseid/issues)
- **README:** [Quick reference](../README.md)

---

*TLDRkseid - Because catching up shouldn't take as long as being there.*
