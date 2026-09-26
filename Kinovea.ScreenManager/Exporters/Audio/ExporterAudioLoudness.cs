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
using System.IO;
using System.Text;
using SpreadsheetLight;

namespace Kinovea.ScreenManager
{
    public enum AudioLoudnessExportFormat
    {
        CSV,
        TXT,
        XLSX,
        JSON,
    }

    /// <summary>
    /// Writes an audio loudness time series to a text or spreadsheet file.
    /// Two columns are always exported: RMS (the perceived level) and Peak (the
    /// instantaneous maximum), both in dBFS.
    /// </summary>
    public class ExporterAudioLoudness
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void Export(string path, List<AudioLoudnessSample> samples, int windowMs, AudioLoudnessExportFormat format)
        {
            if (samples == null)
                samples = new List<AudioLoudnessSample>();

            switch (format)
            {
                case AudioLoudnessExportFormat.TXT:
                    ExportText(path, samples, windowMs, true);
                    break;
                case AudioLoudnessExportFormat.XLSX:
                    ExportXLSX(path, samples, windowMs);
                    break;
                case AudioLoudnessExportFormat.JSON:
                    ExportJSON(path, samples, windowMs);
                    break;
                case AudioLoudnessExportFormat.CSV:
                default:
                    ExportText(path, samples, windowMs, false);
                    break;
            }
        }

        /// <summary>
        /// CSV and TXT. CSV honours the user decimal separator and text encoding
        /// preferences, TXT uses tabs and the invariant separator.
        /// </summary>
        private void ExportText(string path, List<AudioLoudnessSample> samples, int windowMs, bool tabSeparated)
        {
            NumberFormatInfo nfi = tabSeparated ? CultureInfo.InvariantCulture.NumberFormat : CSVHelper.GetCSVNFI();
            string separator = tabSeparated ? "\t" : CSVHelper.GetListSeparator(nfi);

            List<string> lines = new List<string>();

            List<string> headers = new List<string>();
            headers.Add(tabSeparated ? "Time (s)" : CSVHelper.WriteCell("Time (s)"));
            headers.Add(tabSeparated ? "RMS (dBFS)" : CSVHelper.WriteCell("RMS (dBFS)"));
            headers.Add(tabSeparated ? "Peak (dBFS)" : CSVHelper.WriteCell("Peak (dBFS)"));
            lines.Add(string.Join(separator, headers.ToArray()));

            foreach (AudioLoudnessSample sample in samples)
            {
                List<string> cells = new List<string>();
                cells.Add(FormatCell(sample.Time, nfi, tabSeparated));
                cells.Add(FormatCell(AudioLoudnessParser.ToExportLevel(sample.Rms), nfi, tabSeparated));
                cells.Add(FormatCell(AudioLoudnessParser.ToExportLevel(sample.Peak), nfi, tabSeparated));
                lines.Add(string.Join(separator, cells.ToArray()));
            }

            File.WriteAllLines(path, lines, CSVHelper.GetEncoding());
            log.DebugFormat("Audio loudness exported to {0} ({1} samples, {2} ms window).", path, samples.Count, windowMs);
        }

        private static string FormatCell(double value, NumberFormatInfo nfi, bool plain)
        {
            string text = value.ToString("0.####", nfi);
            return plain ? text : CSVHelper.WriteCell(text);
        }

        private void ExportXLSX(string path, List<AudioLoudnessSample> samples, int windowMs)
        {
            using (SLDocument sl = new SLDocument())
            {
                sl.SetCellValue(1, 1, "Time (s)");
                sl.SetCellValue(1, 2, "RMS (dBFS)");
                sl.SetCellValue(1, 3, "Peak (dBFS)");

                SLStyle header = sl.CreateStyle();
                header.Font.Bold = true;

                for (int i = 0; i < 3; i++)
                    sl.SetCellStyle(1, i + 1, header);

                int row = 2;
                foreach (AudioLoudnessSample sample in samples)
                {
                    sl.SetCellValue(row, 1, sample.Time);
                    sl.SetCellValue(row, 2, AudioLoudnessParser.ToExportLevel(sample.Rms));
                    sl.SetCellValue(row, 3, AudioLoudnessParser.ToExportLevel(sample.Peak));
                    row++;
                }

                sl.AutoFitColumn(1, 3);
                sl.SaveAs(path);
            }

            log.DebugFormat("Audio loudness exported to {0} ({1} samples, {2} ms window).", path, samples.Count, windowMs);
        }

        private void ExportJSON(string path, List<AudioLoudnessSample> samples, int windowMs)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            builder.AppendFormat(CultureInfo.InvariantCulture, "  \"windowMs\": {0},\n", windowMs);
            builder.Append("  \"unit\": \"dBFS\",\n");
            builder.Append("  \"samples\": [\n");

            for (int i = 0; i < samples.Count; i++)
            {
                AudioLoudnessSample sample = samples[i];
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "    {{ \"time\": {0:0.####}, \"rms\": {1:0.####}, \"peak\": {2:0.####} }}{3}\n",
                    sample.Time,
                    AudioLoudnessParser.ToExportLevel(sample.Rms),
                    AudioLoudnessParser.ToExportLevel(sample.Peak),
                    i == samples.Count - 1 ? string.Empty : ",");
            }

            builder.Append("  ]\n}\n");
            File.WriteAllText(path, builder.ToString(), CSVHelper.GetEncoding());
            log.DebugFormat("Audio loudness exported to {0} ({1} samples, {2} ms window).", path, samples.Count, windowMs);
        }
    }
}
