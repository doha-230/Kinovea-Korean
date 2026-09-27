using System;
using System.Collections.Generic;
using System.Drawing;
using Kinovea.ScreenManager;
using Kinovea.Services;

namespace Kinovea.Tests.Tracking
{
    /// <summary>
    /// Tests for the tracking candidate helpers (metrics, ranking, validation, set).
    /// Called from Program.Main; returns the number of failed checks.
    /// </summary>
    public static class CandidateMetricsTest
    {
        public static int Run()
        {
            int failures = 0;

            // Metrics
            failures += Check("straight line -> nothing stuck", StraightLineIsNotStuck());
            failures += Check("static points -> everything stuck", StaticPointsAreStuck());
            failures += Check("single jump -> max jump near 1", SingleBigJumpIsReported());
            failures += Check("empty trajectory -> safe zeros", EmptyTrajectoryIsSafe());
            failures += Check("single point -> safe zeros", SinglePointIsSafe());
            failures += Check("coverage with expected frames", CoverageUsesExpectedFrames());
            failures += Check("coverage without expected frames", CoverageUsesTimestamps());
            failures += Check("jerky trajectory -> lower smoothness", JerkyIsLessSmooth());
            failures += Check("match score is clamped", MatchScoreIsClamped());

            // Weighted score
            failures += Check("missing match score -> renormalized", MissingScoreIsRenormalized());
            failures += Check("better motion -> better score", BetterMotionScoresHigher());

            // Ranking
            failures += Check("better candidate ranks first", BetterCandidateRanksFirst());
            failures += Check("tie keeps creation order", TieKeepsCreationOrder());
            failures += Check("failed candidate ranks last", FailedCandidateRanksLast());
            failures += Check("rank is exposed on the candidate", RankIsExposed());

            // Validation
            failures += Check("search window too small is rejected", SearchWindowTooSmallRejected());
            failures += Check("object window too small is rejected", BlockWindowTooSmallRejected());
            failures += Check("threshold zero is rejected", ThresholdZeroRejected());
            failures += Check("duplicate names are rejected", DuplicateNameRejected());
            failures += Check("too many candidates is rejected", TooManyRejected());
            failures += Check("valid set passes", ValidSetPasses());

            // Set and adoption
            failures += Check("adopt returns a copy of the trajectory", AdoptReturnsCopy());
            failures += Check("adopt of a failed candidate returns null", AdoptFailedReturnsNull());
            failures += Check("duplicate copies the parameters", DuplicateCopiesParameters());
            failures += Check("remove keeps the adopted index consistent", RemoveKeepsAdoptedIndex());
            failures += Check("clear resets everything", ClearResets());
            failures += Check("clone carries the fork options", CloneCarriesForkOptions());

            return failures;
        }

        private static int Check(string name, bool ok)
        {
            Console.WriteLine("  [{0}] {1}", ok ? "PASS" : "FAIL", name);
            return ok ? 0 : 1;
        }

        // ------------------------------------------------------------------ helpers

        private static List<TimedPoint> Line(int count, float stepX, long stepT)
        {
            List<TimedPoint> points = new List<TimedPoint>();
            for (int i = 0; i < count; i++)
                points.Add(new TimedPoint(i * stepX, 0, i * stepT));
            return points;
        }

        private static TrackCandidate Candidate(string name, List<TimedPoint> positions, double score, Size block)
        {
            TrackCandidate candidate = new TrackCandidate(name, new TrackingParameters());
            if (positions != null)
                candidate.Positions.AddRange(positions);
            candidate.MeanScore = score;
            candidate.Metrics = CandidateMetrics.Compute(candidate.Positions, score, block, 0);
            return candidate;
        }

        // ------------------------------------------------------------------ metrics

        private static bool StraightLineIsNotStuck()
        {
            CandidateMetrics m = CandidateMetrics.Compute(Line(10, 10, 33), 0.9, new Size(20, 20), 0);
            return Math.Abs(m.StuckRatio) < 1e-9;
        }

        private static bool StaticPointsAreStuck()
        {
            CandidateMetrics m = CandidateMetrics.Compute(Line(10, 0, 33), 0.9, new Size(20, 20), 0);
            return Math.Abs(m.StuckRatio - 1.0) < 1e-9;
        }

        private static bool SingleBigJumpIsReported()
        {
            List<TimedPoint> points = Line(5, 1, 33);
            points[3] = new TimedPoint(500, 500, points[3].T);
            CandidateMetrics m = CandidateMetrics.Compute(points, 0.9, new Size(20, 20), 0);
            return m.MaxJump > 0.9;
        }

        private static bool EmptyTrajectoryIsSafe()
        {
            CandidateMetrics m = CandidateMetrics.Compute(new List<TimedPoint>(), 0.9, new Size(20, 20), 0);
            return m.SampleCount == 0 && m.StuckRatio == 0 && m.MaxJump == 0 && m.Smoothness == 0;
        }

        private static bool SinglePointIsSafe()
        {
            List<TimedPoint> points = new List<TimedPoint>();
            points.Add(new TimedPoint(1, 1, 0));
            CandidateMetrics m = CandidateMetrics.Compute(points, 0.9, new Size(20, 20), 0);
            return m.SampleCount == 1 && m.StuckRatio == 0 && m.Coverage == 0;
        }

        private static bool CoverageUsesExpectedFrames()
        {
            CandidateMetrics m = CandidateMetrics.Compute(Line(50, 10, 33), 0.9, new Size(20, 20), 100);
            return Math.Abs(m.Coverage - 0.5) < 1e-9;
        }

        private static bool CoverageUsesTimestamps()
        {
            List<TimedPoint> points = Line(10, 10, 33);
            // duplicate timestamps on the last three samples (as produced by stalled steps)
            for (int i = 7; i < 10; i++)
                points[i].T = points[6].T;

            CandidateMetrics m = CandidateMetrics.Compute(points, 0.9, new Size(20, 20), 0);
            return m.Coverage > 0.5 && m.Coverage < 0.8;
        }

        private static bool JerkyIsLessSmooth()
        {
            List<TimedPoint> jerky = new List<TimedPoint>();
            for (int i = 0; i < 12; i++)
                jerky.Add(new TimedPoint(i % 2 == 0 ? 0 : 30, 0, i * 33));

            CandidateMetrics smooth = CandidateMetrics.Compute(Line(12, 10, 33), 0.9, new Size(20, 20), 0);
            CandidateMetrics rough = CandidateMetrics.Compute(jerky, 0.9, new Size(20, 20), 0);
            return rough.Smoothness < smooth.Smoothness;
        }

        private static bool MatchScoreIsClamped()
        {
            CandidateMetrics high = CandidateMetrics.Compute(Line(5, 10, 33), 2.0, new Size(20, 20), 0);
            CandidateMetrics low = CandidateMetrics.Compute(Line(5, 10, 33), -1.0, new Size(20, 20), 0);
            return high.MeanScore == 1.0 && low.MeanScore == 0.0;
        }

        // ------------------------------------------------------------------ score

        private static bool MissingScoreIsRenormalized()
        {
            List<TimedPoint> points = Line(10, 10, 33);
            CandidateMetrics withScore = CandidateMetrics.Compute(points, 1.0, new Size(20, 20), 0);
            CandidateMetrics withoutScore = CandidateMetrics.Compute(points, double.NaN, new Size(20, 20), 0);
            return double.IsNaN(withoutScore.MeanScore)
                && withoutScore.Score > 0.8
                && withScore.Score > withoutScore.Score;
        }

        private static bool BetterMotionScoresHigher()
        {
            CandidateMetrics stuck = CandidateMetrics.Compute(Line(10, 0, 33), 0.9, new Size(20, 20), 0);
            CandidateMetrics moving = CandidateMetrics.Compute(Line(10, 10, 33), 0.9, new Size(20, 20), 0);
            return moving.Score > stuck.Score;
        }

        // ------------------------------------------------------------------ ranking

        private static bool BetterCandidateRanksFirst()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(Candidate("stuck", Line(10, 0, 33), 0.9, new Size(20, 20)));
            candidates.Add(Candidate("moving", Line(10, 10, 33), 0.9, new Size(20, 20)));
            CandidateMetrics.Rank(candidates);
            return candidates[1].Rank == 1 && candidates[0].Rank == 2;
        }

        private static bool TieKeepsCreationOrder()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(Candidate("first", Line(10, 10, 33), 0.9, new Size(20, 20)));
            candidates.Add(Candidate("second", Line(10, 10, 33), 0.9, new Size(20, 20)));
            candidates.Add(Candidate("third", Line(10, 10, 33), 0.9, new Size(20, 20)));
            CandidateMetrics.Rank(candidates);
            return candidates[0].Rank == 1 && candidates[1].Rank == 2 && candidates[2].Rank == 3;
        }

        private static bool FailedCandidateRanksLast()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            TrackCandidate failed = Candidate("failed", Line(10, 10, 33), double.NaN, new Size(20, 20));
            failed.Failed = true;
            candidates.Add(failed);
            candidates.Add(Candidate("ok", Line(10, 10, 33), 0.5, new Size(20, 20)));
            CandidateMetrics.Rank(candidates);
            return candidates[0].Rank == 2 && candidates[1].Rank == 1;
        }

        private static bool RankIsExposed()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(Candidate("a", Line(10, 0, 33), 0.9, new Size(20, 20)));
            candidates.Add(Candidate("b", Line(10, 10, 33), 0.9, new Size(20, 20)));
            CandidateMetrics.Rank(candidates);
            return candidates[1].Rank == 1 && candidates[1].Metrics.Rank == 1;
        }

        // ------------------------------------------------------------------ validation

        private static TrackCandidate WithWindows(int search, int block)
        {
            TrackingParameters parameters = new TrackingParameters();
            parameters.SearchWindow = new Size(search, search);
            parameters.BlockWindow = new Size(block, block);
            parameters.SimilarityThreshold = 0.7;
            return new TrackCandidate("case", parameters);
        }

        private static bool SearchWindowTooSmallRejected()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(WithWindows(20, 20));
            CandidateValidationResult result = TrackCandidateValidator.Validate(candidates, 5);
            return result.HasProblem(0, CandidateProblemKind.SearchWindowTooSmall);
        }

        private static bool BlockWindowTooSmallRejected()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(WithWindows(60, 4));
            CandidateValidationResult result = TrackCandidateValidator.Validate(candidates, 5);
            return result.HasProblem(0, CandidateProblemKind.BlockWindowTooSmall);
        }

        private static bool ThresholdZeroRejected()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            TrackCandidate candidate = WithWindows(60, 20);
            candidate.Parameters.SimilarityThreshold = 0;
            candidates.Add(candidate);
            CandidateValidationResult result = TrackCandidateValidator.Validate(candidates, 5);
            return result.HasProblem(0, CandidateProblemKind.ThresholdNotPositive);
        }

        private static bool DuplicateNameRejected()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(WithWindows(60, 20));
            candidates.Add(WithWindows(80, 20));
            candidates[1].Name = "CASE";
            CandidateValidationResult result = TrackCandidateValidator.Validate(candidates, 5);
            return result.HasProblem(0, CandidateProblemKind.DuplicateName);
        }

        private static bool TooManyRejected()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            for (int i = 0; i < 6; i++)
            {
                TrackCandidate candidate = WithWindows(60, 20);
                candidate.Name = "case" + i;
                candidates.Add(candidate);
            }
            CandidateValidationResult result = TrackCandidateValidator.Validate(candidates, 5);
            return result.HasProblem(-1, CandidateProblemKind.TooManyCandidates);
        }

        private static bool ValidSetPasses()
        {
            List<TrackCandidate> candidates = new List<TrackCandidate>();
            candidates.Add(WithWindows(100, 20));
            TrackCandidate second = WithWindows(150, 24);
            second.Name = "case2";
            candidates.Add(second);
            return TrackCandidateValidator.Validate(candidates, 5).IsValid;
        }

        // ------------------------------------------------------------------ set

        private static bool AdoptReturnsCopy()
        {
            TrackCandidateSet set = new TrackCandidateSet();
            set.Add(Candidate("a", Line(5, 10, 33), 0.9, new Size(20, 20)));
            List<TimedPoint> adopted = set.Adopt(0);
            bool same = adopted != null && adopted.Count == 5 && Math.Abs(adopted[4].X - 40) < 1e-6;
            // mutating the copy must not touch the candidate
            adopted[0].X = 999;
            return same && Math.Abs(set[0].Positions[0].X) < 1e-6 && set.AdoptedIndex == 0;
        }

        private static bool AdoptFailedReturnsNull()
        {
            TrackCandidateSet set = new TrackCandidateSet();
            set.Add(Candidate("a", new List<TimedPoint>(), 0.9, new Size(20, 20)));
            return set.Adopt(0) == null && set.AdoptedIndex == -1;
        }

        private static bool DuplicateCopiesParameters()
        {
            TrackCandidateSet set = new TrackCandidateSet();
            TrackCandidate source = WithWindows(120, 20);
            set.Add(source);
            TrackCandidate copy = set.Duplicate(0, "copy");
            if (copy == null || set.Count != 2)
                return false;
            // same values, distinct instances
            return copy.Parameters != source.Parameters
                && copy.Parameters.SearchWindow.Width == 120
                && copy.Parameters.BlockWindow.Width == 20;
        }

        private static bool RemoveKeepsAdoptedIndex()
        {
            TrackCandidateSet set = new TrackCandidateSet();
            set.Add(Candidate("a", Line(5, 10, 33), 0.9, new Size(20, 20)));
            set.Add(Candidate("b", Line(5, 10, 33), 0.9, new Size(20, 20)));
            set.Add(Candidate("c", Line(5, 10, 33), 0.9, new Size(20, 20)));
            set.Adopt(2);
            set.RemoveAt(0);
            bool shifted = set.AdoptedIndex == 1;
            set.RemoveAt(1);
            return shifted && set.AdoptedIndex == -1;
        }

        private static bool CloneCarriesForkOptions()
        {
            TrackingParameters parameters = new TrackingParameters();
            parameters.PredictiveSearch = true;
            parameters.RejectOutliers = true;
            parameters.ScaleAdaptive = true;
            parameters.SearchWindow = new Size(150, 150);
            TrackingParameters clone = parameters.Clone();
            return clone.PredictiveSearch && clone.RejectOutliers && clone.ScaleAdaptive
                && clone.SearchWindow.Width == 150;
        }

        private static bool ClearResets()
        {
            TrackCandidateSet set = new TrackCandidateSet();
            set.Add(Candidate("a", Line(5, 10, 33), 0.9, new Size(20, 20)));
            set.Adopt(0);
            set.Clear();
            return set.Count == 0 && !set.HasCandidates && set.AdoptedIndex == -1;
        }
    }
}
