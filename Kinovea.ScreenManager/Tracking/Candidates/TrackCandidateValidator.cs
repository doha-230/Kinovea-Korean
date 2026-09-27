using System.Collections.Generic;

namespace Kinovea.ScreenManager
{
    /// <summary>Reasons a candidate set cannot be tracked.</summary>
    public enum CandidateProblemKind
    {
        /// <summary>The search window is not large enough compared to the object window.</summary>
        SearchWindowTooSmall,
        /// <summary>The object window is too small to be a usable template.</summary>
        BlockWindowTooSmall,
        /// <summary>The match threshold must be strictly positive.</summary>
        ThresholdNotPositive,
        /// <summary>Two candidates share the same name.</summary>
        DuplicateName,
        /// <summary>There are more candidates than the limit allows.</summary>
        TooManyCandidates
    }

    /// <summary>One problem found in a candidate set.</summary>
    public class CandidateProblem
    {
        /// <summary>Index of the offending candidate, -1 when the problem is about the whole set.</summary>
        public int Index { get; private set; }
        public CandidateProblemKind Kind { get; private set; }

        public CandidateProblem(int index, CandidateProblemKind kind)
        {
            this.Index = index;
            this.Kind = kind;
        }
    }

    /// <summary>A non empty list of problems means the set must not be tracked as is.</summary>
    public class CandidateValidationResult
    {
        public List<CandidateProblem> Problems { get; private set; }

        public CandidateValidationResult()
        {
            this.Problems = new List<CandidateProblem>();
        }

        public bool IsValid
        {
            get { return Problems.Count == 0; }
        }

        public bool HasProblem(int index, CandidateProblemKind kind)
        {
            foreach (CandidateProblem problem in Problems)
            {
                if (problem.Index == index && problem.Kind == kind)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Checks a candidate set before it is handed to the tracker. The rules are the same as
    /// the ones used when a single track is started by hand.
    /// </summary>
    public static class TrackCandidateValidator
    {
        /// <summary>A template smaller than this is not a reliable pattern.</summary>
        public const int MinBlockWindow = 6;

        /// <summary>The search window must leave at least this many pixels around the object.</summary>
        public const int MinSearchMargin = 8;

        public static CandidateValidationResult Validate(IList<TrackCandidate> candidates, int maxCandidates)
        {
            CandidateValidationResult result = new CandidateValidationResult();

            if (candidates == null)
                return result;

            if (candidates.Count > maxCandidates)
                result.Problems.Add(new CandidateProblem(-1, CandidateProblemKind.TooManyCandidates));

            for (int i = 0; i < candidates.Count; i++)
            {
                TrackCandidate candidate = candidates[i];
                if (candidate == null || candidate.Parameters == null)
                    continue;

                if (candidate.Parameters.BlockWindow.Width < MinBlockWindow ||
                    candidate.Parameters.BlockWindow.Height < MinBlockWindow)
                {
                    result.Problems.Add(new CandidateProblem(i, CandidateProblemKind.BlockWindowTooSmall));
                }

                if (candidate.Parameters.SearchWindow.Width < candidate.Parameters.BlockWindow.Width + MinSearchMargin ||
                    candidate.Parameters.SearchWindow.Height < candidate.Parameters.BlockWindow.Height + MinSearchMargin)
                {
                    result.Problems.Add(new CandidateProblem(i, CandidateProblemKind.SearchWindowTooSmall));
                }

                if (candidate.Parameters.SimilarityThreshold <= 0)
                    result.Problems.Add(new CandidateProblem(i, CandidateProblemKind.ThresholdNotPositive));

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (candidates[j] == null)
                        continue;
                    if (string.Equals(candidate.Name, candidates[j].Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        result.Problems.Add(new CandidateProblem(i, CandidateProblemKind.DuplicateName));
                        break;
                    }
                }
            }

            return result;
        }
    }
}
