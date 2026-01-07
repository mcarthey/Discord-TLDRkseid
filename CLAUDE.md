# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## Project Overview

**TLDrkseid** is a Discord bot written in C# (.NET 8) that summarizes channel conversations using OpenAI's GPT-3.5-turbo. It features smart caching, rate limiting, admin management, and comprehensive logging.

## Build & Run Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the bot (requires .env file with tokens)
dotnet run

# Build for release
dotnet publish DiscordPA.csproj -c Release -o out
```

## Environment Variables

The bot requires a `.env` file in the project root with:
- `DISCORD_BOT_TOKEN` - Discord bot token (required)
- `OPENAI_API_KEY` - OpenAI API key (required)
- `DISCORD_DEV_GUILD_ID` - Development guild ID (optional)

## Architecture

- **Commands/** - Discord slash command modules (`TldrModule.cs` handles `/tldr`)
- **Services/** - Business logic (AI summarization, caching, cost tracking, spam blocking)
- **Data/** - EF Core database context and entity models
- **Handlers/** - Event handlers (`!admin` prefix commands)
- **Program.cs** - Entry point, loads environment
- **Startup.cs** - Dependency injection and Discord client setup

## Key Services

- `AiSummarizerService` - OpenAI integration with debounce protection
- `SummaryCacheService` - SHA256-based in-memory caching with tier trickle-down
- `GuildAccessService` - Database-backed admin/superuser management
- `SpamBlockerService` - Rate limiting per user/channel

## Database

Uses SQLite via Entity Framework Core. Database file: `tldr.sqlite`
- Migrations run automatically on startup
- Log entries stored in database via NLog

## Code Style

- Use dependency injection for all services
- Follow existing patterns for slash commands in `TldrModule.cs`
- Use NLog for logging with guild/channel/user context
- Handle Discord interactions with deferred responses for long operations

## Bash Command Guidelines

- **Do not use `cd` prefix** - The working directory is already the project root
- **Do not chain commands with `&&`** - This interferes with permission approvals and requires continual re-approval. Run commands separately instead.
- **Do not use `git -C`** - Permission approvals don't work well with this flag

```bash
# WRONG - don't do this
cd "E:\Documents\dev\Discord-TLDRkseid" && git status

# WRONG - don't chain with &&
git add . && git commit -m "message" && git push

# CORRECT - run commands separately
git status
git add .
git commit -m "message"
git push
```
