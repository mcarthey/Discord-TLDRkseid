# TLDrkseid  
**A smart Discord bot that summarizes your server’s conversations using OpenAI.**  
Built for clarity, built for teams, built for those who always ask: *“what did I miss?”*

---

## Features

- **/tldr [depth]**: Summarize recent messages (depths: `recent`, `brief`, `standard`, `deep`, `max`)
- **Optional user filtering**: Focus summaries on a specific user
- **Smart caching**: Reuses summaries when possible
- **Token cost tracking**: Shows OpenAI API usage per summary
- **Admin controls**: Superuser/admin role-based access control
- **Ephemeral replies**: Clean UI without clutter
- **BuyMeACoffee integration**: Optional button for donations

---

## Setup (Developer Local Build)

### 1. Clone and restore
```bash
git clone https://github.com/mcarthey/Discord-TLDRkseid.git
cd Discord-TLDRkseid
dotnet restore
```

### 2. Add your environment variables

Create a `.env` file (or set these via your IDE/debug config):

```
DISCORD_BOT_TOKEN=your-bot-token-here
OPENAI_API_KEY=your-openai-key-here
DISCORD_DEV_GUILD_ID=your-test-guild-id (optional, for slash command testing)
```

### 3. Run the bot

```bash
dotnet run
```

---

## Admin Setup

Once the bot is invited and running:

1. Use `!admin add-superuser @you` to set the first superuser (only works once)
2. Use `!admin add @user` to assign admins
3. Superuser/admins can run:  
   `!admin remove @user`  
   `!admin list`  
   `!admin whoami`  
   `!admin refresh` (to manually re-sync slash commands)

---

## Permissions Required

When inviting the bot, ensure it has:

- `Read Messages`
- `Send Messages`
- `Read Message History`
- `Manage Messages` *(for auto-deleting admin commands)*
- `Use Application Commands`

You can generate a proper invite link with [Discord’s OAuth2 URL Generator](https://discord.com/developers/applications).

---

## Summary Depth Levels

| Depth     | Messages | Use case                            |
|-----------|----------|-------------------------------------|
| `recent`  | ~100     | Just missed a few posts             |
| `brief`   | ~200     | Missed an hour or so                |
| `standard`| ~300     | ✅ Recommended daily summary         |
| `deep`    | ~400     | Skim a high-traffic period          |
| `max`     | ~500     | ⚠️ Broad, may dilute the summary     |

---

## Contributions

Open to feedback, suggestions, and improvements.  
PRs welcome—especially those that improve moderation tools or OpenAI efficiency!
