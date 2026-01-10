# TLDRkseid

**A Discord bot that summarizes your server's conversations using AI.**

*For those who always ask: "what did I miss?"*

---

## Add to Your Server

**[Invite TLDRkseid to your server](https://discord.com/api/oauth2/authorize?client_id=1360355381875970239&permissions=93184&scope=bot%20applications.commands)**

Or self-host your own instance - see [Self-Hosting Guide](#self-hosting) below.

---

## Quick Start

Once the bot is in your server:

1. **Get a summary:** `/tldr depth:standard`
2. **Filter by user:** `/tldr depth:brief user:@someone`
3. **Check costs:** `/cost`
4. **See all options:** `/tldr-help`

That's it! The bot responds privately (ephemeral) so it won't clutter your channels.

---

## Features

| Feature | Description |
|---------|-------------|
| **AI Summaries** | Powered by OpenAI GPT-3.5-turbo |
| **5 Depth Levels** | From quick skim to deep dive |
| **User Filtering** | Summarize a specific person's messages |
| **Thread Support** | Works in threads too |
| **Smart Caching** | Reuses summaries when messages haven't changed |
| **Rate Limiting** | Built-in spam protection |
| **Privacy First** | All responses are ephemeral (only you see them) |
| **Cost Tracking** | See API cost per summary and totals |

---

## Commands

### For Everyone

| Command | Description |
|---------|-------------|
| `/tldr depth:[level]` | Summarize recent messages |
| `/tldr depth:[level] user:@someone` | Summarize a specific user's messages |
| `/tldr-help` | Show available depth options |
| `/tldr-config` | View/set server's default depth |
| `/cost` | View API usage costs for your server |

### Summary Depth Levels

| Depth | Messages | Best For |
|-------|----------|----------|
| `recent` | ~100 | Just missed a few messages |
| `brief` | ~200 | Missed an hour or two |
| `standard` | ~300 | Daily catch-up (recommended) |
| `deep` | ~400 | Missed a busy day |
| `max` | ~500 | Deep dive (may be less focused) |

### For Server Admins

Admin commands use the `!admin` prefix and auto-delete after 10 seconds for privacy.

| Command | Description | Who Can Use |
|---------|-------------|-------------|
| `!admin add-superuser @user` | Assign the server's superuser (one-time setup) | Server Owner only |
| `!admin add @user` | Add an admin | Superuser only |
| `!admin remove @user` | Remove an admin | Superuser only |
| `!admin list` | List all admins | Superuser only |
| `!admin whoami` | Check your role | Anyone |
| `!admin refresh` | Re-sync slash commands | Admins |

---

## Required Permissions

When inviting the bot, these permissions are requested:

- **View Channels** - To see channels and read conversation history
- **Send Messages** - To send summaries
- **Read Message History** - To fetch older messages
- **Embed Links** - To send formatted summary embeds
- **Manage Messages** - To auto-delete admin commands

---

## Privacy & Data

- **No message storage** - Messages are fetched on-demand, not stored
- **Ephemeral responses** - Summaries are only visible to you
- **Minimal data collection** - We only store guild settings and admin roles
- **Rate limited** - Prevents abuse and controls API costs
- **OpenAI processing** - Messages are sent to OpenAI for summarization (not stored by us)

For full details, see our [Privacy Policy](PRIVACY.md) and [Terms of Service](TERMS-OF-SERVICE.md).

---

## Self-Hosting

Want to run your own instance? Here's how:

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Discord Bot Token](https://discord.com/developers/applications)
- [OpenAI API Key](https://platform.openai.com/api-keys)

### Setup

```bash
# Clone the repository
git clone https://github.com/mcarthey/Discord-TLDRkseid.git
cd Discord-TLDRkseid

# Restore dependencies
dotnet restore

# Create environment file
cp .env.example .env
# Edit .env with your tokens
```

### Environment Variables

Create a `.env` file in the project root:

```env
DISCORD_BOT_TOKEN=your-discord-bot-token
OPENAI_API_KEY=your-openai-api-key
DISCORD_DEV_GUILD_ID=optional-guild-id-for-testing
DATABASE_PATH=tldr.sqlite
```

### Run

```bash
# Development
cd TLDRkseid
dotnet run

# Production build
dotnet publish -c Release -o out
./out/TLDRkseid
```

### Docker

```bash
docker build -t tldrkseid .
docker run -d --env-file .env -v tldrdata:/data tldrkseid
```

---

## Health Checks

The bot exposes health check endpoints (useful for monitoring):

| Endpoint | Description |
|----------|-------------|
| `/health` | Full health status |
| `/health/live` | Liveness probe |
| `/health/ready` | Readiness probe |

---

## Support & Contributing

- **Issues:** [GitHub Issues](https://github.com/mcarthey/Discord-TLDRkseid/issues)
- **Discussions:** [GitHub Discussions](https://github.com/mcarthey/Discord-TLDRkseid/discussions)

PRs welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## Legal

- [Privacy Policy](PRIVACY.md)
- [Terms of Service](TERMS-OF-SERVICE.md)
- [License](LICENSE) (Apache 2.0)

---

*Built with [Discord.Net](https://github.com/discord-net/Discord.Net) and [OpenAI](https://openai.com). Darkseid jokes optional but encouraged.*
