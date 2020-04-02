using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    public static class MovementUtils
    {
        internal static void SerializePath(BoardSquarePathInfo path, NetworkWriter writer)
        {
            bool flag = path != null;
            float b = 8f;
            float b2 = 0f;
            writer.Write(flag);
            if (flag)
            {
                b = path.segmentMovementSpeed;
                b2 = path.segmentMovementDuration;
                writer.Write(path.segmentMovementSpeed);
                writer.Write(path.segmentMovementDuration);
                writer.Write(path.moveCost);
            }
            for (BoardSquarePathInfo boardSquarePathInfo = path; boardSquarePathInfo != null; boardSquarePathInfo = boardSquarePathInfo.next)
            {
                byte value = 0;
                if (boardSquarePathInfo.square.X <= 255)
                {
                    value = (byte)boardSquarePathInfo.square.X;
                }
                else // if (Application.isEditor)
                {
                    Log.Print(LogType.Error, "MovementUtils.SerializePath, x coordinate value too large for byte");
                }
                byte value2 = 0;
                if (boardSquarePathInfo.square.Y <= 255)
                {
                    value2 = (byte)boardSquarePathInfo.square.Y;
                }
                else // if (Application.isEditor)
                {
                    Log.Print(LogType.Error, "MovementUtils.SerializePath, y coordinate value too large for byte");
                }
                sbyte value3 = (sbyte)boardSquarePathInfo.connectionType;
                sbyte value4 = (sbyte)boardSquarePathInfo.chargeCycleType;
                sbyte value5 = (sbyte)boardSquarePathInfo.chargeEndType;
                bool reverse = boardSquarePathInfo.m_reverse;
                bool unskippable = boardSquarePathInfo.m_unskippable;
                bool b3 = boardSquarePathInfo.next == null;
                bool visibleToEnemies = boardSquarePathInfo.m_visibleToEnemies;
                bool updateLastKnownPos = boardSquarePathInfo.m_updateLastKnownPos;
                bool moverDiesHere = boardSquarePathInfo.m_moverDiesHere;
                bool flag2 = !Mathf.Approximately(boardSquarePathInfo.segmentMovementSpeed, b);
                bool flag3 = !Mathf.Approximately(boardSquarePathInfo.segmentMovementDuration, b2);
                bool moverClashesHere = boardSquarePathInfo.m_moverClashesHere;
                bool moverBumpedFromClash = boardSquarePathInfo.m_moverBumpedFromClash;
                byte value6 = ServerClientUtils.CreateBitfieldFromBools(reverse, unskippable, b3, visibleToEnemies, updateLastKnownPos, moverDiesHere, flag2, flag3);
                byte value7 = ServerClientUtils.CreateBitfieldFromBools(moverClashesHere, moverBumpedFromClash, false, false, false, false, false, false);
                writer.Write(value);
                writer.Write(value2);
                writer.Write(value3);
                if (boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Run && boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Vault && boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Knockback)
                {
                    writer.Write(value4);
                    writer.Write(value5);
                }
                writer.Write(value6);
                writer.Write(value7);
                if (flag2)
                {
                    float segmentMovementSpeed = boardSquarePathInfo.segmentMovementSpeed;
                    writer.Write(segmentMovementSpeed);
                }
                if (flag3)
                {
                    float segmentMovementDuration = boardSquarePathInfo.segmentMovementDuration;
                    writer.Write(segmentMovementDuration);
                }
            }
        }

        internal static void SerializePath(BoardSquarePathInfo path, IBitStream stream)
        {
            if (stream.isReading)
            {
                Log.Print(LogType.Error, "Trying to serialize a path while reading");
            }
            else
            {
                bool flag = path != null;
                float b = 8f;
                float b2 = 0f;
                stream.Serialize(ref flag);
                if (flag)
                {
                    b = path.segmentMovementSpeed;
                    b2 = path.segmentMovementDuration;
                    stream.Serialize(ref path.segmentMovementSpeed);
                    stream.Serialize(ref path.segmentMovementDuration);
                    stream.Serialize(ref path.moveCost);
                }
                for (BoardSquarePathInfo boardSquarePathInfo = path; boardSquarePathInfo != null; boardSquarePathInfo = boardSquarePathInfo.next)
                {
                    byte b3 = 0;
                    if (boardSquarePathInfo.square.X <= 255)
                    {
                        b3 = (byte)boardSquarePathInfo.square.X;
                    }
                    else // if (Application.isEditor)
                    {
                        Log.Print(LogType.Error, "MovementUtils.SerializePath, x coordinate value too large for byte");
                    }
                    byte b4 = 0;
                    if (boardSquarePathInfo.square.Y <= 255)
                    {
                        b4 = (byte)boardSquarePathInfo.square.Y;
                    }
                    else // if (Application.isEditor)
                    {
                        Log.Print(LogType.Error, "MovementUtils.SerializePath, y coordinate value too large for byte");
                    }
                    sbyte b5 = (sbyte)boardSquarePathInfo.connectionType;
                    sbyte b6 = (sbyte)boardSquarePathInfo.chargeCycleType;
                    sbyte b7 = (sbyte)boardSquarePathInfo.chargeEndType;
                    bool reverse = boardSquarePathInfo.m_reverse;
                    bool unskippable = boardSquarePathInfo.m_unskippable;
                    bool b8 = boardSquarePathInfo.next == null;
                    bool visibleToEnemies = boardSquarePathInfo.m_visibleToEnemies;
                    bool updateLastKnownPos = boardSquarePathInfo.m_updateLastKnownPos;
                    bool moverDiesHere = boardSquarePathInfo.m_moverDiesHere;
                    bool flag2 = !Mathf.Approximately(boardSquarePathInfo.segmentMovementSpeed, b);
                    bool flag3 = !Mathf.Approximately(boardSquarePathInfo.segmentMovementDuration, b2);
                    bool moverClashesHere = boardSquarePathInfo.m_moverClashesHere;
                    bool moverBumpedFromClash = boardSquarePathInfo.m_moverBumpedFromClash;
                    byte b9 = ServerClientUtils.CreateBitfieldFromBools(reverse, unskippable, b8, visibleToEnemies, updateLastKnownPos, moverDiesHere, flag2, flag3);
                    byte b10 = ServerClientUtils.CreateBitfieldFromBools(moverClashesHere, moverBumpedFromClash, false, false, false, false, false, false);
                    stream.Serialize(ref b3);
                    stream.Serialize(ref b4);
                    stream.Serialize(ref b5);
                    if (boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Run && boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Vault && boardSquarePathInfo.connectionType != BoardSquarePathInfo.ConnectionType.Knockback)
                    {
                        stream.Serialize(ref b6);
                        stream.Serialize(ref b7);
                    }
                    stream.Serialize(ref b9);
                    stream.Serialize(ref b10);
                    if (flag2)
                    {
                        float segmentMovementSpeed = boardSquarePathInfo.segmentMovementSpeed;
                        stream.Serialize(ref segmentMovementSpeed);
                    }
                    if (flag3)
                    {
                        float segmentMovementDuration = boardSquarePathInfo.segmentMovementDuration;
                        stream.Serialize(ref segmentMovementDuration);
                    }
                }
            }
        }

        internal static byte[] SerializePath(BoardSquarePathInfo path)
        {
            if (path == null)
            {
                return null;
            }
            NetworkWriter networkWriter = new NetworkWriter();
            MovementUtils.SerializePath(path, networkWriter);
            return networkWriter.ToArray();
        }

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

        public static float RoundToNearestHalf(float val)
        {
            float f = val * 2f;
            float num = Mathf.Round(f);
            return num / 2f;
        }

        public static bool CanStopOnSquare(BoardSquare square)
        {
            return square != null && square.Height >= 0;
        }
    }
}
