using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Misc
{
    [Serializable]
    public class ServerGameMetrics
    {
        public int CurrentTurn;

        public int TeamAPoints;

        public int TeamBPoints;

        public float AverageFrameTime;
    }

}
