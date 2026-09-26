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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Turns the text produced by ffmpeg's "astats" + "ametadata=print" filters
    /// into a time series of audio levels, and aggregates it into user windows.
    ///
    /// The raw output looks like this (one block per audio frame):
    ///
    ///   frame:0    pts:0       pts_time:0
    ///   lavfi.astats.1.Peak_level=-43.700458
    ///   lavfi.astats.1.RMS_level=-47.343809
    ///   ...
    ///   lavfi.astats.Overall.RMS_level=-47.343809
    ///
    /// A frame can carry per channel keys (".1.", ".2.", ...) and an "Overall"
    /// key when the channels are mixed: "Overall" is preferred when present.
    /// This class is intentionally free of any ffmpeg or WinForms dependency so
    /// it can be unit tested.
    /// </summary>
    public static class AudioLoudnessParser
    {
        /// <summary>Level written for digital silence (-inf dBFS).</summary>
        public const double SilenceFloor = -100.0;

        /// <summary>Fallback frame duration when ffmpeg does not report pts_time.</summary>
        private const double DefaultFrameDurationSeconds = 0.02;

        /// <summary>
        /// Builds the ffmpeg argument line used to extract the levels.
        /// The metadata is written to <paramref name="metadataFile"/>.
        /// </summary>
        public static string BuildArguments(string videoPath, string metadataFile)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "-nostdin -hide_banner -i \"{0}\" -af \"astats=metadata=1:reset=1,ametadata=print:file={1}\" -f null -",
                videoPath, metadataFile);
        }

        /// <summary>
        /// Parses the raw ffmpeg output. Unknown or missing levels are stored as
        /// NaN, digital silence as negative infinity.
        /// </summary>
        public static List<AudioLoudnessSample> Parse(string text)
        {
            List<AudioLoudnessSample> samples = new List<AudioLoudnessSample>();
            if (string.IsNullOrEmpty(text))
                return samples;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            bool inFrame = false;
            long frameIndex = 0;
            double time = 0;
            double rmsOverall = double.NaN;
            double rmsAny = double.NaN;
            double peakOverall = double.NaN;
            double peakAny = double.NaN;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("frame:", StringComparison.Ordinal))
                {
                    if (inFrame)
                        samples.Add(Flush(time, rmsOverall, rmsAny, peakOverall, peakAny));

                    inFrame = true;
                    frameIndex++;
                    time = ParseFrameTime(line, frameIndex);
                    rmsOverall = double.NaN;
                    rmsAny = double.NaN;
                    peakOverall = double.NaN;
                    peakAny = double.NaN;
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                bool overall = key.IndexOf(".Overall.", StringComparison.Ordinal) >= 0;

                if (key.EndsWith("RMS_level", StringComparison.Ordinal))
                {
                    double level = ParseLevel(value);
                    if (overall)
                        rmsOverall = level;
                    else if (double.IsNaN(rmsAny))
                        rmsAny = level;
                }
                else if (key.EndsWith("Peak_level", StringComparison.Ordinal))
                {
                    double level = ParseLevel(value);
                    if (overall)
                        peakOverall = level;
                    else if (double.IsNaN(peakAny))
                        peakAny = level;
                }
            }

            if (inFrame)
                samples.Add(Flush(time, rmsOverall, rmsAny, peakOverall, peakAny));

            return samples;
        }

        /// <summary>
        /// Groups the samples into fixed windows of <paramref name="windowMs"/>
        /// milliseconds. RMS is averaged in the linear domain (so the result is a
        /// true window RMS), peak is the maximum of the window. The timestamp of a
        /// window is its center.
        /// </summary>
        public static List<AudioLoudnessSample> Aggregate(List<AudioLoudnessSample> samples, int windowMs)
        {
            List<AudioLoudnessSample> result = new List<AudioLoudnessSample>();
            if (samples == null || samples.Count == 0)
                return result;

            int window = windowMs < 1 ? 1 : windowMs;

            long currentBucket = long.MinValue;
            int count = 0;
            double sumLinear = 0;
            double maxPeak = double.NegativeInfinity;

            foreach (AudioLoudnessSample sample in samples)
            {
                if (double.IsNaN(sample.Time) || double.IsInfinity(sample.Time))
                    continue;

                // NaN levels mean "not reported" and must not dilute the average.
                if (double.IsNaN(sample.Rms) && double.IsNaN(sample.Peak))
                    continue;

                long bucket = (long)Math.Floor(sample.Time * 1000.0 / window);
                if (bucket != currentBucket)
                {
                    if (count > 0)
                        result.Add(BuildWindow(currentBucket, window, count, sumLinear, maxPeak));

                    currentBucket = bucket;
                    count = 0;
                    sumLinear = 0;
                    maxPeak = double.NegativeInfinity;
                }

                count++;
                sumLinear += ToLinear(sample.Rms);

                if (!double.IsNaN(sample.Peak) && sample.Peak > maxPeak)
                    maxPeak = sample.Peak;
            }

            if (count > 0)
                result.Add(BuildWindow(currentBucket, window, count, sumLinear, maxPeak));

            return result;
        }

        /// <summary>Value to export for a level: finite levels as-is, silence at the floor.</summary>
        public static double ToExportLevel(double level)
        {
            if (double.IsNaN(level))
                return SilenceFloor;

            if (double.IsNegativeInfinity(level))
                return SilenceFloor;

            return level;
        }

        private static AudioLoudnessSample Flush(double time, double rmsOverall, double rmsAny, double peakOverall, double peakAny)
        {
            double rms = double.IsNaN(rmsOverall) ? rmsAny : rmsOverall;
            double peak = double.IsNaN(peakOverall) ? peakAny : peakOverall;
            return new AudioLoudnessSample(time, rms, peak);
        }

        private static AudioLoudnessSample BuildWindow(long bucket, int windowMs, int count, double sumLinear, double maxPeak)
        {
            double time = (bucket + 0.5) * windowMs / 1000.0;
            double rms = sumLinear <= 0 ? double.NegativeInfinity : 20.0 * Math.Log10(sumLinear / count);
            double peak = double.IsNegativeInfinity(maxPeak) ? double.NegativeInfinity : maxPeak;
            return new AudioLoudnessSample(time, rms, peak);
        }

        private static double ToLinear(double level)
        {
            if (double.IsNaN(level) || double.IsNegativeInfinity(level))
                return 0;

            return Math.Pow(10.0, level / 20.0);
        }

        private static double ParseFrameTime(string line, long frameIndex)
        {
            int index = line.IndexOf("pts_time:", StringComparison.Ordinal);
            if (index < 0)
                return frameIndex * DefaultFrameDurationSeconds;

            string tail = line.Substring(index + "pts_time:".Length).Trim();
            int end = tail.IndexOf(' ');
            if (end >= 0)
                tail = tail.Substring(0, end);

            double time;
            if (double.TryParse(tail, NumberStyles.Float, CultureInfo.InvariantCulture, out time))
                return time;

            return frameIndex * DefaultFrameDurationSeconds;
        }

        private static double ParseLevel(string value)
        {
            if (value.Length == 0)
                return double.NaN;

            if (value.StartsWith("-inf", StringComparison.OrdinalIgnoreCase) || value.StartsWith("inf", StringComparison.OrdinalIgnoreCase))
                return double.NegativeInfinity;

            if (value.StartsWith("nan", StringComparison.OrdinalIgnoreCase))
                return double.NaN;

            double level;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out level))
                return level;

            return double.NaN;
        }
    }
}
