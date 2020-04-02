using System;

namespace EvoS.Framework.Network.Unity
{
    public class Time
    {
        private static double _startTime = timeSinceEpoch;
        
        public static float realtimeSinceStartup => (float) (timeSinceEpoch - _startTime);
        public static float time => realtimeSinceStartup;
        public static double timeSinceEpoch
        {
            get
            {
                TimeSpan t = (DateTime.UtcNow - DateTime.UnixEpoch);
                return (double) t.TotalSeconds;
            }
        }
    }
}
