namespace CentralServer.LobbyServer.Utils;

public enum TmpSpriteId
{
    Flux = 0,
    Iso = 1,
    Mentor = 2,
    FreeRotation = 3,
    Prestige = 4,
    Group = 5,
    UpBig = 6,
    DownBig = 7,
    UpSmall = 8,
    DownSmall = 9,
}

public static class TmpSprite
{
    // TMP renders <sprite=N> as U+E000+N (Unicode PUA)
    private const int PuaBase = 0xE000;

    public static string Tag(TmpSpriteId sprite, int size) =>
        $"<size={size}><sprite={(int)sprite}></size>";

    public static char Rendered(TmpSpriteId sprite) =>
        (char)(PuaBase + (int)sprite);

    public const string TagRegex = @"<size=\d+><sprite=[0-9]></size>";
    public const string RenderedRegex = @"[\uE000-\uE009]";
}