using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    public class ActorAnimation
    {
        private AbilityData.ActionType _actionType = AbilityData.ActionType.INVALID_ACTION;
        public int ActorIndex = ActorData.s_invalidActorIndex;
        private List<byte> u000C = new List<byte>();
        private List<byte> u0014 = new List<byte>();
        private List<string> u0018u000E = new List<string>();
        public const float u001D = 3f;
        private short u000E;
        private Vector3 u0012;
        private bool u0015;
        private int u0016;
        private bool u0013;
        private bool u0018;
        private bool u0009;
        private bool u0011;
        private bool u001A;
        private bool u0004;
        private Ability u000B;
        public bool u0008;
        public int u0002;
        public sbyte u000A;
        public sbyte u0006;
        public Bounds u0020;
        private bool u0005;

//        private AbilityRequest u001B;
        private Turn u001E;
        private bool u0001;
        private bool u0010;
        private float u0007;
        private float u001C;
        private float u001Du000E;
        private float u000Eu000E;
        private bool u0012u000E;
        private float u0015u000E;

        private bool u0016u000E;

//        public Bounds u0013u000E;
//        private ActorAnimation.PlaybackState u0009u000E;
        private const float ActorIndexu000E = 1f;

        public Dictionary<ActorData, int> HitActorsToDeltaHP { get; private set; }
        internal SequenceSource SeqSource { get; private set; }
        internal SequenceSource ParentAbilitySeqSource { get; private set; }

        public ActorAnimation(Turn _param1)
        {
            u001E = _param1;
        }

        internal ActorData ActorData // u000Du000E
        {
            get
            {
                throw new NotImplementedException();
//                if (ActorIndex == ActorData.s_invalidActorIndex || GameFlowData.Get() == null)
//                    return null;
//                return GameFlowData.Get().FindActorByActorIndex(ActorIndex);
            }
            set
            {
                if (value == null && ActorIndex != ActorData.s_invalidActorIndex)
                {
                    ActorIndex = ActorData.s_invalidActorIndex;
                }
                else
                {
                    if (value == null || value.ActorIndex == ActorIndex)
                        return;
                    ActorIndex = value.ActorIndex;
                }
            }
        }
        internal void u000Du000E(IBitStream _param1)
        {
            sbyte num1 = (sbyte) u000E;
            sbyte num2 = (sbyte) _actionType;
            float x = u0012.X;
            float z = u0012.Z;
            sbyte num3 = (sbyte) ActorIndex;
            // TODO
//            sbyte num3 = ActorData != null
//                ? (sbyte) ActorData.ActorIndex
//                : (sbyte) ActorData.s_invalidActorIndex;
            bool flag1 = u0008;
            sbyte num4 = (sbyte) u0016;
            bool flag2 = u0013;
            bool flag3 = u0018;
            bool flag4 = u0009;
            bool flag5 = u0015;
            sbyte num5 = u000A;
            sbyte num6 = u0006;
            Vector3 center = u0020.center;
            Vector3 size = u0020.size;
            byte count = checked((byte) u000C.Count);
            _param1.Serialize(ref num1);
            _param1.Serialize(ref num2);
            _param1.Serialize(ref x);
            _param1.Serialize(ref z);
            _param1.Serialize(ref num3);
            _param1.Serialize(ref flag1);
            _param1.Serialize(ref num4);
            _param1.Serialize(ref flag2);
            _param1.Serialize(ref flag3);
            _param1.Serialize(ref flag4);
            _param1.Serialize(ref flag5);
            _param1.Serialize(ref num5);
            _param1.Serialize(ref num6);
            short num7 = (short) Mathf.RoundToInt(center.X);
            short num8 = (short) Mathf.RoundToInt(center.Z);
            _param1.Serialize(ref num7);
            _param1.Serialize(ref num8);
            if (_param1.isReading)
            {
                center.X = num7;
                center.Y = 1.5f; //+ (float) Board.u000E().BaselineHeight; // TODO
                center.Z = num8;
            }

            short num9 = (short) Mathf.CeilToInt(size.X + 0.5f);
            short num10 = (short) Mathf.CeilToInt(size.Z + 0.5f);
            _param1.Serialize(ref num9);
            _param1.Serialize(ref num10);
            if (_param1.isReading)
            {
                size.X = num9;
                size.Y = 3f;
                size.Z = num10;
            }

            _param1.Serialize(ref count);
            if (_param1.isReading)
            {
                for (int index = 0; index < (int) count; ++index)
                {
                    byte num11 = 0;
                    byte num12 = 0;
                    _param1.Serialize(ref num11);
                    _param1.Serialize(ref num12);
                    u000C.Add(num11);
                    u0014.Add(num12);
                }
            }
            else
            {
                for (int index = 0; index < (int) count; ++index)
                {
                    byte num11 = u000C[index];
                    byte num12 = u0014[index];
                    _param1.Serialize(ref num11);
                    _param1.Serialize(ref num12);
                }
            }

            u000E = num1;
//            if (_param1.isReading) // TODO
//                u0012 = new Vector3(x, (float) Board.u000E().BaselineHeight, z);
            ActorIndex = num3;
            u0008 = flag1;
            u0016 = num4;
            u0013 = flag2;
            u0018 = flag3;
            u0009 = flag4;
            u0015 = flag5;
            u000A = num5;
            u0006 = num6;
            u0020 = new Bounds(center, size);
            _actionType = (AbilityData.ActionType) num2;
//            u000B = this.ActorData != null // TODO
//                ? this.ActorData.u000E().GetAbilityOfActionType(_actionType)
//                : (Ability) null;
            if (SeqSource == null)
                SeqSource = new SequenceSource();
            SeqSource.OnSerializeHelper(_param1);
            if (_param1.isWriting)
            {
                bool flag6 = ParentAbilitySeqSource != null;
                _param1.Serialize(ref flag6);
                if (flag6)
                    ParentAbilitySeqSource.OnSerializeHelper(_param1);
            }

            if (_param1.isReading)
            {
                bool flag6 = false;
                _param1.Serialize(ref flag6);
                if (flag6)
                {
                    if (ParentAbilitySeqSource == null)
                        ParentAbilitySeqSource = new SequenceSource();
                    ParentAbilitySeqSource.OnSerializeHelper(_param1);
                }
                else
                    ParentAbilitySeqSource = null;
            }

            sbyte num13 = HitActorsToDeltaHP != null ? checked((sbyte) HitActorsToDeltaHP.Count) : (sbyte) 0;
            _param1.Serialize(ref num13);
            if (num13 > 0 && HitActorsToDeltaHP == null)
                HitActorsToDeltaHP = new Dictionary<ActorData, int>();
            if (_param1.isWriting && num13 > 0)
            {
                foreach (KeyValuePair<ActorData, int> keyValuePair in HitActorsToDeltaHP)
                {
                    sbyte num11 = !(keyValuePair.Key == null)
                        ? (sbyte) keyValuePair.Key.ActorIndex
                        : (sbyte) ActorData.s_invalidActorIndex;
                    if (num11 != ActorData.s_invalidActorIndex)
                    {
                        sbyte num12 = 0;
                        if (keyValuePair.Value > 0)
                            num12 = 1;
                        else if (keyValuePair.Value < 0)
                            num12 = -1;
                        _param1.Serialize(ref num11);
                        _param1.Serialize(ref num12);
                    }
                }
            }
            else if (_param1.isReading)
            {
                if (HitActorsToDeltaHP != null)
                    HitActorsToDeltaHP.Clear();
                for (int index = 0; index < (int) num13; ++index)
                {
                    sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
                    sbyte num11 = 0;
                    _param1.Serialize(ref invalidActorIndex);
                    _param1.Serialize(ref num11);
                    // TODO
//                    ActorData actorByActorIndex = GameFlowData.Get().FindActorByActorIndex((int) invalidActorIndex);
//                    if (actorByActorIndex != null)
//                        HitActorsToDeltaHP[actorByActorIndex] = num11;
                }
            }

//            this.u0008u000E();
        }
    }
}
