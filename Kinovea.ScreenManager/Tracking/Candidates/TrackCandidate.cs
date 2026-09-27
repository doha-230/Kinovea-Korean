using System.Collections.Generic;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// One tracking hypothesis, tracked alongside the others so that the user can compare
    /// the trajectories and adopt one of them.
    /// Candidates are volatile: they are never serialized. Adopting a candidate copies its
    /// trajectory into the real track, so the KVA format and the upstream compatibility are
    /// left untouched.
    /// </summary>
    public class TrackCandidate
    {
        /// <summary>User visible name, unique inside a set.</summary>
        public string Name { get; set; }

        /// <summary>Parameters this candidate is tracked with.</summary>
        public TrackingParameters Parameters { get; set; }

        /// <summary>Tracker instance, created when the candidate set is started.</summary>
        public AbstractTracker Tracker { get; set; }

        /// <summary>Trajectory built by this candidate.</summary>
        public List<TimedPoint> Positions { get; private set; }

        /// <summary>True when the candidate never managed to track anything.</summary>
        public bool Failed { get; set; }

        /// <summary>Proxy metrics, computed once the tracking session stops.</summary>
        public CandidateMetrics Metrics { get; set; }

        /// <summary>Average match score, NaN when the algorithm does not report one (blob, circle).</summary>
        public double MeanScore { get; set; }

        public TrackCandidate(string name, TrackingParameters parameters)
        {
            this.Name = name;
            this.Parameters = parameters;
            this.Positions = new List<TimedPoint>();
            this.MeanScore = double.NaN;
        }

        /// <summary>Sum of the match scores collected while tracking.</summary>
        private double scoreSum;
        private int scoreCount;

        /// <summary>Accumulate one match score. Scores that the algorithm does not report are ignored.</summary>
        public void AddScore(double score)
        {
            if (double.IsNaN(score))
                return;

            scoreSum += score;
            scoreCount++;
        }

        /// <summary>Turn the accumulated scores into the average, NaN when none was reported.</summary>
        public void FinalizeScore()
        {
            MeanScore = scoreCount > 0 ? scoreSum / scoreCount : double.NaN;
        }

        /// <summary>Forget the scores of a previous run.</summary>
        public void ResetScores()
        {
            scoreSum = 0;
            scoreCount = 0;
            MeanScore = double.NaN;
        }

        /// <summary>Rank among the candidates of the set, 1 is the recommended one. 0 when not ranked yet.</summary>
        public int Rank
        {
            get { return Metrics == null ? 0 : Metrics.Rank; }
        }

        /// <summary>True when the trajectory has at least two points.</summary>
        public bool HasTrajectory
        {
            get { return Positions != null && Positions.Count > 1; }
        }
    }
}
