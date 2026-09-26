using System;
using System.Drawing;
using Kinovea.ScreenManager;

namespace Kinovea.Tests.Player
{
    /// <summary>
    /// Tests for the frame skipping helpers (motion-adaptive metric).
    /// Called from Program.Main; returns the number of failed checks.
    /// </summary>
    public static class MotionEstimatorTest
    {
        public static int Run()
        {
            int failures = 0;
            failures += Check("no estimate yet -> no skipping", NoEstimateSkipsNothing());
            failures += Check("still scene -> skip up to the maximum", StaticSceneSkipsMax());
            failures += Check("fast movement -> no skipping", MovingSceneSkipsNothing());
            failures += Check("reset clears the estimate", ResetClearsEstimate());
            failures += Check("skip level ramps one frame at a time", SkipLevelRampsOneStepAtATime());
            failures += Check("higher sensitivity skips more", HigherSensitivitySkipsMore());
            return failures;
        }

        private static int Check(string name, bool ok)
        {
            Console.WriteLine("  [{0}] {1}", ok ? "PASS" : "FAIL", name);
            return ok ? 0 : 1;
        }

        private static bool NoEstimateSkipsNothing()
        {
            MotionEstimator estimator = new MotionEstimator();
            return estimator.GetSkipCount(10) == 0;
        }

        private static bool StaticSceneSkipsMax()
        {
            MotionEstimator estimator = new MotionEstimator();
            using (Bitmap a = MakeFrame(Color.Black))
            using (Bitmap b = MakeFrame(Color.Black))
            {
                estimator.Update(a, 0);
                estimator.Update(b, 33);
            }

            return estimator.HasEstimate && estimator.GetSkipCount(10) == 10;
        }

        private static bool MovingSceneSkipsNothing()
        {
            MotionEstimator estimator = new MotionEstimator();
            using (Bitmap a = MakeFrame(Color.Black))
            using (Bitmap b = MakeFrame(Color.White))
            {
                estimator.Update(a, 0);
                estimator.Update(b, 33);
            }

            return estimator.HasEstimate && estimator.GetSkipCount(10) == 0;
        }

        private static bool ResetClearsEstimate()
        {
            MotionEstimator estimator = new MotionEstimator();
            using (Bitmap a = MakeFrame(Color.Black))
            using (Bitmap b = MakeFrame(Color.Black))
            {
                estimator.Update(a, 0);
                estimator.Update(b, 33);
            }

            estimator.Reset();

            return !estimator.HasEstimate && estimator.GetSkipCount(10) == 0;
        }

        private static Bitmap MakeFrame(Color color)
        {
            Bitmap bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(color);
            }

            return bmp;
        }
        /// <summary>
        /// With a large change of motion the reported skip must walk down one frame
        /// at a time instead of jumping straight to the target.
        /// </summary>
        private static bool SkipLevelRampsOneStepAtATime()
        {
            MotionEstimator estimator = new MotionEstimator();
            using (Bitmap still = MakeFrame(Color.Black))
            using (Bitmap moving = MakeFrame(Color.White))
            {
                estimator.Update(still, 0);
                estimator.Update(still, 33);
                if (estimator.GetSkipCount(10) != 10)
                    return false;

                estimator.Update(moving, 66);
                if (estimator.GetSkipCount(10) != 9)
                    return false;

                return estimator.GetSkipCount(10) == 8;
            }
        }

        /// <summary>
        /// A slower reference speed (higher sensitivity) must keep skipping more.
        /// </summary>
        private static bool HigherSensitivitySkipsMore()
        {
            using (Bitmap a = MakeFrame(Color.FromArgb(0, 0, 0)))
            using (Bitmap b = MakeFrame(Color.FromArgb(1, 1, 1)))
            {
                MotionEstimator normal = new MotionEstimator();
                normal.Update(a, 0);
                normal.Update(b, 100);
                int normalSkip = normal.GetSkipCount(10, 1.0);

                MotionEstimator sensitive = new MotionEstimator();
                sensitive.Update(a, 0);
                sensitive.Update(b, 100);
                int sensitiveSkip = sensitive.GetSkipCount(10, 2.0);

                return sensitiveSkip > normalSkip;
            }
        }

    }
}
