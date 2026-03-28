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
- Emit absolute paths instead of paths relative to `--root`:
  `seek report --root /path/to/root --absolute`
- Regex path search:
  `seek '.*\\.cs$' --regex`
- Match all eligible files:
  `seek '' --files`
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
- Preview matching deletes:
  `seek delete cache --directories`
- Apply matching deletes:
  `seek delete cache --directories --apply`
- Apply matching deletes without the live progress bar:
  `seek delete cache --directories --apply --no-progress`

## Workflow

1. Determine whether the task is path search or content search.
- Path search: use `seek`.
- Content search: use `rg`.

2. Pick the smallest useful option set.
- Root-relative output is the default. Add `--absolute` when the consumer needs standalone paths.
- File-only matches: `--files`
- Directory-only matches: `--directories`
- Regex patterns: `--regex`
- Hidden entries: `--hidden`
- Case-sensitive path matching: `--case-sensitive`

3. Use script-friendly output when piping results.
- Human-facing terminal output: default highlighted mode is fine.
- Plain line-delimited output: `--plain`
- Safe machine output for filenames with spaces/newlines: `--null` and a NUL-aware consumer such as `xargs -0`

4. For destructive work, preview before apply.
- Preview with `seek delete ...`
- Delete only with `seek delete ... --apply`
- Add `--no-progress` when you want durable status lines without the live progress UI.

## Guardrails

- `seek` searches full paths, not file contents.
- Search output is relative to `--root` by default.
- `seek --absolute` emits absolute paths.
- `seek --null` is for machine consumption. It emits plain absolute paths terminated by `\0`. Pair it with `xargs -0` or another NUL-aware reader.
- `seek ''` matches all eligible entries under the selected root.
- `seek delete` previews absolute candidate paths and only deletes when `--apply` is present.
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
- `find . -type d -name '*cache*' -print0 | xargs -0 rm -rf`
  `seek delete cache --directories --apply`
