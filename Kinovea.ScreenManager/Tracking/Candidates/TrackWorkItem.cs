namespace Kinovea.ScreenManager
{
    /// <summary>
    /// One unit of tracking work for a frame: either a regular track, or one candidate of a
    /// track that runs a candidate sweep. Keeping both in a single list lets the parallel loop
    /// spread the candidates over the cores without changing how the regular tracks are handled.
    /// </summary>
    public class TrackWorkItem
    {
        public DrawingTrack Track { get; private set; }

        /// <summary>Null for a regular track step.</summary>
        public TrackCandidate Candidate { get; private set; }

        public TrackWorkItem(DrawingTrack track, TrackCandidate candidate)
        {
            this.Track = track;
            this.Candidate = candidate;
        }
    }
}
