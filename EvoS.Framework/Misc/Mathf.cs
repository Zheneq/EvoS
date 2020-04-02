using System;
using System.ComponentModel;
using System.Numerics;

namespace EvoS.Framework.Misc
{
    public class Mathf
    {
        public const float PI = 3.141593f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = 0.01745329f;
        public const float Rad2Deg = 57.29578f;
        public static volatile float FloatMinNormal = 1.17549435E-38f;
        public static volatile float FloatMinDenormal = float.Epsilon;
        public static bool IsFlushToZeroEnabled = FloatMinDenormal == 0f;
        public static readonly float Epsilon = (!IsFlushToZeroEnabled) ? FloatMinDenormal : FloatMinNormal;

        public static float Sin(float f)
        {
            return (float) Math.Sin(f);
        }

        public static float Cos(float f)
        {
            return (float) Math.Cos(f);
        }

        public static float Tan(float f)
        {
            return (float) Math.Tan(f);
        }

        public static float Asin(float f)
        {
            return (float) Math.Asin(f);
        }

        public static float Acos(float f)
        {
            return (float) Math.Acos(f);
        }

        public static float Atan(float f)
        {
            return (float) Math.Atan(f);
        }

        public static float Atan2(float y, float x)
        {
            return (float) Math.Atan2(y, x);
        }

        public static float Sqrt(float f)
        {
            return (float) Math.Sqrt(f);
        }

        public static float Abs(float f)
        {
            return Math.Abs(f);
        }

        public static int Abs(int value)
        {
            return Math.Abs(value);
        }

        public static float Min(float a, float b)
        {
            return (double) a >= (double) b ? b : a;
        }

        public static float Min(params float[] values)
        {
            int length = values.Length;
            if (length == 0)
                return 0.0f;
            float num = values[0];
            for (int index = 1; index < length; ++index)
            {
                if (values[index] < (double) num)
                    num = values[index];
            }

            return num;
        }

        public static int Min(int a, int b)
        {
            return a >= b ? b : a;
        }

        public static int Min(params int[] values)
        {
            int length = values.Length;
            if (length == 0)
                return 0;
            int num = values[0];
            for (int index = 1; index < length; ++index)
            {
                if (values[index] < num)
                    num = values[index];
            }

            return num;
        }

        public static float Max(float a, float b)
        {
            return (double) a <= (double) b ? b : a;
        }

        public static float Max(params float[] values)
        {
            int length = values.Length;
            if (length == 0)
                return 0.0f;
            float num = values[0];
            for (int index = 1; index < length; ++index)
            {
                if (values[index] > (double) num)
                    num = values[index];
            }

            return num;
        }

        public static int Max(int a, int b)
        {
            return a <= b ? b : a;
        }

        public static int Max(params int[] values)
        {
            int length = values.Length;
            if (length == 0)
                return 0;
            int num = values[0];
            for (int index = 1; index < length; ++index)
            {
                if (values[index] > num)
                    num = values[index];
            }

            return num;
        }

        public static float Pow(float f, float p)
        {
            return (float) Math.Pow(f, p);
        }

        public static float Exp(float power)
        {
            return (float) Math.Exp(power);
        }

        public static float Log(float f, float p)
        {
            return (float) Math.Log(f, p);
        }

        public static float Log(float f)
        {
            return (float) Math.Log(f);
        }

        public static float Log10(float f)
        {
            return (float) Math.Log10(f);
        }

        public static float Ceil(float f)
        {
            return (float) Math.Ceiling(f);
        }

        public static float Floor(float f)
        {
            return (float) Math.Floor(f);
        }

        public static float Round(float f)
        {
            return (float) Math.Round(f);
        }

        public static int CeilToInt(float f)
        {
            return (int) Math.Ceiling(f);
        }

        public static int FloorToInt(float f)
        {
            return (int) Math.Floor(f);
        }

        public static int RoundToInt(float f)
        {
            return (int) Math.Round(f);
        }

        public static float Sign(float f)
        {
            return (double) f < 0.0 ? -1f : 1f;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < (double) min)
                value = min;
            else if (value > (double) max)
                value = max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;
            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0.0)
                return 0.0f;
            if (value > 1.0)
                return 1f;
            return value;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float LerpAngle(float a, float b, float t)
        {
            float num = Repeat(b - a, 360f);
            if (num > 180.0)
                num -= 360f;
            return a + num * Clamp01(t);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= (double) maxDelta)
                return target;
            return current + Sign(target - current) * maxDelta;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float num = DeltaAngle(current, target);
            if (-(double) maxDelta < num && num < (double) maxDelta)
                return target;
            target = current + num;
            return MoveTowards(current, target, maxDelta);
        }

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = (float) (-2.0 * t * t * t + 3.0 * t * t);
            return (float) (to * (double) t + from * (1.0 - t));
        }

        public static float Gamma(float value, float absmax, float gamma)
        {
            bool flag = false;
            if (value < 0.0)
                flag = true;
            float num1 = Abs(value);
            if (num1 > (double) absmax)
                return !flag ? num1 : -num1;
            float num2 = Pow(num1 / absmax, gamma) * absmax;
            return !flag ? num2 : -num2;
        }

        public static bool Approximately(float a, float b)
        {
            return Abs(b - a) <
                   (double)Max(1E-06f * Max(Abs(a), Abs(b)), Mathf.Epsilon * 8f);
        }

        public static float SmoothDamp(
            float current,
            float target,
            ref float currentVelocity,
            float smoothTime,
            [DefaultValue("Mathf.Infinity")] float maxSpeed,
            [DefaultValue("Time.deltaTime")] float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num1 = 2f / smoothTime;
            float num2 = num1 * deltaTime;
            float num3 = (float) (1.0 / (1.0 + num2 + 0.479999989271164 * num2 * num2 +
                                         0.234999999403954 * num2 * num2 * num2));
            float num4 = current - target;
            float num5 = target;
            float max = maxSpeed * smoothTime;
            float num6 = Clamp(num4, -max, max);
            target = current - num6;
            float num7 = (currentVelocity + num1 * num6) * deltaTime;
            currentVelocity = (currentVelocity - num1 * num7) * num3;
            float num8 = target + (num6 + num7) * num3;
            if (num5 - (double) current > 0.0 == num8 > (double) num5)
            {
                num8 = num5;
                currentVelocity = (num8 - num5) / deltaTime;
            }

            return num8;
        }

        public static float SmoothDampAngle(
            float current,
            float target,
            ref float currentVelocity,
            float smoothTime,
            [DefaultValue("Mathf.Infinity")] float maxSpeed,
            [DefaultValue("Time.deltaTime")] float deltaTime)
        {
            target = current + DeltaAngle(current, target);
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        public static float Repeat(float t, float length)
        {
            return t - Floor(t / length) * length;
        }

        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Abs(t - length);
        }

        public static float InverseLerp(float a, float b, float value)
        {
            if (a != (double) b)
                return Clamp01((float) ((value - (double) a) / (b - (double) a)));
            return 0.0f;
        }

        public static float DeltaAngle(float current, float target)
        {
            float num = Repeat(target - current, 360f);
            if (num > 180.0)
                num -= 360f;
            return num;
        }

        internal static bool LineIntersection(
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            Vector2 p4,
            ref Vector2 result)
        {
            float num1 = p2.X - p1.X;
            float num2 = p2.Y - p1.Y;
            float num3 = p4.X - p3.X;
            float num4 = p4.Y - p3.Y;
            float num5 = (float) (num1 * (double) num4 - num2 * (double) num3);
            if (num5 == 0.0)
                return false;
            float num6 = p3.X - p1.X;
            float num7 = p3.Y - p1.Y;
            float num8 = (float) (num6 * (double) num4 - num7 * (double) num3) / num5;
            result = new Vector2(p1.X + num8 * num1, p1.Y + num8 * num2);
            return true;
        }

        internal static bool LineSegmentIntersection(
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            Vector2 p4,
            ref Vector2 result)
        {
            float num1 = p2.X - p1.X;
            float num2 = p2.Y - p1.Y;
            float num3 = p4.X - p3.X;
            float num4 = p4.Y - p3.Y;
            float num5 = (float) (num1 * (double) num4 - num2 * (double) num3);
            if (num5 == 0.0)
                return false;
            float num6 = p3.X - p1.X;
            float num7 = p3.Y - p1.Y;
            float num8 = (float) (num6 * (double) num4 - num7 * (double) num3) / num5;
            if (num8 < 0.0 || num8 > 1.0)
                return false;
            float num9 = (float) (num6 * (double) num2 - num7 * (double) num1) / num5;
            if (num9 < 0.0 || num9 > 1.0)
                return false;
            result = new Vector2(p1.X + num8 * num1, p1.Y + num8 * num2);
            return true;
        }

        internal static long RandomToLong(Random r)
        {
            byte[] buffer = new byte[8];
            r.NextBytes(buffer);
            return (long) BitConverter.ToUInt64(buffer, 0) & long.MaxValue;
        }
    }
}
