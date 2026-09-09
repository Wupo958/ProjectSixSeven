using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public static class RailwaySolver
    {
        private const int MaxCorrectionPasses = 12;
        private const int MaxBackoffAttempts = 8;
        private const float PositionTolerance = 0.01f;
        private const float HeadingTolerance = 0.00002f;

        public static List<TrackSegment> Solve(
            Vector3 startPosition,
            Vector3 startForward,
            Vector3 endPosition,
            Vector3 endForward,
            float minRadius,
            float transitionLength)
        {
            Vector3 start = TrackMath.Flatten(startPosition);
            Vector3 end = TrackMath.Flatten(endPosition);
            Vector3 startDirection = TrackMath.FlattenDirection(startForward);
            Vector3 endDirection = TrackMath.FlattenDirection(endForward);

            float attemptLength = Mathf.Max(0f, transitionLength);

            for (int attempt = 0; attempt <= MaxBackoffAttempts; attempt++)
            {
                List<TrackSegment> solved = SolveWithTransition(
                    start, startDirection, end, endDirection, minRadius, attemptLength, out bool converged);

                if (solved == null)
                {
                    return null;
                }

                if (converged)
                {
                    return solved;
                }

                attemptLength = attempt == MaxBackoffAttempts - 1 ? 0f : attemptLength * 0.5f;
            }

            return SolveWithTransition(start, startDirection, end, endDirection, minRadius, 0f, out _);
        }

        private static List<TrackSegment> SolveWithTransition(
            Vector3 start,
            Vector3 startDirection,
            Vector3 end,
            Vector3 endDirection,
            float minRadius,
            float transitionLength,
            out bool converged)
        {
            converged = false;

            Vector3 targetPosition = end;
            Vector3 targetDirection = endDirection;

            List<TrackSegment> best = null;
            float bestError = float.PositiveInfinity;

            for (int pass = 0; pass < MaxCorrectionPasses; pass++)
            {
                List<TrackSegment> candidate = BuildOnce(
                    start, startDirection, targetPosition, targetDirection, minRadius, transitionLength);

                if (candidate == null || candidate.Count == 0)
                {
                    return best;
                }

                TrackSegment last = candidate[candidate.Count - 1];
                Vector3 positionError = end - last.EndPosition;
                float headingError = TrackMath.SignedAngle(last.EndForward, endDirection);

                float error = positionError.magnitude;
                if (error < bestError)
                {
                    bestError = error;
                    best = candidate;
                }

                if (error < PositionTolerance && Mathf.Abs(headingError) < HeadingTolerance)
                {
                    converged = true;
                    return candidate;
                }

                targetPosition += positionError;
                targetDirection = TrackMath.RotateXZ(targetDirection, headingError);
            }

            return best;
        }

        private static List<TrackSegment> BuildOnce(
            Vector3 start,
            Vector3 startDirection,
            Vector3 target,
            Vector3 targetDirection,
            float minRadius,
            float transitionLength)
        {
            float lead = Mathf.Max(transitionLength, 1f);

            Vector3 afterLeadIn = start + startDirection * lead;
            Vector3 beforeLeadOut = target - targetDirection * lead;

            List<TrackSegment> core = DubinsSolver.Solve(
                afterLeadIn, startDirection, beforeLeadOut, targetDirection, minRadius);

            if (core == null)
            {
                return null;
            }

            List<TrackSegment> raw = new List<TrackSegment>(core.Count + 2);
            raw.Add(new StraightSegment(start, startDirection, lead));
            raw.AddRange(core);
            raw.Add(new StraightSegment(beforeLeadOut, targetDirection, lead));

            return TrackSmoother.Smooth(raw, start, startDirection, transitionLength, 1f / minRadius);
        }
    }
}
