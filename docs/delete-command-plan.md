# Delete Command Plan

## Goal

Add a dedicated `seek delete` command that previews deletion candidates by default and only performs deletion when `--apply` is specified.

This command should reuse the existing search engine and search-selection semantics while keeping destructive behavior explicit and separate from the default search command.

## Agreed CLI Shape

- Keep the default command unchanged: `seek <query> ...`
- Add a dedicated destructive command: `seek delete <query> ...`
- For now, duplicate the existing search-selection arguments on `delete`
- Add only one delete-specific flag:
  - `--apply`

Examples:

```bash
seek delete report
seek delete report --apply
seek delete report --files
seek delete ".*\\.tmp$" --regex --apply
```

## Delete Command Semantics

- `seek delete ...` is preview mode by default
- Preview mode prints the candidate list and does not delete anything
- Preview mode ends with a hint telling the user to rerun with `--apply`
- `seek delete ... --apply` performs deletion
- Deletion is sequential, not parallel
- Each candidate deletion is wrapped in its own `try/catch`
- Output one status line per candidate in apply mode:
  - `SUCCESS <path>`
  - `FAIL <path> - <error message>`
- Exit code is:
  - `0` if all deletions succeed
  - `1` if any deletion fails

## Shared Search Arguments For `delete`

Duplicate the current search-selection arguments from `Commands.SearchAsync` for now:

- `query`
- `regex`
- `caseSensitive`
- `hidden`
- `system`
- `files`
- `directories`
- `root`

Do not add these output-specific search arguments to `delete`:

- `plain`
- `absolute`
- `null`
- `highlightColor`

Argument duplication is acceptable for now and can later be replaced with a shared `SearchArguments` record once ConsoleAppFramework supports record binding.

## Internal Design

### Command Registration

- Add `app.Add("delete", Commands.DeleteAsync);` in `src/Seek.Cli/Program.cs`
- Add a new file: `src/Seek.Cli/Commands.Delete.cs`

### Shared Helpers

Extract small shared helpers from `src/Seek.Cli/Commands.Search.cs` so both commands use the same traversal semantics:

- helper to map `(files, directories)` to `SearchType`
- helper to compute `FileAttributes AttributesToSkip`
- helper to construct `FileSystemSearch`

This keeps behavior aligned between `search` and `delete` without prematurely introducing a larger abstraction.

## Candidate Collection

Delete must be implemented as a two-phase operation.

### Phase 1: Collect

- Run `FileSystemSearch`
- Work internally with absolute matched paths
- Classify each candidate as file or directory

### Phase 2: Collapse

- Collapse descendants under matched directories
- If a matched directory is selected, do not keep nested matched files or directories as separate candidates
- A matched directory deletion is recursive

This avoids noisy output and prevents redundant operations after the parent directory is deleted.

Explicit deduplication is not required because the current `FileSystemSearch` implementation does not produce duplicate matches.

## Output Behavior

### Preview Mode

- Print the final candidate list after descendant collapsing
- Use one line per candidate
- End with a summary hint such as:

```text
Dry run only. Re-run with --apply to delete these entries.
```

### Apply Mode

- Delete files with `File.Delete`
- Delete directories with `Directory.Delete(path, recursive: true)`
- Process candidates sequentially
- Print per-entry status lines

Example:

```text
SUCCESS /tmp/foo.txt
FAIL /tmp/bar - Access to the path is denied.
```

## Implementation Steps

1. Add the `delete` command entry point in `Program.cs`
2. Create `Commands.Delete.cs` with duplicated search-selection arguments plus `bool apply`
3. Extract shared search-construction helpers from `Commands.Search.cs`
4. Implement candidate collection and descendant collapsing
5. Implement preview-by-default output
6. Implement sequential apply mode with per-entry status reporting
7. Return exit code `1` when any deletion fails

## Test Plan

Add CLI tests covering:

- preview mode does not delete files
- preview mode does not delete directories
- apply mode deletes matched files
- apply mode deletes matched directories recursively
- descendant collapsing suppresses nested entries under matched directories
- mixed success and failure returns exit code `1`
- command registration for `delete`

## Documentation Follow-Up

Because this adds a new user-facing command and offers a built-in alternative to shell piping, review whether to update:

- `README.md`
- `CHANGELOG.md`

Recommended README changes:

- add usage examples for `seek delete`
- present `seek delete` as the built-in alternative to `seek ... --null | xargs -0 rm`
