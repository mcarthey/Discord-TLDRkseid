# Contributing to TLDRkseid

Thanks for your interest in contributing! Here's how to get started.

## Development Setup

1. **Prerequisites:** [.NET 10.0 SDK](https://dotnet.microsoft.com/download), a Discord bot token, and an OpenAI API key
2. **Clone:** `git clone https://github.com/mcarthey/Discord-TLDRkseid.git`
3. **Configure:** Copy `.env.example` to `.env` and fill in your tokens
4. **Restore:** `dotnet restore`
5. **Test:** `dotnet test`
6. **Run:** `cd TLDRkseid && dotnet run`

## Project Structure

```
TLDRkseid/
  Commands/     - Discord slash command modules
  Services/     - Business logic (AI, caching, rate limiting)
  Data/         - EF Core database context and entities
  Handlers/     - Message-based ($admin) command handlers
  Configuration/ - Strongly-typed settings classes
```

## Guidelines

- **Use dependency injection** for all services
- **Follow existing patterns** — look at how current commands/services work
- **Use NLog** (`ILogger<T>`) for logging, not `Console.WriteLine`
- **Handle errors gracefully** — users should get friendly messages, not exceptions
- **Ephemeral responses** — all bot responses should be ephemeral (only visible to the invoker)
- **Run tests** before submitting: `dotnet test`

## Pull Requests

1. Fork the repo and create a feature branch
2. Make your changes
3. Ensure `dotnet build` and `dotnet test` pass
4. Submit a PR with a clear description of what changed and why

## Areas Where Help Is Wanted

- **Tests** — TldrModule (command handler) has limited test coverage
- **Features** — See the [TODO](docs/TODO.md) for the roadmap
- **Documentation** — Improvements to docs and guides are always welcome

## Code of Conduct

Be respectful. This is a hobby project — constructive feedback is welcome, hostility is not.

## License

By contributing, you agree that your contributions will be licensed under the [Apache 2.0 License](LICENSE).
