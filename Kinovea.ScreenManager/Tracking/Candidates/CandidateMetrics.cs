using System;
using System.Collections.Generic;
using System.Drawing;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Proxy metrics used to rank the tracking candidates.
    ///
    /// There is no ground truth for a tracking run, so these are only hints that help the
    /// user compare candidates. The adoption is always a manual decision.
    ///
    /// All values are normalized so that "higher is better":
    ///   MeanScore   average match score (only the correlation tracker reports one)
    ///   StuckRatio  fraction of consecutive samples that did not move
    ///   MaxJump     largest move between two samples, relative to the object size
    ///   Coverage    tracked samples relative to the expected frame count (informational)
    ///   Smoothness  1 = very smooth, 0 = very jerky
    /// </summary>
    public class CandidateMetrics
    {
        // Fixed weights, higher is better for every term.
        public const double WeightScore = 0.4;
        public const double WeightStuck = 0.25;
        public const double WeightJump = 0.2;
        public const double WeightSmoothness = 0.15;

        /// <summary>Moves below this many pixels count as "the object did not move".</summary>
        public const double StuckTolerance = 0.5;

        public double MeanScore { get; private set; }
        public double StuckRatio { get; private set; }
        public double MaxJump { get; private set; }
        public double Coverage { get; private set; }
        public double Smoothness { get; private set; }
        public double Score { get; private set; }
        public int Rank { get; internal set; }
        public int SampleCount { get; private set; }

        private CandidateMetrics()
        {
        }

        /// <summary>
        /// Compute the metrics of one trajectory.
        /// meanScore: average match score, use double.NaN when the algorithm reports none.
        /// blockWindow: object window, used to express the jumps in object sizes.
        /// expectedFrames: number of frames the tracking session should have covered, 0 when unknown.
        /// </summary>
        public static CandidateMetrics Compute(IList<TimedPoint> positions, double meanScore, Size blockWindow, int expectedFrames)
        {
            CandidateMetrics m = new CandidateMetrics();

            m.MeanScore = double.IsNaN(meanScore) ? double.NaN : Clamp01(meanScore);

            int count = positions == null ? 0 : positions.Count;
            m.SampleCount = count;

            double diagonal = Math.Sqrt((double)blockWindow.Width * blockWindow.Width + (double)blockWindow.Height * blockWindow.Height);
            double reference = diagonal > 1.0 ? diagonal : 1.0;

            if (count >= 2)
            {
                int stuck = 0;
                double maxJump = 0;
                double sumAccel2 = 0;
                int accelCount = 0;

                for (int i = 1; i < count; i++)
                {
                    double dx = positions[i].X - positions[i - 1].X;
                    double dy = positions[i].Y - positions[i - 1].Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance < StuckTolerance)
                        stuck++;

                    if (distance > maxJump)
                        maxJump = distance;

                    if (i >= 2)
                    {
                        double ax = positions[i].X - 2.0 * positions[i - 1].X + positions[i - 2].X;
                        double ay = positions[i].Y - 2.0 * positions[i - 1].Y + positions[i - 2].Y;
                        sumAccel2 += ax * ax + ay * ay;
                        accelCount++;
                    }
                }

                m.StuckRatio = (double)stuck / (count - 1);
                m.MaxJump = Clamp01(maxJump / reference);

                double meanAccel2 = accelCount > 0 ? sumAccel2 / accelCount : 0;
                double normalizedAccel = Math.Sqrt(meanAccel2) / reference;
                m.Smoothness = Clamp01(1.0 / (1.0 + normalizedAccel * 10.0));

                if (expectedFrames > 0)
                {
                    m.Coverage = Clamp01((double)count / expectedFrames);
                }
                else
                {
                    // Without an expected frame count, use the fraction of samples that
                    // actually advanced in time (repeated timestamps come from stalled steps).
                    int advancing = 0;
                    for (int i = 1; i < count; i++)
                    {
                        if (positions[i].T > positions[i - 1].T)
                            advancing++;
                    }
                    m.Coverage = (double)advancing / (count - 1);
                }
            }

            // A trajectory that never advanced cannot be recommended, whatever the other terms say.
            m.Score = count < 2 ? 0 : WeightedScore(m);
            return m;
        }

        /// <summary>
        /// Weighted score in 0..1. Terms that are not available (no match score with the blob
        /// or circle algorithms) are left out and the remaining weights are renormalized, so
        /// candidates of different algorithms stay comparable.
        /// </summary>
        public static double WeightedScore(CandidateMetrics m)
        {
            double sum = 0;
            double weight = 0;

            if (!double.IsNaN(m.MeanScore))
            {
                sum += WeightScore * m.MeanScore;
                weight += WeightScore;
            }

            sum += WeightStuck * (1.0 - m.StuckRatio);
            weight += WeightStuck;
            sum += WeightJump * (1.0 - m.MaxJump);
            weight += WeightJump;
            sum += WeightSmoothness * m.Smoothness;
            weight += WeightSmoothness;

            return weight > 0 ? sum / weight : 0;
        }

        /// <summary>
        /// Rank the candidates by descending score and store the rank in each candidate.
        /// Ties keep the order the candidates were created in, so the result is deterministic.
        /// Failed candidates are ranked last.
        /// </summary>
        public static void RankCandidates(IList<TrackCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            List<int> order = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                order.Add(i);

            order.Sort(delegate (int a, int b)
            {
                bool failedA = candidates[a].Failed || candidates[a].Metrics == null;
                bool failedB = candidates[b].Failed || candidates[b].Metrics == null;

                if (failedA != failedB)
                    return failedA ? 1 : -1;

                if (failedA && failedB)
                    return a.CompareTo(b);

                int byScore = candidates[b].Metrics.Score.CompareTo(candidates[a].Metrics.Score);
                return byScore != 0 ? byScore : a.CompareTo(b);
            });

            for (int i = 0; i < order.Count; i++)
            {
                TrackCandidate candidate = candidates[order[i]];
                if (candidate.Metrics == null)
                    candidate.Metrics = Compute(candidate.Positions, candidate.MeanScore, new Size(0, 0), 0);
                candidate.Metrics.Rank = i + 1;
            }
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value))
                return 0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
