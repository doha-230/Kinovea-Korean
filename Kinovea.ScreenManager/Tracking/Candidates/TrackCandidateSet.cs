using System.Collections.Generic;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// The set of candidates attached to one track drawing, plus the adoption logic.
    /// This class holds no UI and no tracker session: it is the pure part of the feature,
    /// which makes the selection rules testable.
    /// </summary>
    public class TrackCandidateSet
    {
        private List<TrackCandidate> candidates = new List<TrackCandidate>();
        private int adoptedIndex = -1;

        public int Count
        {
            get { return candidates.Count; }
        }

        /// <summary>Index of the adopted candidate, -1 when none was adopted.</summary>
        public int AdoptedIndex
        {
            get { return adoptedIndex; }
        }

        public IList<TrackCandidate> Candidates
        {
            get { return candidates.AsReadOnly(); }
        }

        public TrackCandidate this[int index]
        {
            get { return candidates[index]; }
        }

        public bool HasCandidates
        {
            get { return candidates.Count > 0; }
        }

        public void Add(TrackCandidate candidate)
        {
            if (candidate == null)
                return;

            candidates.Add(candidate);
        }

        /// <summary>
        /// Copy a candidate: same parameters, same trajectory, fresh name. Used by the editor
        /// to derive a case from an existing one, which is the fast path to build a sweep.
        /// </summary>
        public TrackCandidate Duplicate(int index, string name)
        {
            if (index < 0 || index >= candidates.Count)
                return null;

            TrackCandidate source = candidates[index];
            TrackingParameters parameters = source.Parameters == null ? null : source.Parameters.Clone();
            TrackCandidate copy = new TrackCandidate(name, parameters);
            copy.Failed = source.Failed;
            copy.MeanScore = source.MeanScore;

            if (source.Positions != null)
            {
                foreach (TimedPoint point in source.Positions)
                    copy.Positions.Add(new TimedPoint(point.X, point.Y, point.T, point.R));
            }

            candidates.Add(copy);
            return copy;
        }

        /// <summary>Rename a candidate, ignoring empty names.</summary>
        public void Rename(int index, string name)
        {
            if (index < 0 || index >= candidates.Count)
                return;
            if (string.IsNullOrEmpty(name))
                return;

            candidates[index].Name = name;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= candidates.Count)
                return;

            candidates.RemoveAt(index);

            if (adoptedIndex == index)
                adoptedIndex = -1;
            else if (adoptedIndex > index)
                adoptedIndex--;
        }

        public void Clear()
        {
            candidates.Clear();
            adoptedIndex = -1;
        }

        /// <summary>
        /// Adopt the candidate: returns a copy of its trajectory and remembers which one was
        /// adopted, so the caller can push it into the real track with a memento.
        /// Returns null when the candidate has no usable trajectory.
        /// </summary>
        public List<TimedPoint> Adopt(int index)
        {
            if (index < 0 || index >= candidates.Count)
                return null;

            TrackCandidate candidate = candidates[index];
            if (candidate.Positions == null || candidate.Positions.Count < 2)
                return null;

            List<TimedPoint> trajectory = new List<TimedPoint>();
            foreach (TimedPoint point in candidate.Positions)
                trajectory.Add(new TimedPoint(point.X, point.Y, point.T, point.R));

            adoptedIndex = index;
            return trajectory;
        }

        /// <summary>The candidate whose trajectory should be drawn on its own, -1 for the overlay view.</summary>
        public int VisibleIndex { get; set; }

        public void ResetVisibility()
        {
            VisibleIndex = -1;
        }
    }
}
