namespace Seek.Core;

[Flags]
internal enum SearchType {
    Files = 1,
    Directories = 2,
    FilesAndDirectories = Files | Directories
}
