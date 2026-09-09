using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public static class TrackSmoother
    {
        private const float MinPieceLength = 0.01f;
        private const int RelaxPasses = 4;

        public static List<TrackSegment> Smooth(
            List<TrackSegment> input,
            Vector3 startPosition,
            Vector3 startForward,
            float transitionLength,
            float maxCurvature)
        {
            if (input == null || input.Count == 0)
            {
                return input;
            }

            int count = input.Count;
            float[] curvature = new float[count];
            float[] length = new float[count];

            for (int i = 0; i < count; i++)
            {
                curvature[i] = input[i].CurvatureAt(0f);
                length[i] = input[i].Length;
            }

            float[] halfRamp = BuildRamps(curvature, length, transitionLength, maxCurvature);

            return Integrate(curvature, length, halfRamp, startPosition, startForward);
        }

        private static float[] BuildRamps(
            float[] curvature,
            float[] length,
            float transitionLength,
            float maxCurvature)
        {
            int joins = curvature.Length - 1;
            float[] halfRamp = new float[Mathf.Max(0, joins)];

            for (int i = 0; i < joins; i++)
            {
                float change = Mathf.Abs(curvature[i + 1] - curvature[i]);
                if (change < 0.000001f || maxCurvature < 0.000001f)
                {
                    continue;
                }

                halfRamp[i] = transitionLength * (change / maxCurvature) * 0.5f;
            }

            for (int pass = 0; pass < RelaxPasses; pass++)
            {
                for (int i = 0; i < curvature.Length; i++)
                {
                    float left = i > 0 ? halfRamp[i - 1] : 0f;
                    float right = i < joins ? halfRamp[i] : 0f;
                    float wanted = left + right;

                    if (wanted <= length[i] || wanted < 0.000001f)
                    {
                        continue;
                    }

                    float scale = length[i] / wanted;
                    if (i > 0)
                    {
                        halfRamp[i - 1] *= scale;
                    }

                    if (i < joins)
                    {
                        halfRamp[i] *= scale;
                    }
                }
            }

            return halfRamp;
        }

        private static List<TrackSegment> Integrate(
            float[] curvature,
            float[] length,
            float[] halfRamp,
            Vector3 startPosition,
            Vector3 startForward)
        {
            List<TrackSegment> output = new List<TrackSegment>();

            Vector3 position = TrackMath.Flatten(startPosition);
            Vector3 forward = TrackMath.FlattenDirection(startForward);
            int joins = curvature.Length - 1;

            for (int i = 0; i < curvature.Length; i++)
            {
                float left = i > 0 ? halfRamp[i - 1] : 0f;
                float right = i < joins ? halfRamp[i] : 0f;
                float core = length[i] - left - right;

                if (core > MinPieceLength)
                {
                    TrackSegment segment = MakeConstant(position, forward, curvature[i], core);
                    output.Add(segment);
                    position = segment.EndPosition;
                    forward = segment.EndForward;
                }

                if (i < joins && right > MinPieceLength * 0.5f)
                {
                    ClothoidSegment ramp = new ClothoidSegment(
                        position, forward, curvature[i], curvature[i + 1], right * 2f);

                    output.Add(ramp);
                    position = ramp.EndPosition;
                    forward = ramp.EndForward;
                }
            }

            return output;
        }

        private static TrackSegment MakeConstant(Vector3 position, Vector3 forward, float curvature, float length)
        {
            if (Mathf.Abs(curvature) < 0.000001f)
            {
                return new StraightSegment(position, forward, length);
            }

            float radius = 1f / Mathf.Abs(curvature);
            float turnAngle = curvature * length;
            return new ArcSegment(position, forward, radius, turnAngle);
        }
    }
}
