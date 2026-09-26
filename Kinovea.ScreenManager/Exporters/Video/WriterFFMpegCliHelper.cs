using Kinovea.Services;
using Kinovea.Video;
using System;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Converts export settings to command line arguments for ffmpeg.
    /// </summary>
    public class WriterFFMpegCliHelper
    {
        public static string BuildArguments(VideoExportSettings settings, Size inputSize, Size outputSize)
        {
            StringBuilder args = new StringBuilder();

            // Prevent the banner from appearing in the console output.
            Add(args, "-hide_banner");
            Add(args, "-loglevel");
            Add(args, "error");

            // Overwrite output file without asking.
            Add(args, "-y");

            // Input is headerless raw frames through stdin.
            Add(args, "-f");
            Add(args, "rawvideo");

            Add(args, "-pixel_format");
            Add(args, "bgr24");

            Add(args, "-video_size");
            Add(args, string.Format("{0}x{1}", inputSize.Width, inputSize.Height));

            double frameRate = 1000.0 / settings.OutputIntervalMilliseconds;
            Add(args, "-framerate");
            Add(args, frameRate.ToString("0.########", CultureInfo.InvariantCulture));

            Add(args, "-i");
            Add(args, "pipe:0");

            // No audio stream.
            Add(args, "-an");

            if (outputSize.Width != inputSize.Width || outputSize.Height != inputSize.Height)
            {
                Add(args, "-vf");
                Add(args, string.Format("scale={0}:{1}:flags=bicubic", outputSize.Width, outputSize.Height));
            }

            AddVideoEncodingArgs(args, settings);

            string formatString = ExportProfile.GetFormatString(settings.ExportProfile.Container);
            Add(args, "-f");
            Add(args, formatString);
            
            // The output filename must be the final argument.
            Add(args, settings.File);

            return args.ToString();
        }

        private static void AddVideoEncodingArgs(StringBuilder args, VideoExportSettings settings)
        {
            ExportProfile p = settings.ExportProfile;

            switch (p.Codec)
            {
                case VideoCodec.MJPEG:
                    {
                        AddMjpegArgs(args, p);
                        break;
                    }
                case VideoCodec.H264:
                    {
                        HardwareEncoder hardware = PreferencesManager.PlayerPreferences.VideoHardwareEncoder;
                        if (hardware != HardwareEncoder.None)
                            AddH26xHardwareArgs(args, p, GetHardwareEncoderName(hardware, false));
                        else
                            AddH26xArgs(args, p, "libx264");
                        break;
                    }
                case VideoCodec.H265:
                    {
                        HardwareEncoder hardware = PreferencesManager.PlayerPreferences.VideoHardwareEncoder;
                        if (hardware != HardwareEncoder.None)
                            AddH26xHardwareArgs(args, p, GetHardwareEncoderName(hardware, true));
                        else
                            AddH26xArgs(args, p, "libx265");
                        break;
                    }
                default:
                    break;
            }
        }

        private static void AddMjpegArgs(StringBuilder args, ExportProfile p)
        {
            Add(args, "-c:v");
            Add(args, "mjpeg");

            Add(args, "-pix_fmt");
            Add(args, "yuvj420p");

            int q = ExportProfile.GetMJPEGQuality(p.EncodingQuality);
            Add(args, "-q:v");
            Add(args, q.ToString());

            // No preset.
            // No GOP size since we are always intra-only.
        }

        /// <summary>Name of the hardware encoder to use for the requested codec.</summary>
        private static string GetHardwareEncoderName(HardwareEncoder hardware, bool hevc)
        {
            switch (hardware)
            {
                case HardwareEncoder.Nvenc:
                    return hevc ? "hevc_nvenc" : "h264_nvenc";
                case HardwareEncoder.Qsv:
                    return hevc ? "hevc_qsv" : "h264_qsv";
                case HardwareEncoder.Amf:
                    return hevc ? "hevc_amf" : "h264_amf";
                default:
                    return hevc ? "libx265" : "libx264";
            }
        }

        /// <summary>
        /// Hardware encoders do not share the libx264/libx265 options: they bring
        /// their own rate control. The quality target is preserved; the
        /// compression-effort preset only maps to the NVENC p1..p7 presets.
        /// </summary>
        private static void AddH26xHardwareArgs(StringBuilder args, ExportProfile p, string name)
        {
            int quality = ExportProfile.GetCRF(p.EncodingQuality, p.Codec);

            Add(args, "-c:v");
            Add(args, name);

            if (name.EndsWith("_qsv"))
            {
                Add(args, "-pix_fmt");
                Add(args, "nv12");

                // Quality target in ICQ mode: lower is better.
                Add(args, "-global_quality");
                Add(args, quality.ToString());
            }
            else if (name.EndsWith("_nvenc"))
            {
                Add(args, "-pix_fmt");
                Add(args, "yuv420p");

                string preset = "p4";
                switch (p.EncodingSpeed)
                {
                    case EncodingSpeed.Fast:
                        preset = "p1";
                        break;
                    case EncodingSpeed.Medium:
                        preset = "p4";
                        break;
                    case EncodingSpeed.Slow:
                        preset = "p7";
                        break;
                }

                Add(args, "-preset");
                Add(args, preset);
                Add(args, "-rc");
                Add(args, "vbr");
                Add(args, "-cq");
                Add(args, quality.ToString());
                Add(args, "-b:v");
                Add(args, "0");
            }
            else
            {
                // AMD AMF: constant quantizer.
                Add(args, "-pix_fmt");
                Add(args, "yuv420p");
                Add(args, "-rc");
                Add(args, "cqp");
                Add(args, "-qp_i");
                Add(args, quality.ToString());
                Add(args, "-qp_p");
                Add(args, quality.ToString());
            }

            if (p.GOPSize > 0)
            {
                Add(args, "-g");
                Add(args, p.GOPSize.ToString());
            }
        }

        private static void AddH26xArgs(StringBuilder args, ExportProfile p, string name)
        {
            Add(args, "-c:v");
            Add(args, name);

            Add(args, "-pix_fmt");
            Add(args, "yuv420p");
            
            // Speed/compression preset.
            // = Compression effort. Slower preset spends more time seeking better compression.
            // Does not raise or lower the requested quality.
            Add(args, "-preset");
            Add(args, ExportProfile.GetEncodingSpeedPreset(p.EncodingSpeed));

            int crf = ExportProfile.GetCRF(p.EncodingQuality, p.Codec);
            Add(args, "-crf");
            Add(args, crf.ToString());

            // GOP size.
            // 0: encoder default, 1: intra-only.
            if (p.GOPSize > 0)
            {
                Add(args, "-g");
                Add(args, p.GOPSize.ToString());
            }
        }

        private static void Add(StringBuilder args, string value)
        {
            if (args.Length > 0)
            {
                args.Append(' ');
            }

            args.Append(QuoteArg(value));
        }

        private static string QuoteArg(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            bool requiresQuotes = value.Length == 0 || value.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) >= 0;
            if (!requiresQuotes)
            {
                return value;
            }

            StringBuilder result = new StringBuilder();

            result.Append('"');

            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashCount * 2 + 1);
                    result.Append('"');
                    backslashCount = 0;
                    continue;
                }

                result.Append('\\', backslashCount);
                backslashCount = 0;
                result.Append(character);
            }

            // Backslashes immediately preceding the closing quote must themselves be doubled.
            result.Append('\\', backslashCount * 2);
            result.Append('"');

            return result.ToString();
        }
    }
}
