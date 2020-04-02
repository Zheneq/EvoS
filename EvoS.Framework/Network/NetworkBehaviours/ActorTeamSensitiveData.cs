using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("ActorTeamSensitiveData")]
    public class ActorTeamSensitiveData : NetworkBehaviour
    {
        private static int kRpcRpcMovement = 1638998675;
        private static int kRpcRpcReceivedPingInfo = 1349069861;
        private static int kRpcRpcReceivedAbilityPingInfo = 315009541;
        public ObservedBy _typeObservingMe;
        private int _actorIndex = ActorData.s_invalidActorIndex;
        private List<BoardSquare> _respawnAvailableSquares = new List<BoardSquare>();
        private List<bool> _queuedAbilities = new List<bool>();
        private List<bool> _abilityToggledOn = new List<bool>();
        private List<GameObject> _oldPings = new List<GameObject>();

        private List<ActorTargeting.AbilityRequestData> _abilityRequestData =
            new List<ActorTargeting.AbilityRequestData>();

        private ActorData _associatedActor;
        private Vector3 _facingDirAfterMovement;
        private BoardSquare _lastMovementDestination;
        private BoardSquarePathInfo _lastMovementPath;
        private GameEventManager.EventType _lastMovementWaitForEvent;
        private ActorData.MovementType _lastMovementType;
        private BoardSquare _moveFromBoardSquare;
        private BoardSquare _initialMoveStartSquare;
        private LineData.LineInstance _movementLine = new LineData.LineInstance();
        private sbyte _numNodesInSnaredLine;
        private Bounds _movementCameraBounds;
        private BoardSquare _respawnPickedSquare;
        private bool _disappearingAfterMovement;
        private bool _assignedInitialBoardSquare;
        private float _lastPingChatTime;

        static ActorTeamSensitiveData()
        {
            RegisterRpcDelegate(typeof(ActorTeamSensitiveData), kRpcRpcMovement, InvokeRpcRpcMovement);
        }

        public List<ActorTargeting.AbilityRequestData> AbilityRequestData
        {
            get => _abilityRequestData;
            set
            {
                if (_abilityRequestData.Equals(value))
                {
                    return;
                }
                _abilityRequestData = value;
                MarkAsDirty(DirtyBit.AbilityRequestDataForTargeter);
            }
        }

        public LineData.LineInstance MovementLine
        {
            get
            {
                MarkAsDirty(DirtyBit.LineData);
                return _movementLine;
            }
        }

        public Vector3 FacingDirAfterMovement
        {
            get => _facingDirAfterMovement;
            set
            {
                if (!(_facingDirAfterMovement != value))
                    return;
                _facingDirAfterMovement = value;
                MarkAsDirty(DirtyBit.FacingDirection);
            }
        }

        public BoardSquare MoveFromBoardSquare
        {
            get => _moveFromBoardSquare;
            set
            {
                if (_moveFromBoardSquare == value)
                    return;
                _moveFromBoardSquare = value;
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                MarkAsDirty(DirtyBit.MoveFromBoardSquare);
            }
        }

        public BoardSquare InitialMoveStartSquare
        {
            get => _initialMoveStartSquare;
            set
            {
                if (_initialMoveStartSquare == value)
                    return;
                _initialMoveStartSquare = value;
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                MarkAsDirty(DirtyBit.InitialMoveStartSquare);
            }
        }

        public Bounds MovementCameraBounds
        {
            get => _movementCameraBounds;
            set
            {
                if (_movementCameraBounds != value)
                    _movementCameraBounds = value;
                if (EvoSGameConfig.NetworkIsServer)
                    MarkAsDirty(DirtyBit.MovementCameraBound);
            }
        }

        public BoardSquare RespawnPickedSquare
        {
            get => _respawnPickedSquare;
            set
            {
                _respawnPickedSquare = value;
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                MarkAsDirty(DirtyBit.Respawn);
            }
        }

        public List<BoardSquare> RespawnAvailableSquares
        {
            get => _respawnAvailableSquares;
            set
            {
                _respawnAvailableSquares = value;
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                MarkAsDirty(DirtyBit.Respawn);
            }
        }

        public BoardSquarePathInfo LastMovementPath
        {
            get => _lastMovementPath;
            set
            {
                _lastMovementPath = value;
            }
        }

        public Team ActorsTeam => Actor?.Team ?? Team.Invalid;

        public ActorData Actor
        {
            get
            {
                // TODO
                if (_associatedActor == null && _actorIndex != ActorData.s_invalidActorIndex &&
                    GameFlowData != null)
                {
                    var actorByActorIndex = GameFlowData.FindActorByActorIndex(_actorIndex);
                    if (actorByActorIndex != null)
                        _associatedActor = actorByActorIndex;
                }

                return _associatedActor;
            }
        }

        public void MarkAsRespawning()
        {
        }

        public int ActorIndex => _actorIndex;

        public bool HasToggledAction(AbilityData.ActionType actionType)
        {
            var flag = false;
            if (actionType != AbilityData.ActionType.INVALID_ACTION)
            {
                var index = (int) actionType;
                if (index >= 0 && index < _abilityToggledOn.Count)
                    flag = _abilityToggledOn[index];
            }

            return flag;
        }

        public ActorTeamSensitiveData()
        {
        }

        public ActorTeamSensitiveData(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public void SetActorIndex(int actorIndex)
        {
            if (_actorIndex == actorIndex && _associatedActor != null)
                return;
            _actorIndex = actorIndex;
            _associatedActor = GameFlowData?.FindActorByActorIndex(_actorIndex);
        }

        public override void Awake()
        {
            for (int index = 0; index < 14; ++index)
            {
                _queuedAbilities.Add(false);
                _abilityToggledOn.Add(false);
            }
        }

//        [ClientRpc]
        private void RpcMovement(
            GameEventManager.EventType wait,
            GridPosProp start,
            GridPosProp end_grid,
            byte[] pathBytes,
            ActorData.MovementType type,
            bool disappearAfterMovement,
            bool respawning)
        {
            if (EvoSGameConfig.NetworkIsServer)
                return;
            ProcessMovement(wait, GridPos.FromGridPosProp(start),
                Board.GetBoardSquare(GridPos.FromGridPosProp(end_grid)), MovementUtils.DeSerializePath(this, pathBytes),
                type, disappearAfterMovement, respawning);
        }

        private void ProcessMovement(
            GameEventManager.EventType wait,
            GridPos start,
            BoardSquare end,
            BoardSquarePathInfo path,
            ActorData.MovementType type,
            bool disappearAfterMovement,
            bool respawning)
        {
//            this.FlushQueuedMovement();
            bool flag1;
            bool flag2 = (flag1 = end != null) && _lastMovementDestination != end;
            bool flag3 = Actor?.CurrentBoardSquare == null;
            bool flag4 = flag1 && !flag2 && (!flag3 && path != null) &&
                         path.GetPathEndpoint().square == Actor.CurrentBoardSquare;
            _lastMovementDestination = end;
            _lastMovementPath = path;
            _lastMovementWaitForEvent = wait;
            _lastMovementType = type;
            _disappearingAfterMovement = disappearAfterMovement;
            bool flag5 = false;
//    if (Actor?.method_9() != null) // TODO ?
//      flag5 = Actor.method_9().AmMoving(); 
            int num = 0;
            if (GameFlowData != null)
                num = GameFlowData.CurrentTurn;
            if (!flag5 && wait == GameEventManager.EventType.Invalid && (Actor != null && Actor.LastDeathTurn != num) &&
                (!Actor.method_38() || respawning))
            {
                if (!flag2 && (!flag3 || !flag1) && !flag4)
                {
                    if (!flag1 && disappearAfterMovement)
                        Actor.OnMovementWhileDisappeared(type);
                }
                else
                {
                    if (path == null && type != ActorData.MovementType.Teleport)
                        Actor.MoveToBoardSquareLocal(end, ActorData.MovementType.Teleport, path,
                            disappearAfterMovement);
                    else
                        Actor.MoveToBoardSquareLocal(end, type, path, disappearAfterMovement);
//        if (respawning && end != null)
//          this.HandleRespawnCharacterVisibility(Actor);
                }

                if (_assignedInitialBoardSquare)
                    return;
//      Actor.gameObject.SendMessage("OnAssignedToInitialBoardSquare", SendMessageOptions.DontRequireReceiver);
                _assignedInitialBoardSquare = true;
            }
            else if (!flag5 && wait != GameEventManager.EventType.Invalid &&
                     (Actor != null && Actor.LastDeathTurn != num) && (!Actor.method_38() || respawning))
            {
                BoardSquare dest = Board.GetBoardSquare(start);
                if (dest == null || dest == Actor.CurrentBoardSquare)
                    return;
                Actor.AppearAtBoardSquare(dest);
            }
            else
            {
                if (Actor == null || !respawning)
                    return;
//      this.HandleRespawnCharacterVisibility(Actor);
            }
        }

        public void MarkAsDirty(DirtyBit bit)
        {
            SetDirtyBit((uint) bit);
        }

        private bool IsBitDirty(uint setBits, DirtyBit bitToTest)
        {
            return ((DirtyBit) setBits & bitToTest) != ~DirtyBit.All;
        }

        private void SerializeAbilityRequestData(NetworkWriter writer)
        {
            byte count = (byte) _abilityRequestData.Count;
            writer.Write(count);
            for (int index = 0; index < (int) count; ++index)
            {
                sbyte actionType = (sbyte) _abilityRequestData[index]._actionType;
                writer.Write(actionType);
                AbilityTarget.SerializeAbilityTargetList(_abilityRequestData[index]._targets, writer);
            }
        }

        private void DeSerializeAbilityRequestData(NetworkReader reader)
        {
            _abilityRequestData.Clear();
            byte num = reader.ReadByte();
            for (int index = 0; index < (int) num; ++index)
                _abilityRequestData.Add(new ActorTargeting.AbilityRequestData(
                    (AbilityData.ActionType) reader.ReadSByte(), AbilityTarget.DeSerializeAbilityTargetList(reader)));
//            if (!(this.Actor != null) || !(this.Actor.\u000E() != null))
//                return;
//            this.Actor.\u000E().OnRequestDataDeserialized();
//            this.Actor.OnClientQueuedActionChanged();
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            uint setBits = uint.MaxValue;
            var initialPos = writer.Position;
            if (!forceAll)
            {
                setBits = syncVarDirtyBits;
                writer.WritePackedUInt32(setBits);
            }

            Log.Print(LogType.Warning, $"{nameof(ActorTeamSensitiveData)}.{nameof(OnSerialize)} not implemented!");
            writer.Write((sbyte) _actorIndex);

            if (IsBitDirty(setBits, DirtyBit.FacingDirection))
            {
                // TODO - this is likely wrong
                writer.Write((short) VectorUtils.HorizontalAngle_Deg(_facingDirAfterMovement));
            }

            if (IsBitDirty(setBits, DirtyBit.MoveFromBoardSquare))
            {
                writer.Write((short) (MoveFromBoardSquare?.X ?? -1));
                writer.Write((short) (MoveFromBoardSquare?.Y ?? -1));
            }

            if (IsBitDirty(setBits, DirtyBit.InitialMoveStartSquare))
            {
                // TODO
                writer.Write((short) (InitialMoveStartSquare?.X ?? -1));
                writer.Write((short) (InitialMoveStartSquare?.X ?? -1));
            }

            if (IsBitDirty(setBits, DirtyBit.LineData))
            {
                writer.Write(ServerClientUtils.CreateBitfieldFromBools(_movementLine != null,
                    _numNodesInSnaredLine != 0, false, false, false, false, false, false));
                if (_movementLine != null)
                {
                    LineData.SerializeLine(_movementLine, writer);
                }

                if (_numNodesInSnaredLine != 0)
                {
                    writer.Write(_numNodesInSnaredLine);
                }
            }

            if (IsBitDirty(setBits, DirtyBit.MovementCameraBound))
            {
                writer.Write((short) MovementCameraBounds.center.X);
                writer.Write((short) MovementCameraBounds.center.Z);
                writer.Write((short) MovementCameraBounds.size.X);
                writer.Write((short) MovementCameraBounds.size.Z);
            }

            if (IsBitDirty(setBits, DirtyBit.Respawn))
            {
                writer.Write((short) (RespawnPickedSquare?.X ?? -1));
                writer.Write((short) (RespawnPickedSquare?.Y ?? -1));

                writer.Write(false); // TODO respawningThisTurn

                writer.Write((short) _respawnAvailableSquares.Count);
                foreach (var square in _respawnAvailableSquares)
                {
                    writer.Write((short) square.X);
                    writer.Write((short) square.Y);
                }
            }

            var flag1 = IsBitDirty(setBits, DirtyBit.QueuedAbilities);
            if (flag1 || IsBitDirty(setBits, DirtyBit.AbilityRequestDataForTargeter))
                SerializeAbilityRequestData(writer);
            if (flag1)
            {
                short queuedAbilitiesFlag = 0;
                for (var index = 0; index < 14; ++index)
                {
                    var flag = (short) (1 << index);
                    if (_queuedAbilities[index])
                    {
                        queuedAbilitiesFlag |= flag;
                    }
                }

                writer.Write(queuedAbilitiesFlag);
            }

            if (IsBitDirty(setBits, DirtyBit.ToggledOnAbilities))
            {
                short toggledAbiltiesFlag = 0;
                for (var index = 0; index < 14; ++index)
                {
                    var mask = (short) (1 << index);
                    if (_abilityToggledOn[index])
                        toggledAbiltiesFlag |= mask;
                }

                writer.Write(toggledAbiltiesFlag);
            }

            return initialPos != writer.Position;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            uint setBits = uint.MaxValue;
            if (!initialState)
                setBits = reader.ReadPackedUInt32();
            _actorIndex = reader.ReadSByte();
            if (IsBitDirty(setBits, DirtyBit.FacingDirection))
            {
                short num = reader.ReadInt16();
                _facingDirAfterMovement = num >= (short) 0 ? VectorUtils.AngleDegreesToVector(num) : Vector3.Zero;
//                if (this.Actor != null)
//                    this.Actor.SetFacingDirectionAfterMovement(_facingDirAfterMovement);
            }

            if (IsBitDirty(setBits, DirtyBit.MoveFromBoardSquare))
            {
                BoardSquare boardSquare = Board.GetBoardSquare((int) reader.ReadInt16(), (int) reader.ReadInt16());
                if (boardSquare != MoveFromBoardSquare)
                {
                    MoveFromBoardSquare = boardSquare;
//                    if (this.Actor != null && this.Actor.\u000E() != null)
//                        this.Actor.\u000E().UpdateSquaresCanMoveTo();
                }
            }

            if (IsBitDirty(setBits, DirtyBit.InitialMoveStartSquare))
            {
                BoardSquare boardSquare = Board.GetBoardSquare((int) reader.ReadInt16(), (int) reader.ReadInt16());
                if (InitialMoveStartSquare != boardSquare)
                {
                    InitialMoveStartSquare = boardSquare;
//                    if (this.Actor != null && this.Actor.\u000E() != null)
//                        this.Actor.\u000E().UpdateSquaresCanMoveTo();
                }
            }

            if (IsBitDirty(setBits, DirtyBit.LineData))
            {
                bool out0;
                bool out1;
                ServerClientUtils.GetBoolsFromBitfield(reader.ReadByte(), out out0, out out1);
                _movementLine = !out0 ? null : LineData.DeSerializeLine(reader);
                _numNodesInSnaredLine = !out1 ? (sbyte) 0 : reader.ReadSByte();
                if (Actor != null)
                {
                    LineData component = Actor.GetComponent<LineData>();
                    if (component != null)
                        component.OnDeserializedData(_movementLine, _numNodesInSnaredLine);
                }
            }

            if (IsBitDirty(setBits, DirtyBit.MovementCameraBound))
            {
                // TODO
                var x = reader.ReadInt16();
                var y = reader.ReadInt16();
                var sizeA = reader.ReadInt16();
                var sizeB = reader.ReadInt16();
//                MovementCameraBounds =
//                    new Bounds(
//                        new Vector3(reader.ReadInt16(), 1.5f, // TODO + (float) Board.\u000E().BaselineHeight,
//                            reader.ReadInt16()), new Vector3(reader.ReadInt16(), 3f, reader.ReadInt16()));
            }

            if (IsBitDirty(setBits, DirtyBit.Respawn))
            {
                // TODO
                RespawnPickedSquare = Board.GetBoardSquare((int) reader.ReadInt16(), (int) reader.ReadInt16());
                bool respawningThisTurn = reader.ReadBoolean();
                short num1 = reader.ReadInt16();
                _respawnAvailableSquares.Clear();
                for (int index = 0; index < (int) num1; ++index)
                {
                    var num2 = reader.ReadInt16();
                    var num3 = reader.ReadInt16();
                    var boardSquare = Board.GetBoardSquare((int) num2, (int) num3);
                    if (boardSquare != null)
                        _respawnAvailableSquares.Add(boardSquare);
                    else
                        Log.Print(LogType.Error, $"Invalid square received for respawn choices {num2}, {num3}");
                }
            }

            bool flag1 = IsBitDirty(setBits, DirtyBit.QueuedAbilities);
            if (flag1 || IsBitDirty(setBits, DirtyBit.AbilityRequestDataForTargeter))
                DeSerializeAbilityRequestData(reader);
            if (flag1)
            {
                bool flag2 = false;
                short num1 = reader.ReadInt16();
                for (int index = 0; index < 14; ++index)
                {
                    short num2 = (short) (1 << index);
                    bool flag3 = (num1 & num2) != 0;
                    if (_queuedAbilities[index] != flag3)
                    {
                        _queuedAbilities[index] = flag3;
                        flag2 = true;
                    }
                }

//                if (flag2 && Actor != null && Actor.\u000E() != null)
//                    Actor.\u000E().OnQueuedAbilitiesChanged();
            }

            if (!IsBitDirty(setBits, DirtyBit.ToggledOnAbilities))
                return;
            short num4 = reader.ReadInt16();
            for (int index = 0; index < 14; ++index)
            {
                short num1 = (short) (1 << index);
                bool flag2 = (num4 & num1) != 0;
                if (_abilityToggledOn[index] != flag2)
                    _abilityToggledOn[index] = flag2;
            }
        }

        public void CallRpcMovement(
            GameEventManager.EventType wait,
            GridPosProp start,
            GridPosProp end_grid,
            byte[] pathBytes,
            ActorData.MovementType type,
            bool disappearAfterMovement,
            bool respawning)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcMovement called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcMovement);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write((int) wait);
                GeneratedNetworkCode._WriteGridPosProp_None(writer, start);
                GeneratedNetworkCode._WriteGridPosProp_None(writer, end_grid);
                writer.WriteBytesFull(pathBytes);
                writer.Write((int) type);
                writer.Write(disappearAfterMovement);
                writer.Write(respawning);
                SendRPCInternal(writer, 0, "RpcMovement");
            }
        }

        protected static void InvokeRpcRpcMovement(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcMovement called on server.");
            else
                ((ActorTeamSensitiveData) obj).RpcMovement((GameEventManager.EventType) reader.ReadInt32(),
                    GeneratedNetworkCode._ReadGridPosProp_None(reader),
                    GeneratedNetworkCode._ReadGridPosProp_None(reader), reader.ReadBytesAndSize(),
                    (ActorData.MovementType) reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());
        }

        public bool HasQueuedAction(AbilityData.ActionType actionType)
        {
            bool result = false;
            if (actionType != AbilityData.ActionType.INVALID_ACTION)
            {
                result = (actionType >= AbilityData.ActionType.ABILITY_0 && actionType < (AbilityData.ActionType)this._queuedAbilities.Count && this._queuedAbilities[(int)actionType]);
            }
            return result;
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            _typeObservingMe = (ObservedBy) stream.ReadInt32(); // valuetype ActorTeamSensitiveData.ObservedBy
        }

        public override string ToString()
        {
            return $"{nameof(ActorTeamSensitiveData)}>(" +
                   $"{nameof(_typeObservingMe)}: {_typeObservingMe}, " +
                   ")";
        }

        public enum DirtyBit : uint
        {
            FacingDirection = 1,
            MoveFromBoardSquare = 2,
            LineData = 4,
            MovementCameraBound = 8,
            Respawn = 16, // 0x00000010
            InitialMoveStartSquare = 32, // 0x00000020
            QueuedAbilities = 64, // 0x00000040
            ToggledOnAbilities = 128, // 0x00000080
            AbilityRequestDataForTargeter = 256, // 0x00000100
            All = 4294967295 // 0xFFFFFFFF
        }

        public enum ObservedBy
        {
            Friendlies,
            Hostiles
        }
    }
}
