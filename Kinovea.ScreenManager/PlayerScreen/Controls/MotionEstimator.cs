#region License
/*
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Cheap motion metric used by the motion-adaptive frame skipping mode.
    ///
    /// It samples a downscaled luma version of the rendered frames and reports the
    /// average luma change *per millisecond*. Working per millisecond matters:
    /// the raw difference between two rendered frames grows with their time
    /// interval, so using it directly would make the policy self-reinforcing
    /// (skip more -> bigger difference -> skip less -> ...). Dividing by the real
    /// elapsed time turns it into a speed, which is what the policy needs.
    ///
    /// Cost is kept low by sampling one pixel every <see cref="Step"/> pixels and
    /// only copying the sampled rows out of the bitmap.
    /// </summary>
    public sealed class MotionEstimator
    {
        /// <summary>Sample one pixel every N pixels horizontally and vertically.</summary>
        private const int Step = 8;

        /// <summary>
        /// Luma change per millisecond considered "fast motion" (skip nothing).
        /// </summary>
        private const double ReferenceMotionPerMs = 0.02;

        /// <summary>Weight of the newest sample in the exponential moving average.</summary>
        private const double Smoothing = 0.25;

        private byte[] previous;
        private int sampleWidth;
        private long previousTimestamp;
        private bool hasPrevious;
        private bool hasEstimate;
        private double smoothed;
        private int lastSkip;
        private bool hasSkipLevel;

        /// <summary>Smoothed motion, in luma units per millisecond.</summary>
        public double MotionPerMs
        {
            get { return smoothed; }
        }

        /// <summary>
        /// Whether a motion measurement is available yet. Until then the policy
        /// must not skip (the first frames are played frame by frame).
        /// </summary>
        public bool HasEstimate
        {
            get { return hasEstimate; }
        }

        /// <summary>Forget the history. Call when playback (re)starts.</summary>
        public void Reset()
        {
            previous = null;
            previousTimestamp = 0;
            hasPrevious = false;
            hasEstimate = false;
            smoothed = 0;
            lastSkip = 0;
            hasSkipLevel = false;
        }

        /// <summary>
        /// Feed a rendered frame and return the smoothed motion in luma units per millisecond.
        /// </summary>
        public double Update(Bitmap image, long timestamp)
        {
            if (image == null)
                return smoothed;

            byte[] samples = Sample(image);
            if (samples == null)
                return smoothed;

            if (hasPrevious && previous != null && samples.Length == previous.Length && samples.Length > 0)
            {
                long total = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    int d = samples[i] - previous[i];
                    total += d < 0 ? -d : d;
                }

                double meanDifference = (double)total / samples.Length;
                long delta = timestamp - previousTimestamp;
                if (delta > 0)
                {
                    double motion = meanDifference / delta;
                    smoothed = hasEstimate ? (1 - Smoothing) * smoothed + Smoothing * motion : motion;
                    hasEstimate = true;
                }
            }

            previous = samples;
            previousTimestamp = timestamp;
            hasPrevious = true;

            return smoothed;
        }

        /// <summary>
        /// Number of frames to skip for the current motion: no motion -&gt; maxSkip,
        /// fast motion -&gt; 0. The sensitivity multiplies the reference speed, so a
        /// higher value keeps skipping longer (only faster motion stops it).
        /// The result moves at most one frame per call so that the skip level
        /// ramps up and down instead of oscillating between extremes.
        /// </summary>
        public int GetSkipCount(int maxSkip, double sensitivity = 1.0)
        {
            if (maxSkip <= 0 || !hasEstimate)
                return 0;

            double reference = ReferenceMotionPerMs;
            if (sensitivity > 0)
                reference *= sensitivity;

            double normalized = smoothed / reference;
            if (normalized < 0)
                normalized = 0;
            if (normalized > 1)
                normalized = 1;

            int target = (int)Math.Round(maxSkip * (1 - normalized));

            // Ramp: never jump more than one frame at a time.
            if (!hasSkipLevel)
            {
                lastSkip = target;
                hasSkipLevel = true;
            }
            else if (target > lastSkip)
            {
                lastSkip = lastSkip + 1;
            }
            else if (target < lastSkip)
            {
                lastSkip = lastSkip - 1;
            }

            return lastSkip;
        }

        /// <summary>
        /// Downscaled luma samples of the image, or null if the bitmap cannot be read.
        /// </summary>
        private byte[] Sample(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;
            if (width <= 0 || height <= 0)
                return null;

            int w = width / Step;
            int h = height / Step;
            if (w <= 0) w = 1;
            if (h <= 0) h = 1;

            byte[] result = new byte[w * h];
            BitmapData data = null;

            try
            {
                data = image.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                byte[] row = new byte[data.Stride];
                for (int y = 0; y < h; y++)
                {
                    int sourceY = y * Step;
                    if (sourceY >= height)
                        break;

                    IntPtr rowPointer = (IntPtr)((long)data.Scan0 + (long)sourceY * data.Stride);
                    Marshal.Copy(rowPointer, row, 0, row.Length);

                    for (int x = 0; x < w; x++)
                    {
                        int offset = x * Step * 4;
                        if (offset + 2 >= row.Length)
                            break;

                        int b = row[offset];
                        int g = row[offset + 1];
                        int r = row[offset + 2];
                        result[y * w + x] = (byte)((r * 77 + g * 150 + b * 29) >> 8);
                    }
                }
            }
            catch (Exception)
            {
                // The bitmap may be locked or disposed by the decoder; motion is
                // optional, so silently keep the previous estimate.
                return null;
            }
            finally
            {
                if (data != null)
                {
                    try { image.UnlockBits(data); }
                    catch (Exception) { }
                }
            }

            sampleWidth = w;
            return result;
        }
    }
}
