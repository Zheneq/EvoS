using System.IO;
using log4net;
using YamlDotNet.Serialization;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordBotConfiguration
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordBotConfiguration));
        private const string ConfigPath = "Config/discordBot.yaml";

        private static DiscordBotConfiguration Instance;

        public bool Enabled = false;
        public string BotToken = "";
        public ulong? BotChannelId;
        public ulong? RequestChannelId;

        public static DiscordBotConfiguration Get()
        {
            if (Instance == null)
            {
                if (File.Exists(ConfigPath))
                {
                    var deserializer = new DeserializerBuilder().Build();
                    Instance = deserializer.Deserialize<DiscordBotConfiguration>(File.ReadAllText(ConfigPath));
                }
                else
                {
                    log.Info($"{ConfigPath} not found, Discord bot is disabled");
                    Instance = new DiscordBotConfiguration();
                }
            }

            return Instance;
        }
    }
}
