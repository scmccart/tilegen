namespace Tilegen.Core;

// Deliberate violation to show CI fails on it (functional-core R1). Reverted in the next commit.
public static class CiBite
{
    public static string Name(string path) => System.IO.Path.GetFileName(path);
}
