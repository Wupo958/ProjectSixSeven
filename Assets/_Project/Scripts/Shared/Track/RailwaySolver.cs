using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Connects two nodes with track a real railway could actually be built from: minimum radius
    /// honoured, clothoid easements into and out of every curve, and straight track through the
    /// nodes themselves.
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

            // Nodes packed closer together than the easements need leave the curvature ramps no room,
            // and the correction below stops converging. Shortening the easement is the honest answer:
            // cramped geometry gets gentler easing, and at zero it degrades to plain arcs, which are
            // always exact. Hitting the node is not negotiable; the easement length is.
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

            // Easing the curvature shifts the track slightly off the pose it was solved for, so we
            // solve against a target that we nudge until the eased result lands on the real one.
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
            // Straight lead-ins give the first and last easement somewhere to live, which is also
            // why track through a node is dead straight - the same as a real station or junction.
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
