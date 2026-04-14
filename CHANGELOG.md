# Changelog

## 1.2.1

- Refreshed Seek's CLI framework integration and console dependency stack without changing the command surface or output contracts.

## 1.2.0

- Seek is now available on WinGet via `winget install dusrdev.Seek`.
- Search, delete, update checks, and CLI errors now use a shared built-in palette for more consistent terminal output.
- Removed `--highlight-color` in favor of the built-in match-highlight palette.
- Added `seek delete --no-progress` to suppress the live progress bar during apply runs.

## 1.1.1

- Updated the shipped `seek-file-search` agent skill to reflect the 1.1.0 CLI behavior, including root-relative output by default, `--absolute`, `--null`, empty-query match-all searches, and `seek delete`.

## 1.1.0

- Search results are relative to `--root` by default. Use `--absolute` for standalone paths or `--null` for NUL-terminated absolute paths in scripts.
- `seek ""` now behaves like a match-all search instead of hanging.
- Added `seek delete`, which previews matches first and only removes them when you pass `--apply`.
- NuGet now publishes native AOT tool packages for common runtimes, plus a `Seek.any` fallback package for generic environments.

## 1.0.0

- Initial release
