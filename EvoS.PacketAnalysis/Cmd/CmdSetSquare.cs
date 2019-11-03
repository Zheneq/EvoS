using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Cmd
{
    [Cmd(-1156253069)]
    public class CmdSetSquare : BaseCmd
    {
        public int X;
        public int Y;
        public bool SetWaypoint;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            X = (int) reader.ReadPackedUInt32();
            Y = (int) reader.ReadPackedUInt32();
            SetWaypoint = reader.ReadBoolean();
        }

        public override string ToString()
        {
            return $"{nameof(CmdSetSquare)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(X)}: {X}, " +
                   $"{nameof(Y)}: {Y}, " +
                   $"{nameof(SetWaypoint)}: {SetWaypoint}" +
                   ")";
        }
    }
}
