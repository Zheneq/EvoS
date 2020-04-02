using System;
using System.Numerics;
using EvoS.Framework.Network.NetworkBehaviours;

namespace EvoS.Framework.Misc
{
    public static class VectorUtils
    {
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 forward => new Vector3(0.0f, 0.0f, 1f);
        public static Vector3 back => new Vector3(0.0f, 0.0f, -1f);
        public static Vector3 up => new Vector3(0.0f, 1f, 0.0f);
        public static Vector3 down => new Vector3(0.0f, -1f, 0.0f);
        public static Vector3 left => new Vector3(-1f, 0.0f, 0.0f);
        public static Vector3 right => new Vector3(1f, 0.0f, 0.0f);

        public static ActorCover.CoverDirections GetCoverDirection(BoardSquare srcSquare, BoardSquare destSquare)
        {
            int x = srcSquare.X;
            int y = srcSquare.Y;
            int x2 = destSquare.X;
            int y2 = destSquare.Y;
            ActorCover.CoverDirections result;
            if (Mathf.Abs(x - x2) > Mathf.Abs(y - y2))
            {
                if (x > x2)
                {
                    result = ActorCover.CoverDirections.X_NEG;
                }
                else
                {
                    result = ActorCover.CoverDirections.X_POS;
                }
            }
            else if (y > y2)
            {
                result = ActorCover.CoverDirections.Y_NEG;
            }
            else
            {
                result = ActorCover.CoverDirections.Y_POS;
            }
            return result;
        }

        public static bool IsPointInLaser(Vector3 testPoint, Vector3 laserStartPos, Vector3 laserEndPos, float laserWidthInWorld)
        {
            testPoint.Y = 0f;
            laserStartPos.Y = 0f;
            laserEndPos.Y = 0f;
            float sqrMagnitude = (laserEndPos - laserStartPos).LengthSquared();
            Vector3 normalized = Vector3.Normalize(laserEndPos - laserStartPos);
            Vector3 lhs = testPoint - laserStartPos;
            Vector3 vector = laserStartPos + Vector3.Dot(lhs, normalized) * normalized;
            float sqrMagnitude2 = (vector - laserStartPos).LengthSquared();
            float sqrMagnitude3 = (laserEndPos - vector).LengthSquared();
            bool flag = sqrMagnitude2 < sqrMagnitude && sqrMagnitude3 < sqrMagnitude;
            float sqrMagnitude4 = (vector - testPoint).LengthSquared();
            float num = laserWidthInWorld / 2f * (laserWidthInWorld / 2f);
            bool flag2 = sqrMagnitude4 < num;
            return flag && flag2;
        }

        public static bool OnSameSideOfLine(Vector3 testPoint1, Vector3 testPoint2, Vector3 linePtA, Vector3 linePtB)
        {
            Vector3 lhs = linePtB - linePtA;
            lhs.Y = 0f;
            Vector3 rhs = testPoint1 - linePtA;
            rhs.Y = 0f;
            Vector3 rhs2 = testPoint2 - linePtA;
            rhs2.Y = 0f;
            Vector3 lhs2 = Vector3.Cross(lhs, rhs);
            Vector3 rhs3 = Vector3.Cross(lhs, rhs2);
            float num = Vector3.Dot(lhs2, rhs3);
            return num >= 0f;
        }

        public static float HorizontalAngle_Rad(Vector3 vec)
        {
            var vector2 = new Vector2(vec.X, vec.Z);
            Vector2.Normalize(vector2);
            return Mathf.Atan2(vector2.Y, vector2.X);
        }

        public static float HorizontalAngle_Deg(Vector3 vec)
        {
            var num = HorizontalAngle_Rad(vec) * 57.29578f;
            if (num < 0.0)
                num += 360f;
            return num;
        }

        public static Vector3 AngleRadToVector(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
        }

        public static Vector3 AngleDegreesToVector(float angle)
        {
            return AngleRadToVector(angle * ((float) Math.PI / 180f));
        }

        public static float Angle(Vector3 from, Vector3 to)
        {
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(Vector3.Normalize(from), Vector3.Normalize(to)), -1f, 1f)) * 57.29578f;
        }
    }
}
