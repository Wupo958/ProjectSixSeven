using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public static class DubinsSolver
    {
        private const float TwoPi = Mathf.PI * 2f;

        private enum Turn
        {
            Left,
            Straight,
            Right
        }

        private static readonly Turn[][] Words =
        {
            new[] { Turn.Left, Turn.Straight, Turn.Left },
            new[] { Turn.Right, Turn.Straight, Turn.Right },
            new[] { Turn.Left, Turn.Straight, Turn.Right },
            new[] { Turn.Right, Turn.Straight, Turn.Left },
            new[] { Turn.Right, Turn.Left, Turn.Right },
            new[] { Turn.Left, Turn.Right, Turn.Left }
        };

        public static List<TrackSegment> Solve(
            Vector3 startPosition,
            Vector3 startForward,
            Vector3 endPosition,
            Vector3 endForward,
            float radius)
        {
            if (radius <= 0f)
            {
                return null;
            }

            Vector3 start = new Vector3(startPosition.x, 0f, startPosition.z);
            Vector3 end = new Vector3(endPosition.x, 0f, endPosition.z);
            Vector3 startDir = new Vector3(startForward.x, 0f, startForward.z).normalized;
            Vector3 endDir = new Vector3(endForward.x, 0f, endForward.z).normalized;

            if (startDir.sqrMagnitude < 0.5f || endDir.sqrMagnitude < 0.5f)
            {
                return null;
            }

            float startHeading = Mathf.Atan2(startDir.z, startDir.x);
            float endHeading = Mathf.Atan2(endDir.z, endDir.x);

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            float d = distance / radius;
            float theta = Mod2Pi(Mathf.Atan2(delta.z, delta.x));

            float alpha = Mod2Pi(startHeading - theta);
            float beta = Mod2Pi(endHeading - theta);

            float bestCost = float.PositiveInfinity;
            Turn[] bestWord = null;
            Vector3 bestParams = Vector3.zero;

            for (int i = 0; i < Words.Length; i++)
            {
                if (!TrySolveWord(i, alpha, beta, d, out Vector3 lengths))
                {
                    continue;
                }

                float cost = lengths.x + lengths.y + lengths.z;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestWord = Words[i];
                    bestParams = lengths;
                }
            }

            if (bestWord == null)
            {
                return null;
            }

            return BuildSegments(start, startDir, bestWord, bestParams, radius);
        }

        private static List<TrackSegment> BuildSegments(
            Vector3 start,
            Vector3 startDir,
            IReadOnlyList<Turn> word,
            Vector3 lengths,
            float radius)
        {
            List<TrackSegment> segments = new List<TrackSegment>(3);

            Vector3 position = start;
            Vector3 forward = startDir;

            for (int i = 0; i < 3; i++)
            {
                float value = i == 0 ? lengths.x : i == 1 ? lengths.y : lengths.z;
                if (value <= Mathf.Epsilon)
                {
                    continue;
                }

                TrackSegment segment;
                switch (word[i])
                {
                    case Turn.Straight:
                        segment = new StraightSegment(position, forward, value * radius);
                        break;
                    case Turn.Left:
                        segment = new ArcSegment(position, forward, radius, value);
                        break;
                    default:
                        segment = new ArcSegment(position, forward, radius, -value);
                        break;
                }

                segments.Add(segment);
                position = segment.EndPosition;
                forward = segment.EndForward;
            }

            return segments.Count > 0 ? segments : null;
        }

        private static bool TrySolveWord(int index, float alpha, float beta, float d, out Vector3 lengths)
        {
            switch (index)
            {
                case 0: return SolveLsl(alpha, beta, d, out lengths);
                case 1: return SolveRsr(alpha, beta, d, out lengths);
                case 2: return SolveLsr(alpha, beta, d, out lengths);
                case 3: return SolveRsl(alpha, beta, d, out lengths);
                case 4: return SolveRlr(alpha, beta, d, out lengths);
                default: return SolveLrl(alpha, beta, d, out lengths);
            }
        }

        private static bool SolveLsl(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float pSq = 2f + d * d - 2f * Mathf.Cos(alpha - beta) + 2f * d * (sinA - sinB);
            if (pSq < 0f)
            {
                return false;
            }

            float tmp = Mathf.Atan2(cosB - cosA, d + sinA - sinB);
            lengths = new Vector3(Mod2Pi(tmp - alpha), Mathf.Sqrt(pSq), Mod2Pi(beta - tmp));
            return true;
        }

        private static bool SolveRsr(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float pSq = 2f + d * d - 2f * Mathf.Cos(alpha - beta) + 2f * d * (sinB - sinA);
            if (pSq < 0f)
            {
                return false;
            }

            float tmp = Mathf.Atan2(cosA - cosB, d - sinA + sinB);
            lengths = new Vector3(Mod2Pi(alpha - tmp), Mathf.Sqrt(pSq), Mod2Pi(tmp - beta));
            return true;
        }

        private static bool SolveLsr(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float pSq = -2f + d * d + 2f * Mathf.Cos(alpha - beta) + 2f * d * (sinA + sinB);
            if (pSq < 0f)
            {
                return false;
            }

            float p = Mathf.Sqrt(pSq);
            float tmp = Mathf.Atan2(-cosA - cosB, d + sinA + sinB) - Mathf.Atan2(-2f, p);
            lengths = new Vector3(Mod2Pi(tmp - alpha), p, Mod2Pi(tmp - Mod2Pi(beta)));
            return true;
        }

        private static bool SolveRsl(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float pSq = -2f + d * d + 2f * Mathf.Cos(alpha - beta) - 2f * d * (sinA + sinB);
            if (pSq < 0f)
            {
                return false;
            }

            float p = Mathf.Sqrt(pSq);
            float tmp = Mathf.Atan2(cosA + cosB, d - sinA - sinB) - Mathf.Atan2(2f, p);
            lengths = new Vector3(Mod2Pi(alpha - tmp), p, Mod2Pi(beta - tmp));
            return true;
        }

        private static bool SolveRlr(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float tmp = (6f - d * d + 2f * Mathf.Cos(alpha - beta) + 2f * d * (sinA - sinB)) / 8f;
            if (Mathf.Abs(tmp) > 1f)
            {
                return false;
            }

            float p = Mod2Pi(TwoPi - Mathf.Acos(tmp));
            float t = Mod2Pi(alpha - Mathf.Atan2(cosA - cosB, d - sinA + sinB) + Mod2Pi(p * 0.5f));
            float q = Mod2Pi(alpha - beta - t + Mod2Pi(p));

            lengths = new Vector3(t, p, q);
            return true;
        }

        private static bool SolveLrl(float alpha, float beta, float d, out Vector3 lengths)
        {
            lengths = Vector3.zero;

            float sinA = Mathf.Sin(alpha);
            float sinB = Mathf.Sin(beta);
            float cosA = Mathf.Cos(alpha);
            float cosB = Mathf.Cos(beta);

            float tmp = (6f - d * d + 2f * Mathf.Cos(alpha - beta) + 2f * d * (sinB - sinA)) / 8f;
            if (Mathf.Abs(tmp) > 1f)
            {
                return false;
            }

            float p = Mod2Pi(TwoPi - Mathf.Acos(tmp));
            float t = Mod2Pi(-alpha - Mathf.Atan2(cosA - cosB, d + sinA - sinB) + p * 0.5f);
            float q = Mod2Pi(Mod2Pi(beta) - alpha - t + Mod2Pi(p));

            lengths = new Vector3(t, p, q);
            return true;
        }

        private static float Mod2Pi(float value)
        {
            float result = value % TwoPi;
            return result < 0f ? result + TwoPi : result;
        }
    }
}
