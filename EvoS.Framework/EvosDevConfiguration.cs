using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using System.Collections.Generic;
using System.IO;

namespace EvoS.Framework
{
    public class EvosDevConfiguration
    {
        private static EvosDevConfiguration Instance = null;
        public List<string> DevList = new List<string>();

        private static EvosDevConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .Build();

                Instance = deserializer.Deserialize<EvosDevConfiguration>(File.ReadAllText("Config/developers.yaml"));
            }

            return Instance;
        }

        public static List<string> GetDevList() {
            return GetInstance().DevList;
        }

        public static bool IsDev(PersistedAccountData account) {
            return !GetDevList().Find(m => m == account.Handle.ToString()).IsNullOrEmpty();
        }
    }
}
