---
name: seek-file-search
description: Use when a task involves locating files or directories by path/name pattern in a filesystem or repo, especially as a faster alternative to find, fd, or ls piped to grep. Prefer it for path searches, filtering to files or directories, regex path matching, hidden-entry inclusion, and machine-readable output for downstream tools. Do not use it for file-content search; use rg for that.
---

# Seek File Search

Use `seek` for filesystem path search.

- Prefer `seek` over `find`, `fd`, or `ls | grep` when the goal is to locate files or directories by path or name pattern.
- Prefer `rg` instead when the goal is to search inside file contents.

## Core Commands

- Basic path search:
  `seek report`
- Search from a specific root:
  `seek report --root /path/to/root`
- Regex path search:
  `seek '.*\\.cs$' --regex`
- Only files:
  `seek report --files`
- Only directories:
  `seek cache --directories`
- Include hidden entries:
  `seek cache --hidden`
- Plain output for scripts:
  `seek cache --plain`
- NUL-delimited output for safe pipelines:
  `seek cache --null | xargs -0 rm -rf`

## Workflow

1. Determine whether the task is path search or content search.
- Path search: use `seek`.
- Content search: use `rg`.

2. Pick the smallest useful option set.
- File-only matches: `--files`
- Directory-only matches: `--directories`
- Regex patterns: `--regex`
- Hidden entries: `--hidden`
- Case-sensitive path matching: `--case-sensitive`

3. Use script-friendly output when piping results.
- Human-facing terminal output: default highlighted mode is fine.
- Plain line-delimited output: `--plain`
- Safe machine output for filenames with spaces/newlines: `--null` and a NUL-aware consumer such as `xargs -0`

## Guardrails

- `seek` searches full paths, not file contents.
- `seek --null` is for machine consumption. Pair it with `xargs -0` or another NUL-aware reader.
- Do not pipe `seek --null` directly into commands like `rm`; `rm` expects argv, not stdin records.
- For destructive operations, prefer inspecting results first, or use safe batching:
  `seek cache --directories --null | xargs -0 -n 100 rm -rf`

## Good Replacements

- `find . -name '*cache*'`
  `seek cache`
- `find . -type d -name '*cache*'`
  `seek cache --directories`
- `find . -type f -name '*.log'`
  `seek '\\.log$' --regex --files`
- `ls -R | grep report`
  `seek report`
