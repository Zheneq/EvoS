using System;

namespace EvoS.Framework.GameData;

[Serializable]
public class GamePackData
{
    public GamePack[] m_gamePacks;

    public static GamePackData Get()
    {
        return GameWideData.Get().m_gamePackData;
    }
}
