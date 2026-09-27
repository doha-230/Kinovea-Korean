using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Creates the tracker that matches a parameters set.
    /// Shared by the track itself and by the candidate set so that both always use the
    /// same algorithm mapping.
    /// </summary>
    public static class TrackCandidateFactory
    {
        public static AbstractTracker CreateTracker(TrackingParameters parameters)
        {
            AbstractTracker tracker;

            switch (parameters.TrackingAlgorithm)
            {
                case TrackingAlgorithm.Blob:
                    tracker = new TrackerBlob(parameters);
                    break;
                case TrackingAlgorithm.Circle:
                    tracker = new TrackerCircle(parameters);
                    break;
                case TrackingAlgorithm.Correlation:
                default:
                    tracker = new TrackerTemplateMatching(parameters);
                    break;
            }

            tracker.Parameters.ResetOnMove = false;
            return tracker;
        }
    }
}
