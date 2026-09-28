using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Kinovea.ScreenManager;

namespace Kinovea.Tests.Player
{
    /// <summary>
    /// Tests for the audio loudness extraction (parser + window aggregation).
    /// The parser is deliberately free of ffmpeg/WinForms dependencies so the
    /// whole pipeline except the ffmpeg call itself can be verified here.
    /// </summary>
    public static class AudioLoudnessTest
    {
        public static int Run()
        {
            int failures = 0;
            failures += Check("parse: empty input yields no samples", ParseEmpty());
            failures += Check("parse: frame blocks, times and levels", ParseFrames());
            failures += Check("parse: Overall level preferred over channel", PreferOverall());
            failures += Check("parse: falls back to channel level", FallbackToChannel());
            failures += Check("parse: digital silence is -infinity, exported as the floor", SilenceHandling());
            failures += Check("parse: missing pts_time falls back to a nominal step", MissingPtsTime());
            failures += Check("aggregate: window count and centre timestamps", AggregateWindows());
            failures += Check("aggregate: RMS powers averaged in the linear domain", AggregatePowerMean());
            failures += Check("aggregate: missing RMS does not dilute a valid RMS", AggregateMissingRms());
            failures += Check("aggregate: peak is the window maximum", AggregatePeak());
            failures += Check("aggregate: empty input yields no windows", AggregateEmpty());
            failures += Check("command line: contains the astats/ametadata chain and the file", BuildArguments());
            failures += Check("integration: bundled ffmpeg extracts audio through a temporary directory", ExtractWithFfmpeg());
            return failures;
        }

        private static int Check(string name, bool ok)
        {
            Console.WriteLine("  [{0}] {1}", ok ? "PASS" : "FAIL", name);
            return ok ? 0 : 1;
        }

        private static bool ParseEmpty()
        {
            return AudioLoudnessParser.Parse(string.Empty).Count == 0
                && AudioLoudnessParser.Parse(null).Count == 0;
        }

        private static bool ParseFrames()
        {
            string text =
                "frame:0    pts:0       pts_time:0\r\n" +
                "lavfi.astats.Overall.Peak_level=-10.5\r\n" +
                "lavfi.astats.Overall.RMS_level=-20.25\r\n" +
                "frame:1    pts:1024    pts_time:0.02\r\n" +
                "lavfi.astats.Overall.Peak_level=-11.5\r\n" +
                "lavfi.astats.Overall.RMS_level=-21.25\r\n";

            List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(text);
            if (samples.Count != 2)
                return false;

            return Near(samples[0].Time, 0) && Near(samples[0].Rms, -20.25) && Near(samples[0].Peak, -10.5)
                && Near(samples[1].Time, 0.02) && Near(samples[1].Rms, -21.25) && Near(samples[1].Peak, -11.5);
        }

        private static bool PreferOverall()
        {
            string text =
                "frame:0    pts:0       pts_time:0\r\n" +
                "lavfi.astats.1.RMS_level=-30\r\n" +
                "lavfi.astats.Overall.RMS_level=-40\r\n" +
                "lavfi.astats.1.Peak_level=-25\r\n" +
                "lavfi.astats.Overall.Peak_level=-35\r\n";

            List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(text);
            return samples.Count == 1 && Near(samples[0].Rms, -40) && Near(samples[0].Peak, -35);
        }

        private static bool FallbackToChannel()
        {
            string text =
                "frame:0    pts:0       pts_time:0\r\n" +
                "lavfi.astats.1.RMS_level=-30\r\n" +
                "lavfi.astats.2.RMS_level=-50\r\n";

            List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(text);
            return samples.Count == 1 && Near(samples[0].Rms, -30);
        }

        private static bool SilenceHandling()
        {
            string text =
                "frame:0    pts:0       pts_time:0\r\n" +
                "lavfi.astats.Overall.RMS_level=-inf\r\n" +
                "lavfi.astats.Overall.Peak_level=-inf\r\n";

            List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(text);
            if (samples.Count != 1)
                return false;

            bool infinity = double.IsNegativeInfinity(samples[0].Rms) && double.IsNegativeInfinity(samples[0].Peak);
            bool floor = Near(AudioLoudnessParser.ToExportLevel(samples[0].Rms), AudioLoudnessParser.SilenceFloor);
            return infinity && floor;
        }

        private static bool MissingPtsTime()
        {
            string text =
                "frame:0    pts:0\r\n" +
                "lavfi.astats.Overall.RMS_level=-20\r\n" +
                "frame:1    pts:1024\r\n" +
                "lavfi.astats.Overall.RMS_level=-20\r\n";

            List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(text);
            return samples.Count == 2 && Near(samples[0].Time, 0.02) && Near(samples[1].Time, 0.04);
        }

        private static bool AggregateWindows()
        {
            // 10 samples every 20 ms => 200 ms, window of 100 ms => 2 windows.
            List<AudioLoudnessSample> samples = new List<AudioLoudnessSample>();
            for (int i = 0; i < 10; i++)
                samples.Add(new AudioLoudnessSample(i * 0.02, -20, -10));

            List<AudioLoudnessSample> windows = AudioLoudnessParser.Aggregate(samples, 100);
            if (windows.Count != 2)
                return false;

            return Near(windows[0].Time, 0.05) && Near(windows[1].Time, 0.15)
                && Near(windows[0].Rms, -20, 0.01);
        }

        private static bool AggregatePowerMean()
        {
            // Two samples at -20 dBFS (power 0.01) and two silent ones.
            // Mean power = 0.005 => -23.01 dBFS.
            List<AudioLoudnessSample> samples = new List<AudioLoudnessSample>();
            samples.Add(new AudioLoudnessSample(0.000, -20, -20));
            samples.Add(new AudioLoudnessSample(0.020, -20, -20));
            samples.Add(new AudioLoudnessSample(0.040, double.NegativeInfinity, double.NegativeInfinity));
            samples.Add(new AudioLoudnessSample(0.060, double.NegativeInfinity, double.NegativeInfinity));

            List<AudioLoudnessSample> windows = AudioLoudnessParser.Aggregate(samples, 100);
            return windows.Count == 1 && Near(windows[0].Rms, -23.01, 0.05);
        }

        private static bool AggregateMissingRms()
        {
            List<AudioLoudnessSample> samples = new List<AudioLoudnessSample>();
            samples.Add(new AudioLoudnessSample(0.000, -20, -10));
            samples.Add(new AudioLoudnessSample(0.020, double.NaN, -5));

            List<AudioLoudnessSample> windows = AudioLoudnessParser.Aggregate(samples, 100);
            return windows.Count == 1 && Near(windows[0].Rms, -20) && Near(windows[0].Peak, -5);
        }

        private static bool AggregatePeak()
        {
            List<AudioLoudnessSample> samples = new List<AudioLoudnessSample>();
            samples.Add(new AudioLoudnessSample(0.000, -30, -25));
            samples.Add(new AudioLoudnessSample(0.020, -10, -3));

            List<AudioLoudnessSample> windows = AudioLoudnessParser.Aggregate(samples, 100);
            return windows.Count == 1 && Near(windows[0].Peak, -3);
        }

        private static bool AggregateEmpty()
        {
            return AudioLoudnessParser.Aggregate(null, 50).Count == 0
                && AudioLoudnessParser.Aggregate(new List<AudioLoudnessSample>(), 50).Count == 0;
        }

        private static bool BuildArguments()
        {
            string arguments = AudioLoudnessParser.BuildArguments(@"C:\clips\jump.mp4", "meta.txt");
            return arguments.Contains("astats=metadata=1")
                && arguments.Contains(" -vn -af ")
                && arguments.Contains("ametadata=print")
                && arguments.Contains("jump.mp4")
                && arguments.Contains("ametadata=print:file=meta.txt")
                && !arguments.Contains(@"file=C:");
        }

        private static bool ExtractWithFfmpeg()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string ffmpegPath = Path.Combine(solutionRoot, "Kinovea", "bin", "x64", "Release", "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
                return false;

            string directory = Path.Combine(Path.GetTempPath(), "kinovea audio 한글 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string audioPath = Path.Combine(directory, "test audio.wav");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = ffmpegPath;
                startInfo.Arguments = "-nostdin -hide_banner -loglevel error -f lavfi -i sine=frequency=440:duration=0.1 -y \"" + audioPath + "\"";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardError = true;

                using (Process process = Process.Start(startInfo))
                {
                    var errorTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(10000))
                    {
                        process.Kill();
                        return false;
                    }
                    errorTask.Wait(2000);
                    if (process.ExitCode != 0)
                        return false;
                }

                AudioLoudnessExtractor extractor = new AudioLoudnessExtractor(ffmpegPath);
                AudioLoudnessExtractionResult result = extractor.Extract(audioPath, 100);
                return result.Status == AudioLoudnessStatus.Success && result.Samples.Count > 0 &&
                    !double.IsNaN(result.Samples[0].Rms);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static bool Near(double actual, double expected, double tolerance = 1e-9)
        {
            if (double.IsNaN(actual) || double.IsNaN(expected))
                return false;

            return Math.Abs(actual - expected) <= tolerance;
        }
    }
}
