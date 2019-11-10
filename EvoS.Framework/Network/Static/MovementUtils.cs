using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    public static class MovementUtils
    {
        public static BoardSquarePathInfo DeSerializePath(Component context, NetworkReader reader)
        {
            var boardSquarePathInfo1 = new BoardSquarePathInfo();
            var boardSquarePathInfo2 = boardSquarePathInfo1;
            BoardSquarePathInfo boardSquarePathInfo3 = null;
            float num1 = 0.0f;
            sbyte num2 = 0;
            sbyte num3 = 0;
            float num4 = 0.0f;
            float num5 = 0.0f;
            float num6 = 0.0f;
            bool out0_1 = false;
            bool out1_1 = false;
            bool flag;
            if (flag = reader.ReadBoolean())
            {
                num4 = reader.ReadSingle();
                num5 = reader.ReadSingle();
                num6 = reader.ReadSingle();
            }

            bool out2 = !flag;
            bool out3 = false;
            bool out4 = false;
            bool out5 = false;
            bool out0_2 = false;
            bool out1_2 = false;
            while (!out2)
            {
                byte num7 = reader.ReadByte();
                byte num8 = reader.ReadByte();
                sbyte num9 = reader.ReadSByte();
                int num10;
                switch (num9)
                {
                    case 0:
                    case 3:
                        num10 = 1;
                        break;
                    default:
                        num10 = num9 == (sbyte) 1 ? 1 : 0;
                        break;
                }

                if (num10 == 0)
                {
                    num2 = reader.ReadSByte();
                    num3 = reader.ReadSByte();
                }

                var bitField1 = reader.ReadByte();
                var bitField2 = reader.ReadByte();
                var out6 = false;
                var out7 = false;
                ServerClientUtils.GetBoolsFromBitfield(bitField1, out out0_1, out out1_1, out out2, out out3, out out4,
                    out out5, out out6, out out7);
                ServerClientUtils.GetBoolsFromBitfield(bitField2, out out0_2, out out1_2);
                var num11 = num4;
                var num12 = num5;
                if (out6)
                    num11 = reader.ReadSingle();
                if (out7)
                    num12 = reader.ReadSingle();
                var boardSquare = context.Board.method_10(num7, num8);
                if (boardSquare == null)
                    Log.Print(LogType.Error,
                        "Failed to find square from index [" + num7 + ", " + num8 + "] during serialization of path");
                boardSquarePathInfo2.square = boardSquare;
                boardSquarePathInfo2.moveCost = num1;
                boardSquarePathInfo2.heuristicCost = 0.0f;
                boardSquarePathInfo2.connectionType = (BoardSquarePathInfo.ConnectionType) num9;
                boardSquarePathInfo2.chargeCycleType = (BoardSquarePathInfo.ChargeCycleType) num2;
                boardSquarePathInfo2.chargeEndType = (BoardSquarePathInfo.ChargeEndType) num3;
                boardSquarePathInfo2.segmentMovementSpeed = num11;
                boardSquarePathInfo2.segmentMovementDuration = num12;
                boardSquarePathInfo2.m_reverse = out0_1;
                boardSquarePathInfo2.m_unskippable = out1_1;
                boardSquarePathInfo2.m_visibleToEnemies = out3;
                boardSquarePathInfo2.m_updateLastKnownPos = out4;
                boardSquarePathInfo2.m_moverDiesHere = out5;
                boardSquarePathInfo2.m_moverClashesHere = out0_2;
                boardSquarePathInfo2.m_moverBumpedFromClash = out1_2;
                boardSquarePathInfo2.prev = boardSquarePathInfo3;
                if (boardSquarePathInfo3 != null)
                    boardSquarePathInfo3.next = boardSquarePathInfo2;
                if (!out2)
                {
                    boardSquarePathInfo3 = boardSquarePathInfo2;
                    boardSquarePathInfo2 = new BoardSquarePathInfo();
                }
            }

            boardSquarePathInfo1.moveCost = num6;
            boardSquarePathInfo1.CalcAndSetMoveCostToEnd(context);
            return boardSquarePathInfo1;
        }

        public static BoardSquarePathInfo DeSerializePath(Component context, byte[] data)
        {
            if (data == null)
                return null;
            return DeSerializePath(context, new NetworkReader(data));
        }

        public static bool ArePathSegmentsEquivalent_FromBeginning(
            BoardSquarePathInfo pathA,
            BoardSquarePathInfo pathB)
        {
            if (pathA == null || pathB == null || (pathA.square == null || pathB.square == null) ||
                pathA.square != pathB.square)
                return false;
            var flag = true;
            var boardSquarePathInfo1 = pathA;
            var boardSquarePathInfo2 = pathB;
            int num;
            for (num = 0; flag && num < 100; ++num)
            {
                boardSquarePathInfo1 = boardSquarePathInfo1.prev;
                boardSquarePathInfo2 = boardSquarePathInfo2.prev;
                if (boardSquarePathInfo1 != null || boardSquarePathInfo2 != null)
                {
                    if (boardSquarePathInfo1 == null && boardSquarePathInfo2 != null)
                        flag = false;
                    else if (boardSquarePathInfo1 != null && boardSquarePathInfo2 == null)
                        flag = false;
                    else if (boardSquarePathInfo1.square != boardSquarePathInfo2.square)
                        flag = false;
                }
                else
                    break;
            }

            if (num >= 100)
                Log.Print(LogType.Error,
                    "Infinite/circular (or maybe just massive) loop detected in ArePathSegmentsEquivalent_FromBeginning.");
            return flag;
        }


        internal static BoardSquarePathInfo DeSerializeLightweightPath(Component context, IBitStream stream)
        {
            if (stream == null)
            {
                Log.Print(LogType.Error, "Calling DeSerializeLightweightPath with a null stream");
                return null;
            }

            BoardSquarePathInfo boardSquarePathInfo1;
            if (stream.isWriting)
            {
                Log.Print(LogType.Error, "Trying to deserialize a (lightweight) path while writing.");
                boardSquarePathInfo1 = null;
            }
            else
            {
                sbyte num1 = 0;
                stream.Serialize(ref num1);
                if (num1 <= 0)
                {
                    boardSquarePathInfo1 = null;
                }
                else
                {
                    sbyte num2 = 0;
                    stream.Serialize(ref num2);
                    boardSquarePathInfo1 = null;
                    BoardSquarePathInfo boardSquarePathInfo2 = null;
                    for (int index = 0; index < (int) num1; ++index)
                    {
                        short num3 = -1;
                        short num4 = -1;
                        stream.Serialize(ref num3);
                        stream.Serialize(ref num4);
                        var boardSquare = num3 != (short) -1 || num4 != (short) -1
                            ? context.Board.method_10(num3, num4)
                            : null;
                        var boardSquarePathInfo3 = new BoardSquarePathInfo();
                        boardSquarePathInfo3.square = boardSquare;
                        boardSquarePathInfo3.prev = boardSquarePathInfo2;
                        if (boardSquarePathInfo2 != null)
                            boardSquarePathInfo2.next = boardSquarePathInfo3;
                        boardSquarePathInfo2 = boardSquarePathInfo3;
                        if (index == 0)
                            boardSquarePathInfo1 = boardSquarePathInfo3;
                    }

                    BoardSquarePathInfo boardSquarePathInfo4 = boardSquarePathInfo1;
                    for (int index = 0; index < (int) num2; ++index)
                    {
                        short num3 = -1;
                        short num4 = -1;
                        stream.Serialize(ref num3);
                        stream.Serialize(ref num4);
                        var boardSquare = num3 != (short) -1 || num4 != (short) -1
                            ? context.Board.method_10(num3, num4)
                            : null;
                        var boardSquarePathInfo3 = new BoardSquarePathInfo();
                        boardSquarePathInfo3.square = boardSquare;
                        boardSquarePathInfo3.next = boardSquarePathInfo4;
                        boardSquarePathInfo4.prev = boardSquarePathInfo3;
                        boardSquarePathInfo4 = boardSquarePathInfo3;
                    }
                }
            }

            return boardSquarePathInfo1;
        }
    }
}
