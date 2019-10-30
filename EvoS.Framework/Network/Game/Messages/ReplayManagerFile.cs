using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(serverMsgIds: new short[] {48})]
    public class ReplayManagerFile : MessageBase
    {
        public string Fragment;
        public bool Restart;
        public bool Save;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(Fragment);
            writer.Write(Restart);
            writer.Write(Save);
        }

        public override void Deserialize(NetworkReader reader)
        {
            Fragment = reader.ReadString();
            Restart = reader.ReadBoolean();
            Save = reader.ReadBoolean();
        }

        public override string ToString()
        {
            return $"{nameof(ReplayManagerFile)}(" +
                   $"{nameof(Fragment)}: {Fragment.Length} characters, " +
                   $"{nameof(Restart)}: {Restart}, " +
                   $"{nameof(Save)}: {Save}" +
                   ")";
        }
    }
}
