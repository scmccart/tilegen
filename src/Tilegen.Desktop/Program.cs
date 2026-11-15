namespace Tilegen.Desktop;

public static class Program
{
    public static void Main()
    {
        using var game = new TilegenGame();
        game.Run();
    }
}
