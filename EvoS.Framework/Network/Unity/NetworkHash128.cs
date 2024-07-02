using System;

namespace EvoS.Framework.Network.Unity
{
    [Serializable]
    public struct NetworkHash128
    {
        public byte i0;
        public byte i1;
        public byte i2;
        public byte i3;
        public byte i4;
        public byte i5;
        public byte i6;
        public byte i7;
        public byte i8;
        public byte i9;
        public byte i10;
        public byte i11;
        public byte i12;
        public byte i13;
        public byte i14;
        public byte i15;

        public NetworkHash128(byte[] hash)
        {
            i0 = hash[0];
            i1 = hash[1];
            i2 = hash[2];
            i3 = hash[3];
            i4 = hash[4];
            i5 = hash[5];
            i6 = hash[6];
            i7 = hash[7];
            i8 = hash[8];
            i9 = hash[9];
            i10 = hash[10];
            i11 = hash[11];
            i12 = hash[12];
            i13 = hash[13];
            i14 = hash[14];
            i15 = hash[15];
        }

        public void Reset()
        {
            i0 = 0;
            i1 = 0;
            i2 = 0;
            i3 = 0;
            i4 = 0;
            i5 = 0;
            i6 = 0;
            i7 = 0;
            i8 = 0;
            i9 = 0;
            i10 = 0;
            i11 = 0;
            i12 = 0;
            i13 = 0;
            i14 = 0;
            i15 = 0;
        }

        public bool IsValid()
        {
            return (i0 | i1 | i2 | i3 | i4 | i5 |
                    i6 | i7 | i8 | i9 | i10 | i11 |
                    i12 | i13 | i14 | i15) != 0;
        }

        private static int HexToNumber(char c)
        {
            if (c >= '0' && c <= '9')
                return c - 48;
            if (c >= 'a' && c <= 'f')
                return c - 97 + 10;
            if (c >= 'A' && c <= 'F')
                return c - 65 + 10;
            return 0;
        }

        public static NetworkHash128 Parse(string text)
        {
            int length = text.Length;
            if (length < 32)
            {
                string str = "";
                for (int index = 0; index < 32 - length; ++index)
                    str += "0";
                text = str + text;
            }

            NetworkHash128 networkHash128;
            networkHash128.i0 = (byte) (HexToNumber(text[0]) * 16 + HexToNumber(text[1]));
            networkHash128.i1 = (byte) (HexToNumber(text[2]) * 16 + HexToNumber(text[3]));
            networkHash128.i2 = (byte) (HexToNumber(text[4]) * 16 + HexToNumber(text[5]));
            networkHash128.i3 = (byte) (HexToNumber(text[6]) * 16 + HexToNumber(text[7]));
            networkHash128.i4 = (byte) (HexToNumber(text[8]) * 16 + HexToNumber(text[9]));
            networkHash128.i5 =
                (byte) (HexToNumber(text[10]) * 16 + HexToNumber(text[11]));
            networkHash128.i6 =
                (byte) (HexToNumber(text[12]) * 16 + HexToNumber(text[13]));
            networkHash128.i7 =
                (byte) (HexToNumber(text[14]) * 16 + HexToNumber(text[15]));
            networkHash128.i8 =
                (byte) (HexToNumber(text[16]) * 16 + HexToNumber(text[17]));
            networkHash128.i9 =
                (byte) (HexToNumber(text[18]) * 16 + HexToNumber(text[19]));
            networkHash128.i10 =
                (byte) (HexToNumber(text[20]) * 16 + HexToNumber(text[21]));
            networkHash128.i11 =
                (byte) (HexToNumber(text[22]) * 16 + HexToNumber(text[23]));
            networkHash128.i12 =
                (byte) (HexToNumber(text[24]) * 16 + HexToNumber(text[25]));
            networkHash128.i13 =
                (byte) (HexToNumber(text[26]) * 16 + HexToNumber(text[27]));
            networkHash128.i14 =
                (byte) (HexToNumber(text[28]) * 16 + HexToNumber(text[29]));
            networkHash128.i15 =
                (byte) (HexToNumber(text[30]) * 16 + HexToNumber(text[31]));
            return networkHash128;
        }

        public bool IsZero()
        {
            return i0 == 0 && i1 == 0 && i2 == 0 && i3 == 0 && i4 == 0 && i5 == 0 && i6 == 0 && i7 == 0 && i8 == 0 && i9 == 0 && i10 == 0 && i11 == 0 && i12 == 0 && i13 == 0 && i14 == 0 && i15 == 0;;
        }

        public override string ToString()
        {
            return
                $"{(object) i0:x2}{(object) i1:x2}{(object) i2:x2}{(object) i3:x2}{(object) i4:x2}{(object) i5:x2}{(object) i6:x2}{(object) i7:x2}{(object) i8:x2}{(object) i9:x2}{(object) i10:x2}{(object) i11:x2}{(object) i12:x2}{(object) i13:x2}{(object) i14:x2}{(object) i15:x2}";
        }

        public byte[] ToByteArray() => new[] {i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15};
    }
}
