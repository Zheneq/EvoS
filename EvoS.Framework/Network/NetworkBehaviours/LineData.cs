using System.Collections.Generic;
using System.Drawing;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Game;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [SerializedMonoBehaviour("LineData")]
    public class LineData : NetworkBehaviour
    {
        public float LastAllyMovementChange { get; set; }
        private LineInstance m_movementLine;
        private LineInstance m_movementSnaredLine;
        private LineInstance m_movementLostLine;

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            return false;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
        }

        public void OnResolveStart()
        {
//            this.HideAllyMovementLinesIfResolving();
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            stream.AlignTo();
            LastAllyMovementChange = stream.ReadSingle();
        }

        public override string ToString()
        {
            return $"{nameof(LineData)}(" +
                   $"{nameof(LastAllyMovementChange)}: {LastAllyMovementChange}" +
                   ")";
        }

        public void ClearAllLines()
        {
            ClearLine(ref m_movementLine);
            ClearLine(ref m_movementSnaredLine);
            ClearLine(ref m_movementLostLine);
        }

        private void ClearLine(ref LineInstance theLine)
        {
            if (theLine == null)
                return;
            theLine.m_positions.Clear();
            if (theLine.m_lineObject != null)
                theLine.DestroyLineObject();
            theLine = null;
            if (!EvoSGameConfig.NetworkIsServer)
                return;
            MarkForSerializationUpdate();
        }

        private void MarkForSerializationUpdate()
        {
            // TODO
        }

        public void OnDeserializedData(LineInstance movementLine, sbyte numNodesInSnaredLine)
        {
            ClearAllLines();
            m_movementLine = movementLine;
            OnDeSerializeNumNodesInSnaredLine(numNodesInSnaredLine);
//            DrawLine(ref m_movementSnaredLine, 0.4f);
//            DrawLine(ref m_movementLine, 0.4f);
//            DrawLine(ref m_movementLostLine, 0.2f);
//            HideAllyMovementLinesIfResolving();
//            if (!(SinglePlayerManager.Get() != null) ||
//                !(SinglePlayerManager.Get().GetLocalPlayer() != null))
//                return;
//            SinglePlayerManager.Get().OnActorMovementChanged(SinglePlayerManager.Get().GetLocalPlayer());
        }

        public static void SerializeLine(LineInstance line, NetworkWriter writer)
        {
            var count = (sbyte) line.m_positions.Count;
            writer.Write(count);
            var isChasing = line.isChasing;
            writer.Write(isChasing);
            for (var index = 0; index < (int) count; ++index)
            {
                var x = line.m_positions[index].X;
                var y = line.m_positions[index].Y;
                byte num1 = 0;
                byte num2 = 0;
                if (x >= 0 && x <= byte.MaxValue)
                    num1 = (byte) x;
                if (y >= 0 && y <= byte.MaxValue)
                    num2 = (byte) y;
                writer.Write(num1);
                writer.Write(num2);
            }
        }

        public static LineInstance DeSerializeLine(NetworkReader reader)
        {
            var lineInstance = new LineInstance();
            var num1 = reader.ReadSByte();
            var flag = reader.ReadBoolean();
            lineInstance.isChasing = flag;
            lineInstance.m_positions.Clear();
            for (var index = 0; index < (int) num1; ++index)
            {
                var num2 = reader.ReadByte();
                var num3 = reader.ReadByte();
                var gridPos = new GridPos(
                    num2,
                    num3,
                    -1 // TODO (int) Board.\u000E().\u000E(gridPos.x, gridPos.y)
                );
                lineInstance.m_positions.Add(gridPos);
            }

            return lineInstance;
        }

        public void OnDeSerializeNumNodesInSnaredLine(sbyte numNodesInSnaredLine)
        {
            if (numNodesInSnaredLine > 0)
            {
                if (m_movementLine == null)
                    return;
                numNodesInSnaredLine = (sbyte) Mathf.Min(numNodesInSnaredLine,
                    (sbyte) m_movementLine.m_positions.Count);
                if (m_movementSnaredLine == null)
                    m_movementSnaredLine = new LineInstance();
                if (m_movementLostLine == null)
                    m_movementLostLine = new LineInstance();
                m_movementSnaredLine.m_positions.Clear();
                m_movementSnaredLine.isChasing = m_movementLine.isChasing;
                m_movementLostLine.m_positions.Clear();
                m_movementLostLine.isChasing = m_movementLine.isChasing;
                for (int index = 0; index < (int) numNodesInSnaredLine; ++index)
                    m_movementSnaredLine.m_positions.Add(m_movementLine.m_positions[index]);
                for (int index = Mathf.Max(0, numNodesInSnaredLine - 1);
                    index < m_movementLine.m_positions.Count;
                    ++index)
                    m_movementLostLine.m_positions.Add(m_movementLine.m_positions[index]);
            }
            else
            {
                ClearLine(ref m_movementSnaredLine);
                if (m_movementLine != null)
                {
                    if (m_movementLostLine == null)
                        m_movementLostLine = new LineInstance();
                    m_movementLostLine.m_positions.Clear();
                    m_movementLostLine.isChasing = m_movementLine.isChasing;
                    for (int index = 0; index < m_movementLine.m_positions.Count; ++index)
                        m_movementLostLine.m_positions.Add(m_movementLine.m_positions[index]);
                }
                else
                    ClearLine(ref m_movementLostLine);
            }
        }

        public class LineInstance
        {
            public List<GridPos> m_positions;
            public bool isChasing;
            public GameObject m_lineObject;
            public MovementPathStart m_movePathStart;
            public Color m_currentColor;

            public LineInstance()
            {
                m_positions = new List<GridPos>();
            }

            public override string ToString()
            {
                var empty = string.Empty;
                var str = m_lineObject != null
                    ? empty + "(created lineObject)\n"
                    : empty + "(no lineObject)\n";
                for (var index = 0; index < m_positions.Count; ++index)
                {
                    var position = m_positions[index];
                    if (index == 0)
                    {
                        str += $"({(object) position.X}, {(object) position.Y})";
                    }
                    else if (index == m_positions.Count - 1)
                    {
                        str += $",\n({(object) position.X}, {(object) position.Y}) (end)";
                    }
                    else
                        str += $",\n({(object) position.X}, {(object) position.Y})";
                }

                return str;
            }

            public void DestroyLineObject()
            {
                m_movePathStart = null;

//                UnityEngine.Object.Destroy(m_lineObject);
                m_lineObject = null;
            }
        }
    }
}
