using EvoS.Framework.Game;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(1638998675)]
    public class RpcMovement : BaseRpc
    {
        public GameEventManager.EventType EventType;
        public GridPosProp Start;
        public GridPosProp EndGridPos;
        public BoardSquare EndSquare;
        public BoardSquarePathInfo Path;
        public ActorData.MovementType MovementType;
        public bool DisappearAfterMovement;
        public bool Respawning;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            var board = context.GameManager.Board;

            EventType = (GameEventManager.EventType) reader.ReadInt32();
            Start = GeneratedNetworkCode._ReadGridPosProp_None(reader);
            EndGridPos = GeneratedNetworkCode._ReadGridPosProp_None(reader);
            EndSquare = board.GetBoardSquare(GridPos.FromGridPosProp(EndGridPos));
            var pathBytes = reader.ReadBytesAndSize();
            Path = MovementUtils.DeSerializePath(board, pathBytes);
            MovementType = (ActorData.MovementType) reader.ReadInt32();
            DisappearAfterMovement = reader.ReadBoolean();
            Respawning = reader.ReadBoolean();
        }

        public override string ToString()
        {
            return $"{nameof(RpcMovement)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   (Start.m_height != 0 ? $"{nameof(Start)}: {Start}, " : "") +
                   (EndSquare != null ? $"{nameof(EndSquare)}: {EndSquare.ToPositionString()}, " : "") +
                   $"{nameof(EventType)}: {EventType}, " +
                   $"{nameof(MovementType)}: {MovementType}, " +
                   (Path != null ? $"{nameof(Path)}: {Path}, " : "") +
                   $"{nameof(DisappearAfterMovement)}: {DisappearAfterMovement}, " +
                   $"{nameof(Respawning)}: {Respawning}" +
                   ")";
        }
    }
}
