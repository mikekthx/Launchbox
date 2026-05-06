# Contributing to Launchbox

## Prerequisites

- Windows 10 or 11 (WinUI 3 requires Windows)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 (17.x) with the **Windows App SDK** workload, or the `dotnet` CLI

## Building and testing

```bash
# Build (Debug, x64)
dotnet build Launchbox.csproj -p:Platform=x64

# Run unit tests
dotnet test Launchbox.Tests/Launchbox.Tests.csproj

# Format code (required before committing)
dotnet format Launchbox.sln
```

CI enforces `dotnet format --verify-no-changes` on every push; run `dotnet format Launchbox.sln` locally first to avoid a failed check.

## Pull request process

1. Branch off `main` with a descriptive name.
2. All CI checks must pass: build, unit tests, CodeQL, and ≥ 80% line coverage.
3. At least one CODEOWNER review is required before merge.
4. Signed commits are strongly preferred (`git config commit.gpgsign true`).

## Code style

- 4-space indentation for C#, 2-space for XAML/XML — enforced by `.editorconfig`.
- Nullable reference types are enabled; use the `?` suffix.
- Follow the naming conventions and patterns documented in [`CLAUDE.md`](../CLAUDE.md).

## Adding tests

- New testable classes must be file-linked in `Launchbox.Tests/Launchbox.Tests.csproj` (see the existing `<Compile Include>` entries).
- Use the existing mock classes (`MockFileSystem`, `MockSettingsStore`, etc.) where possible.
- The 80% coverage threshold is enforced by CI and will block merge if not met.

## Security

Do not open public issues for security vulnerabilities. See [SECURITY.md](SECURITY.md) for the private reporting process.

## Commit messages

Use imperative mood in the present tense ("Add feature", not "Added feature"). Reference issue numbers where applicable: `Fixes #123`.
