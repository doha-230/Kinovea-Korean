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
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Small options dialog for the audio loudness export: aggregation window and
    /// output format. Built in code so there is no designer file to maintain.
    /// </summary>
    public class FormAudioLoudnessExport : Form
    {
        private NumericUpDown nudWindow;
        private ComboBox cmbFormat;
        private Button btnOk;
        private Button btnCancel;

        /// <summary>Aggregation window chosen by the user, in milliseconds.</summary>
        public int WindowMs
        {
            get { return (int)nudWindow.Value; }
        }

        /// <summary>Output format chosen by the user.</summary>
        public AudioLoudnessExportFormat Format
        {
            get
            {
                switch (cmbFormat.SelectedIndex)
                {
                    case 1:
                        return AudioLoudnessExportFormat.XLSX;
                    case 2:
                        return AudioLoudnessExportFormat.TXT;
                    case 3:
                        return AudioLoudnessExportFormat.JSON;
                    case 0:
                    default:
                        return AudioLoudnessExportFormat.CSV;
                }
            }
        }

        public FormAudioLoudnessExport(int windowMs)
        {
            BuildUi();
            nudWindow.Value = Math.Max(nudWindow.Minimum, Math.Min(windowMs, nudWindow.Maximum));
        }

        private void BuildUi()
        {
            Text = ScreenManagerLang.dlgAudioLoudness_Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 130);
            ShowInTaskbar = false;

            Label lblWindow = new Label();
            lblWindow.Text = ScreenManagerLang.dlgAudioLoudness_Window;
            lblWindow.Location = new Point(16, 22);
            lblWindow.AutoSize = true;

            nudWindow = new NumericUpDown();
            nudWindow.Location = new Point(220, 19);
            nudWindow.Size = new Size(120, 22);
            nudWindow.Minimum = 10;
            nudWindow.Maximum = 5000;
            nudWindow.Increment = 10;
            nudWindow.ThousandsSeparator = true;

            Label lblFormat = new Label();
            lblFormat.Text = ScreenManagerLang.dlgAudioLoudness_Format;
            lblFormat.Location = new Point(16, 56);
            lblFormat.AutoSize = true;

            cmbFormat = new ComboBox();
            cmbFormat.Location = new Point(220, 53);
            cmbFormat.Size = new Size(120, 22);
            cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFormat.Items.Add("CSV");
            cmbFormat.Items.Add(ScreenManagerLang.dlgAudioLoudness_FormatExcel);
            cmbFormat.Items.Add(ScreenManagerLang.dlgAudioLoudness_FormatText);
            cmbFormat.Items.Add("JSON");
            cmbFormat.SelectedIndex = 0;

            Label lblHint = new Label();
            lblHint.Text = ScreenManagerLang.dlgAudioLoudness_Hint;
            lblHint.Location = new Point(16, 82);
            lblHint.AutoSize = true;
            lblHint.ForeColor = SystemColors.GrayText;

            btnOk = new Button();
            btnOk.Text = ScreenManagerLang.Generic_OK;
            btnOk.Location = new Point(184, 100);
            btnOk.Size = new Size(76, 24);
            btnOk.DialogResult = DialogResult.OK;

            btnCancel = new Button();
            btnCancel.Text = ScreenManagerLang.Generic_Cancel;
            btnCancel.Location = new Point(264, 100);
            btnCancel.Size = new Size(76, 24);
            btnCancel.DialogResult = DialogResult.Cancel;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblWindow);
            Controls.Add(nudWindow);
            Controls.Add(lblFormat);
            Controls.Add(cmbFormat);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }
}
