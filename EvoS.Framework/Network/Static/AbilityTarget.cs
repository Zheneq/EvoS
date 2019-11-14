using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    public class AbilityTarget
    {
        [JsonIgnore]
        private GridPos m_gridPos;
        [JsonIgnore]
        private Vector3 m_dir;
        [JsonIgnore]
        private Vector3 m_freePos;

        private AbilityTarget()
        {
        }

        private AbilityTarget(GridPos targetPos, Vector3 freePos, Vector3 dir)
        {
            m_freePos = freePos;
            m_gridPos = targetPos;
            m_dir = dir;
        }

        public Vector3 FreePos => m_freePos;
        public GridPos GridPos => m_gridPos;
        public Vector3 AimDirection => m_dir;

        public void SetPosAndDir(GridPos gridPos, Vector3 freePos, Vector3 dir)
        {
            m_gridPos = gridPos;
            m_freePos = freePos;
            m_dir = dir;
        }

        public void SetValuesFromBoardSquare(BoardSquare targetSquare, Vector3 currentWorldPos)
        {
            var dir = targetSquare.ToVector3() - currentWorldPos;
            dir.Y = 0.0f;
            Vector3.Normalize(dir);
            SetPosAndDir(targetSquare.GridPos, targetSquare.ToVector3(), dir);
        }

        public void OnSerializeHelper(IBitStream stream)
        {
            var dir = m_dir;
            var freePos = m_freePos;
            m_gridPos.OnSerializeHelper(stream);
            stream.Serialize(ref dir);
            stream.Serialize(ref freePos);
            m_dir = dir;
            m_freePos = freePos;
        }

        public Vector3 GetWorldGridPos()
        {
            return new Vector3(GridPos.worldX, GridPos.Height, GridPos.worldY);
        }

        public AbilityTarget GetCopy()
        {
            return new AbilityTarget(GridPos, FreePos, AimDirection);
        }

        public static AbilityTarget CreateAbilityTarget(IBitStream stream)
        {
            AbilityTarget abilityTarget = new AbilityTarget();
            abilityTarget.OnSerializeHelper(stream);
            return abilityTarget;
        }

        public static List<AbilityTarget> AbilityTargetList(AbilityTarget onlyTarget)
        {
            return new List<AbilityTarget> {onlyTarget};
        }

        public static void SerializeAbilityTargetList(List<AbilityTarget> targetList, NetworkWriter stream)
        {
            SerializeAbilityTargetList(targetList, new NetworkWriterAdapter(stream));
        }

        public static void SerializeAbilityTargetList(List<AbilityTarget> targetList, IBitStream stream)
        {
            var count = checked((byte) targetList.Count);
            stream.Serialize(ref count);
            foreach (var target in targetList)
            {
                var x = checked((short) target.GridPos.X);
                var y = checked((short) target.GridPos.Y);
                var dir = target.m_dir;
                var freePos = target.m_freePos;
                stream.Serialize(ref x);
                stream.Serialize(ref y);
                stream.Serialize(ref dir);
                stream.Serialize(ref freePos);
            }
        }

        public static List<AbilityTarget> DeSerializeAbilityTargetList(
            NetworkReader stream)
        {
            return DeSerializeAbilityTargetList(new NetworkReaderAdapter(stream));
        }

        public static List<AbilityTarget> DeSerializeAbilityTargetList(
            IBitStream stream)
        {
            var abilityTargetList = new List<AbilityTarget>();
            byte num1 = 0;
            stream.Serialize(ref num1);
            for (var index = 0; index < (int) num1; ++index)
            {
                short num2 = -1;
                short num3 = -1;
                var zero1 = Vector3.Zero;
                var zero2 = Vector3.Zero;
                stream.Serialize(ref num2);
                stream.Serialize(ref num3);
                stream.Serialize(ref zero1);
                stream.Serialize(ref zero2);
                var abilityTarget = new AbilityTarget(new GridPos(
                    num2,
                    num3,
                    -1 // TODO: (int) Board.\u000E().\u000E((int) num2, (int) num3)
                ), zero2, zero1);
                abilityTargetList.Add(abilityTarget);
            }

            return abilityTargetList;
        }

        public string GetDebugString()
        {
            return $"[GridPos= {m_gridPos.ToStringWithCross()} | FreePos= ({m_freePos.X}, {m_freePos.Z})]";
        }
    }
}
