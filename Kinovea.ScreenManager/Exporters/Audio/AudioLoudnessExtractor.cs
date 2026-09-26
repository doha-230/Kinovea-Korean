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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Kinovea.ScreenManager
{
    public enum AudioLoudnessStatus
    {
        Success,
        NoAudioStream,
        FfmpegNotFound,
        Failed,
        Cancelled,
    }

    public class AudioLoudnessExtractionResult
    {
        public AudioLoudnessStatus Status { get; set; }
        public List<AudioLoudnessSample> Samples { get; set; }
        public string ErrorMessage { get; set; }

        public AudioLoudnessExtractionResult(AudioLoudnessStatus status, List<AudioLoudnessSample> samples, string errorMessage)
        {
            Status = status;
            Samples = samples;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Extracts the audio level over time from a video file.
    ///
    /// Implementation note: the levels are extracted by running the ffmpeg binary
    /// that ships with Kinovea (the video exporter already depends on it) with the
    /// "astats" filter, rather than decoding audio in the reader. The extraction is
    /// done at the audio frame resolution (about 20 ms) and the caller then decides
    /// the window it wants, so changing the window never requires running ffmpeg
    /// again.
    /// </summary>
    public class AudioLoudnessExtractor
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Safety net for very long files, in milliseconds.</summary>
        private const int DefaultTimeoutMs = 30 * 60 * 1000;

        public string FfmpegPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"); }
        }

        public AudioLoudnessExtractionResult Extract(string videoPath, int windowMs)
        {
            return Extract(videoPath, windowMs, CancellationToken.None, DefaultTimeoutMs);
        }

        public AudioLoudnessExtractionResult Extract(string videoPath, int windowMs, CancellationToken cancellationToken, int timeoutMs)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                return new AudioLoudnessExtractionResult(AudioLoudnessStatus.Failed, new List<AudioLoudnessSample>(), "Video file not found.");

            if (!File.Exists(FfmpegPath))
            {
                log.ErrorFormat("ffmpeg.exe not found: {0}", FfmpegPath);
                return new AudioLoudnessExtractionResult(AudioLoudnessStatus.FfmpegNotFound, new List<AudioLoudnessSample>(), "ffmpeg.exe not found.");
            }

            string metadataFile = Path.Combine(Path.GetTempPath(), "kinovea-audio-" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                string arguments = AudioLoudnessParser.BuildArguments(videoPath, metadataFile);
                string errorOutput = string.Empty;
                int exitCode = RunFfmpeg(arguments, cancellationToken, timeoutMs, out errorOutput);

                if (cancellationToken.IsCancellationRequested)
                    return new AudioLoudnessExtractionResult(AudioLoudnessStatus.Cancelled, new List<AudioLoudnessSample>(), "Cancelled.");

                string raw = ReadMetadata(metadataFile);
                List<AudioLoudnessSample> samples = AudioLoudnessParser.Parse(raw);

                if (samples.Count == 0)
                {
                    // Either the file has no audio stream at all, or ffmpeg failed.
                    AudioLoudnessStatus status = exitCode == 0 ? AudioLoudnessStatus.NoAudioStream : AudioLoudnessStatus.Failed;
                    string message = exitCode == 0 ? "No audio stream found in this file." : Trim(errorOutput);
                    return new AudioLoudnessExtractionResult(status, samples, message);
                }

                return new AudioLoudnessExtractionResult(
                    AudioLoudnessStatus.Success,
                    AudioLoudnessParser.Aggregate(samples, windowMs),
                    string.Empty);
            }
            catch (Exception e)
            {
                log.ErrorFormat("Exception while extracting audio loudness: {0}", e);
                return new AudioLoudnessExtractionResult(AudioLoudnessStatus.Failed, new List<AudioLoudnessSample>(), e.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(metadataFile))
                        File.Delete(metadataFile);
                }
                catch (Exception)
                {
                    // Not being able to clean up the temp file is not a failure.
                }
            }
        }

        private int RunFfmpeg(string arguments, CancellationToken cancellationToken, int timeoutMs, out string errorOutput)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = FfmpegPath;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Drain both pipes asynchronously so ffmpeg can never block on a full pipe.
                System.Threading.Tasks.Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errorTask = process.StandardError.ReadToEndAsync();

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (!process.WaitForExit(200))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryKill(process);
                        break;
                    }

                    if (stopwatch.ElapsedMilliseconds > timeoutMs)
                    {
                        log.ErrorFormat("ffmpeg audio extraction timed out after {0} ms.", timeoutMs);
                        TryKill(process);
                        break;
                    }
                }

                errorOutput = SafeResult(errorTask);
                SafeResult(outputTask);

                return process.HasExited ? process.ExitCode : -1;
            }
        }

        private static string SafeResult(System.Threading.Tasks.Task<string> task)
        {
            try
            {
                return task.Wait(2000) ? task.Result : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
            }
        }

        private static string ReadMetadata(string path)
        {
            // ffmpeg may release the file a few milliseconds after exiting.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!File.Exists(path))
                        return string.Empty;

                    return File.ReadAllText(path, Encoding.UTF8);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }

            return string.Empty;
        }

        private static string Trim(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            List<string> kept = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && kept.Count < 5; i--)
            {
                if (lines[i].Trim().Length > 0)
                    kept.Insert(0, lines[i].Trim());
            }

            return string.Join(" / ", kept.ToArray());
        }
    }
}
