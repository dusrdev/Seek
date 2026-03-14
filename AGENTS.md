# Agent Notes For `Seek`

## Non-negotiables

- Always read the latest version of every file you reference before changing it.
- Do not simplify production code just to make tests easier.
- Prefer runtime behavior and search performance over test convenience.
- Keep the project NativeAOT-friendly by default.
- Keep CLI UX based on `PrettyConsole` and argument handling based on `ConsoleAppFramework`.
- Treat `AGENTS.md` as architecture guidance, not as a substitute for reading the current source.

## Task behavior

- If a requested change needs unknown external source material, ask for it.
- If public command behavior, install flow, or package semantics change, ask whether to update `README.md` and `CHANGELOG.md`.
- When discussing external dependencies, verify behavior against the exact version in use.

## Current architecture

- `src/Seek.Cli`
  The executable entry point and command surface.
- `src/Seek.Core`
  The filesystem traversal engine, path matchers, and match-section model.
- `tests/Seek.Core.Tests`
  Integration-style search tests plus CLI/package consistency checks.
- `tests/Seek.Cli.Tests`
  CLI command-surface tests that call `Commands.SearchAsync` directly with redirected `PrettyConsole.ConsoleContext` writers.

## CLI contract

- The CLI exposes a single default command wired in `Program.cs` via `app.Add("", Commands.SearchAsync)`.
- The positional argument is the search query. There are no named subcommands.
- Current arguments and defaults from `Commands.SearchAsync`:
  - `query`: required positional argument.
  - `regex`: defaults to `false`.
  - `caseSensitive`: defaults to `false`.
  - `plain`: defaults to `false`.
  - `null`: defaults to `false`.
  - `hidden`: defaults to `false`.
  - `system`: defaults to `false`.
  - `files`: defaults to `false`.
  - `directories`: defaults to `false`.
  - `root`: defaults to `"."`.
  - `highlightColor`: defaults to `ConsoleColor.Green`.
- Short aliases currently exposed by the command surface:
  - `-r` => `--regex`
  - `-p` => `--plain`
  - `-h` => `--hidden`
  - `-s` => `--system`
  - `-f` => `--files`
  - `-d` => `--directories`
  - `-c` => `--highlight-color`
- If `plain` is `true`, the CLI writes the full path directly and does not emit PrettyConsole color/escape sequences for match sections.
- If `null` is `true`, the CLI emits plain NUL-terminated paths and bypasses highlight rendering.
- If both `files` and `directories` are `false`, both result kinds are emitted. If both are `true`, the current behavior is also to emit both result kinds.
- Worker count is not user-configurable today. `FileSystemSearch` computes it as `Math.Max(1, Environment.ProcessorCount - 1)`.
- Results are rendered to standard output through `PrettyConsole` when highlighting is enabled.
- Global exception handling preserves validation and argument parse failures, prints cancellations as a non-error, and prints unexpected exception messages in red while setting exit code `1`.

## Search engine behavior

- `FileSystemSearch` performs direct filesystem traversal. There is no persistent index and no background service.
- `FileSystemSearch` owns matcher selection from `query`, `useRegex`, and `caseSensitive`; callers do not pass an `IMatcher` instance.
- Root paths are normalized with `Path.GetFullPath`. Trailing separators are trimmed except for filesystem roots.
- If the root path does not exist, search throws `DirectoryNotFoundException`.
- Traversal uses `FileSystemEnumerable<T>` with:
  - `IgnoreInaccessible = true`
  - `RecurseSubdirectories = false`
  - `ReturnSpecialDirectories = false`
  - `AttributesToSkip = ReparsePoint | Hidden | System` by default
- Recursion is implemented manually through a channel-backed work queue instead of built-in recursive enumeration.
- Reparse points are skipped by default through `AttributesToSkip`.
- Hidden and system entries are skipped by default, but can be included by clearing those bits from `AttributesToSkip`.
- Both directories and files can be emitted as matches.
- Directory matching is tested against the full normalized directory path before enumerating its children.
- File matching is performed against the full path assembled from the directory span and file-name span.

## Algorithm notes

- Seek is optimized around a parallel producer-consumer traversal model.
- A channel of pending directories feeds a fixed-size worker pool.
- Each worker:
  - checks whether the directory path itself matches
  - enumerates immediate children with `FileSystemEnumerable<T>`
  - enqueues matching subdirectories
  - evaluates files inline and emits matches immediately
- Completion is coordinated by an atomic pending-directory counter instead of recursive task trees.
- The implementation is intentionally allocation-conscious in hot paths:
  - file full paths are assembled into a stackalloc buffer before materializing a string only for actual matches
- Do not replace this traversal with `Directory.GetFiles`, `Directory.EnumerateFiles` recursion, LINQ-heavy pipelines, or abstractions that hide enumeration cost without a clear measured win.

## Matcher behavior

- `ContainsMatcher`
  - uses `ReadOnlySpan<char>.IndexOf`
  - uses `StringComparison.OrdinalIgnoreCase` by default
  - switches to `StringComparison.Ordinal` for case-sensitive mode
  - records every matching slice and the non-matching gaps between them
- `RegexMatcher`
  - uses `RegexOptions.Compiled`
  - uses `RegexOptions.CultureInvariant`
  - uses `RegexOptions.NonBacktracking`
  - adds `RegexOptions.IgnoreCase` unless case-sensitive mode is requested
  - emits absolute path slices for both matching and non-matching segments
- Highlight rendering depends on `Sections` containing absolute offsets into the full displayed path. Do not change matcher output shape casually.

## Packaging and versioning

- `src/Seek.Cli/Seek.Cli.csproj` is the source of package metadata for the tool package.
- The package is packed as a .NET tool via `PackAsTool=true`.
- Optional strong-name signing for the CLI assembly is enabled by passing `StrongNameKeyPath` at build or pack time.
- Package identity is `Seek`.
- The package embeds:
  - the repository `README.md` as package readme
  - `assets/seek-icon.png` as package icon, packed to `seek-icon.png`
- `DotnetToolSettings.xml` is generated into the `.nupkg` by the SDK during packing.
- `ConsoleApp.Version` in `src/Seek.Cli/Program.cs` is expected to match the project `<Version>` in `src/Seek.Cli/Seek.Cli.csproj`.
- The release workflow materializes a base64-encoded `SNK` GitHub secret into a temporary `.snk` file and passes it as `StrongNameKeyPath`.
- The binary release workflow emits GitHub build attestations and Sigstore bundles for each zipped release artifact.

## Change expectations

- Preserve the single-command CLI shape unless the user explicitly asks to expand it.
- Preserve the current traversal strategy unless a different design is justified with performance evidence.
- Preserve NativeAOT and trimming compatibility unless the user explicitly accepts the tradeoff.
- When changing matcher semantics, check highlight rendering and existing tests.
- When changing defaults or output semantics, treat that as a user-facing behavior change.
- Before describing architecture or repo shape in answers, re-read the current source instead of relying on this file alone.
