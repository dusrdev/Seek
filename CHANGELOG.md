# Changelog

## Unreleased

- Fix empty-query searches so `seek ""` matches all eligible files and directories instead of hanging indefinitely.
- Search and highlight against paths relative to `--root`, and print root-relative results by default.
- Add `-a` / `--absolute` to emit absolute paths when needed.
- Make `--null` emit plain absolute NUL-terminated paths for safe piping.
- Add `seek delete` with preview-by-default behavior and `--apply` for sequential deletion.
- Publish runtime-specific native AOT NuGet tool packages for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
- Publish a framework-dependent `Seek.any` NuGet tool package as a fallback for unsupported or generic environments.

## 1.0.0

- Initial release
