# Contributing

## Prerequisites

- .NET SDK `10.0.x`

## Build and test

```bash
dotnet build Seek.slnx
dotnet test --solution Seek.slnx
```

## Local CLI usage

```bash
dotnet run --project src/Seek.Cli -- --help
```

## Guidelines

- Keep `Seek.Core` storage/UI agnostic.
- Keep hot paths allocation-conscious.
- Preserve NativeAOT compatibility.
- Prefer deterministic behavior and clear errors in CLI commands.

## Before submitting

- Add/update tests for behavior changes.
- Update `README.md` and `CHANGELOG.md` for user-visible CLI or semantic changes.
- Keep docs in `docs/` aligned with implementation details.
