# Privacy Policy for TLDRkseid Bot

**Last Updated:** January 2025

## Overview

TLDRkseid is a Discord bot that summarizes channel conversations using AI. This privacy policy explains what data the bot collects, how it's used, and your rights regarding that data.

## Data Collection

### Data We Collect

When you use the `/tldr` command, the bot temporarily processes:
- **Message content** from the specified channel/timeframe (to generate summaries)
- **Usernames** of message authors (included in summaries for context)
- **Channel names** (for context in summaries)
- **Guild (server) ID** (for configuration and rate limiting)
- **User IDs** (for admin/superuser permissions and rate limiting)

### Data We Store

The bot stores minimal data in a local SQLite database:
- **Guild settings** (admin/superuser user IDs, per-guild configuration)
- **Cost tracking data** (aggregate API usage costs, no personal data)
- **Rate limiting data** (temporary, to prevent abuse)

### Data We Do NOT Collect
- Direct messages
- Voice/video data
- Email addresses
- Payment information
- Data from channels the bot cannot access

## How Data Is Used

1. **Message Summarization**: Messages are sent to OpenAI's API to generate summaries. Messages are processed in real-time and not permanently stored by TLDRkseid.

2. **Admin Management**: User IDs are stored to manage bot permissions within your server.

3. **Rate Limiting**: Temporary tracking to prevent abuse and ensure fair usage.

4. **Cost Tracking**: Aggregate API costs are logged for operational purposes (no personal data).

## Third-Party Services

### OpenAI
Message content is sent to OpenAI's API for summarization. OpenAI's data handling is governed by their [Privacy Policy](https://openai.com/privacy) and [API Data Usage Policies](https://openai.com/policies/api-data-usage-policies).

Per OpenAI's API data usage policy, data sent via the API is:
- Not used to train their models
- Retained for up to 30 days for abuse monitoring
- Then deleted

## Data Retention

- **Message content**: Not permanently stored; processed in memory only
- **Summaries**: Cached temporarily (up to 1 hour) to reduce API costs
- **Guild settings**: Retained while the bot is in your server
- **Rate limiting data**: Automatically expires after the rate limit window

## Data Security

- All data is transmitted over encrypted connections (HTTPS/TLS)
- Database access is restricted to the bot application only
- No data is sold or shared with third parties (except OpenAI for summarization)

## Your Rights

### For Server Administrators
- Remove the bot from your server to delete all stored guild settings
- Use admin commands to manage permissions
- Contact the bot operator to request data deletion

### For Users
- Your messages are only processed when someone uses the `/tldr` command
- You can request information about data processing from your server administrator

## Children's Privacy

This bot is not intended for use by children under 13 years of age, consistent with Discord's Terms of Service.

## Changes to This Policy

We may update this privacy policy from time to time. Significant changes will be announced via the bot's support server or GitHub repository.

## Contact

For privacy questions or data requests:
- **GitHub**: [mcarthey/Discord-TLDRkseid](https://github.com/mcarthey/Discord-TLDRkseid)
- **Discord**: Contact the bot operator through your server

## GDPR Compliance (EU Users)

If you are in the European Union, you have additional rights under GDPR:
- Right to access your data
- Right to rectification
- Right to erasure ("right to be forgotten")
- Right to data portability

To exercise these rights, contact the bot operator using the information above.
