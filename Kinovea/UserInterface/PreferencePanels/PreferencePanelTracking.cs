using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.Root.Languages;
using Kinovea.Root.Properties;
using Kinovea.Services;

namespace Kinovea.Root
{
    /// <summary>
    /// Preferences panel exposing the default tracking parameters used for new tracks,
    /// and the behaviour of the player when a match fails.
    /// </summary>
    public class PreferencePanelTracking : UserControl, IPreferencePanel
    {
        private List<PreferenceTab> tabs = new List<PreferenceTab>() { PreferenceTab.Tracking_General };

        private Label lblScope;
        private CheckBox chkPredictiveSearch;
        private CheckBox chkRejectOutliers;
        private CheckBox chkScaleAdaptive;
        private CheckBox chkStopOnFailure;
        private CheckBox chkCandidatesEnabled;
        private Label lblCandidateMax;
        private NumericUpDown nudCandidateMax;
        private Label lblHsvRange;
        private Label lblHue;
        private NumericUpDown nudHueMin;
        private NumericUpDown nudHueMax;
        private Label lblSaturation;
        private NumericUpDown nudSaturationMin;
        private NumericUpDown nudSaturationMax;
        private Label lblValue;
        private NumericUpDown nudValueMin;
        private NumericUpDown nudValueMax;
        private Label lblDilateErode;
        private NumericUpDown nudDilate;
        private NumericUpDown nudErode;
        private CheckBox chkRetryOnFailure;
        private CheckBox chkValidateParameters;
        private CheckBox chkPanelExtras;
        private CheckBox chkFollowObject;
        private Label lblStopOnFailureHelp;

        public string Description
        {
            get { return RootLang.dlgPreferences_Player_TrackingDescription; }
        }

        public Bitmap Icon
        {
            get { return Resources.circled_play_button_30; }
        }

        public List<PreferenceTab> Tabs
        {
            get { return tabs; }
        }

        public PreferencePanelTracking()
        {
            BuildUi();
            RefreshCulture();
            ReadPreferences();
        }

        private void BuildUi()
        {
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Size = new Size(490, 372);

            lblScope = new Label();
            lblScope.AutoSize = true;
            lblScope.Location = new Point(18, 20);

            chkPredictiveSearch = new CheckBox();
            chkPredictiveSearch.AutoSize = true;
            chkPredictiveSearch.Location = new Point(18, 50);
            chkPredictiveSearch.UseVisualStyleBackColor = true;

            chkRejectOutliers = new CheckBox();
            chkRejectOutliers.AutoSize = true;
            chkRejectOutliers.Location = new Point(18, 78);
            chkRejectOutliers.UseVisualStyleBackColor = true;

            chkScaleAdaptive = new CheckBox();
            chkScaleAdaptive.AutoSize = true;
            chkScaleAdaptive.Location = new Point(18, 106);
            chkScaleAdaptive.UseVisualStyleBackColor = true;

            chkStopOnFailure = new CheckBox();
            chkStopOnFailure.AutoSize = true;
            chkStopOnFailure.Location = new Point(18, 158);
            chkStopOnFailure.UseVisualStyleBackColor = true;

            lblStopOnFailureHelp = new Label();
            lblStopOnFailureHelp.Location = new Point(36, 182);
            lblStopOnFailureHelp.Size = new Size(430, 34);

            this.Controls.Add(lblScope);
            this.Controls.Add(chkPredictiveSearch);
            this.Controls.Add(chkRejectOutliers);
            this.Controls.Add(chkScaleAdaptive);
            this.Controls.Add(chkStopOnFailure);
            this.Controls.Add(lblStopOnFailureHelp);

            // Blob (HSV) bounds. Used by the Blob tracking algorithm and applied to new tracks.
            lblHsvRange = new Label();
            lblHsvRange.AutoSize = true;
            lblHsvRange.Location = new Point(18, 226);

            lblHue = new Label();
            lblHue.AutoSize = true;
            lblHue.Location = new Point(18, 254);

            nudHueMin = MakeNud(new Point(96, 251));
            nudHueMax = MakeNud(new Point(160, 251));

            lblSaturation = new Label();
            lblSaturation.AutoSize = true;
            lblSaturation.Location = new Point(18, 280);

            nudSaturationMin = MakeNud(new Point(96, 277));
            nudSaturationMax = MakeNud(new Point(160, 277));

            lblValue = new Label();
            lblValue.AutoSize = true;
            lblValue.Location = new Point(18, 306);

            nudValueMin = MakeNud(new Point(96, 303));
            nudValueMax = MakeNud(new Point(160, 303));

            lblDilateErode = new Label();
            lblDilateErode.AutoSize = true;
            lblDilateErode.Location = new Point(240, 254);

            nudDilate = MakeNud(new Point(318, 251));
            nudErode = MakeNud(new Point(382, 251));
            nudDilate.Maximum = 10;
            nudErode.Maximum = 10;

            // Hue runs from 0 to 179 on an 8-bit HSV image, the others from 0 to 255.
            nudHueMin.Maximum = 179;
            nudHueMax.Maximum = 179;

            // Behaviour of the tracking itself. All of these are off by default so that
            // a fresh installation tracks exactly like upstream.
            chkRetryOnFailure = new CheckBox();
            chkRetryOnFailure.AutoSize = true;
            chkRetryOnFailure.Location = new Point(240, 224);
            chkRetryOnFailure.UseVisualStyleBackColor = true;

            chkValidateParameters = new CheckBox();
            chkValidateParameters.AutoSize = true;
            chkValidateParameters.Location = new Point(240, 275);
            chkValidateParameters.UseVisualStyleBackColor = true;

            chkPanelExtras = new CheckBox();
            chkPanelExtras.AutoSize = true;
            chkPanelExtras.Location = new Point(240, 301);
            chkPanelExtras.UseVisualStyleBackColor = true;

            this.Controls.Add(chkRetryOnFailure);
            this.Controls.Add(chkValidateParameters);
            this.Controls.Add(chkPanelExtras);

            chkFollowObject = new CheckBox();
            chkFollowObject.AutoSize = true;
            chkFollowObject.Location = new Point(18, 324);
            chkFollowObject.UseVisualStyleBackColor = true;
            this.Controls.Add(chkFollowObject);

            chkCandidatesEnabled = new CheckBox();
            chkCandidatesEnabled.AutoSize = true;
            chkCandidatesEnabled.Location = new Point(18, 350);
            chkCandidatesEnabled.UseVisualStyleBackColor = true;
            this.Controls.Add(chkCandidatesEnabled);

            lblCandidateMax = new Label();
            lblCandidateMax.AutoSize = true;
            lblCandidateMax.Location = new Point(286, 352);
            this.Controls.Add(lblCandidateMax);

            nudCandidateMax = MakeNud(new Point(400, 350));
            nudCandidateMax.Minimum = 1;
            nudCandidateMax.Maximum = 8;
            this.Controls.Add(nudCandidateMax);

            this.Controls.Add(lblHsvRange);
            this.Controls.Add(lblHue);
            this.Controls.Add(nudHueMin);
            this.Controls.Add(nudHueMax);
            this.Controls.Add(lblSaturation);
            this.Controls.Add(nudSaturationMin);
            this.Controls.Add(nudSaturationMax);
            this.Controls.Add(lblValue);
            this.Controls.Add(nudValueMin);
            this.Controls.Add(nudValueMax);
            this.Controls.Add(lblDilateErode);
            this.Controls.Add(nudDilate);
            this.Controls.Add(nudErode);
        }

        /// <summary>
        /// Small numeric field for the HSV bounds.
        /// </summary>
        private NumericUpDown MakeNud(Point location)
        {
            NumericUpDown nud = new NumericUpDown();
            nud.Location = location;
            nud.Size = new Size(56, 20);
            nud.Minimum = 0;
            nud.Maximum = 255;
            return nud;
        }

        private void RefreshCulture()
        {
            lblScope.Text = RootLang.dlgPreferences_Tracking_Scope;
            chkPredictiveSearch.Text = RootLang.dlgPreferences_Tracking_PredictiveSearch;
            chkRejectOutliers.Text = RootLang.dlgPreferences_Tracking_RejectOutliers;
            chkScaleAdaptive.Text = RootLang.dlgPreferences_Tracking_ScaleAdaptive;
            chkStopOnFailure.Text = RootLang.dlgPreferences_Tracking_StopOnFailure;
            lblStopOnFailureHelp.Text = RootLang.dlgPreferences_Tracking_StopOnFailure_Help;
            lblHsvRange.Text = RootLang.dlgPreferences_Tracking_HsvRange;
            chkCandidatesEnabled.Text = RootLang.dlgPreferences_Tracking_Candidates;
            lblCandidateMax.Text = RootLang.dlgPreferences_Tracking_CandidateMax;
            lblHue.Text = RootLang.dlgPreferences_Tracking_Hue;
            lblSaturation.Text = RootLang.dlgPreferences_Tracking_Saturation;
            lblValue.Text = RootLang.dlgPreferences_Tracking_Value;
            lblDilateErode.Text = RootLang.dlgPreferences_Tracking_DilateErode;
            chkRetryOnFailure.Text = RootLang.dlgPreferences_Tracking_RetryOnFailure;
            chkValidateParameters.Text = RootLang.dlgPreferences_Tracking_ValidateParameters;
            chkPanelExtras.Text = RootLang.dlgPreferences_Tracking_PanelExtras;
            chkFollowObject.Text = RootLang.dlgPreferences_Tracking_FollowObject;
        }

        private void ReadPreferences()
        {
            TrackingParameters tp = PreferencesManager.PlayerPreferences.TrackingParameters;
            chkPredictiveSearch.Checked = tp.PredictiveSearch;
            chkRejectOutliers.Checked = tp.RejectOutliers;
            chkScaleAdaptive.Checked = tp.ScaleAdaptive;
            chkStopOnFailure.Checked = PreferencesManager.PlayerPreferences.StopTrackingOnFailure;
            chkCandidatesEnabled.Checked = PreferencesManager.PlayerPreferences.TrackingCandidatesEnabled;
            nudCandidateMax.Value = Math.Max(nudCandidateMax.Minimum,
                Math.Min(nudCandidateMax.Maximum, PreferencesManager.PlayerPreferences.TrackingCandidateMax));
            chkRetryOnFailure.Checked = PreferencesManager.PlayerPreferences.TrackingRetryOnFailure;
            chkValidateParameters.Checked = PreferencesManager.PlayerPreferences.TrackingValidateParameters;
            chkPanelExtras.Checked = PreferencesManager.PlayerPreferences.TrackingPanelExtras;
            chkFollowObject.Checked = PreferencesManager.PlayerPreferences.TrackingFollowObject;

            HSVRange hsv = tp.HSVRange;
            nudHueMin.Value = Clamp(hsv.HueMin, 179);
            nudHueMax.Value = Clamp(hsv.HueMax, 179);
            nudSaturationMin.Value = Clamp(hsv.SaturationMin);
            nudSaturationMax.Value = Clamp(hsv.SaturationMax);
            nudValueMin.Value = Clamp(hsv.ValueMin);
            nudValueMax.Value = Clamp(hsv.ValueMax);
            nudDilate.Value = Clamp(tp.Dilate, 10);
            nudErode.Value = Clamp(tp.Erode, 10);
        }

        private static decimal Clamp(float value)
        {
            return Clamp(value, 255);
        }

        private static decimal Clamp(float value, int maximum)
        {
            return (decimal)Math.Max(0, Math.Min(maximum, value));
        }

        public void CommitChanges()
        {
            TrackingParameters tp = PreferencesManager.PlayerPreferences.TrackingParameters;
            tp.PredictiveSearch = chkPredictiveSearch.Checked;
            tp.RejectOutliers = chkRejectOutliers.Checked;
            tp.ScaleAdaptive = chkScaleAdaptive.Checked;

            HSVRange hsv = tp.HSVRange;
            hsv.HueMin = (float)nudHueMin.Value;
            hsv.HueMax = (float)nudHueMax.Value;
            hsv.SaturationMin = (float)nudSaturationMin.Value;
            hsv.SaturationMax = (float)nudSaturationMax.Value;
            hsv.ValueMin = (float)nudValueMin.Value;
            hsv.ValueMax = (float)nudValueMax.Value;
            tp.Dilate = (int)nudDilate.Value;
            tp.Erode = (int)nudErode.Value;

            PreferencesManager.PlayerPreferences.StopTrackingOnFailure = chkStopOnFailure.Checked;
            PreferencesManager.PlayerPreferences.TrackingRetryOnFailure = chkRetryOnFailure.Checked;
            PreferencesManager.PlayerPreferences.TrackingValidateParameters = chkValidateParameters.Checked;
            PreferencesManager.PlayerPreferences.TrackingPanelExtras = chkPanelExtras.Checked;
            PreferencesManager.PlayerPreferences.TrackingFollowObject = chkFollowObject.Checked;

            // Nested objects (the tracking parameters above) do not trigger a save on their own,
            // so save explicitly once everything is written.
            PreferencesManager.PlayerPreferences.TrackingCandidatesEnabled = chkCandidatesEnabled.Checked;
            PreferencesManager.PlayerPreferences.TrackingCandidateMax = (int)nudCandidateMax.Value;
            PreferencesManager.Save();
        }

        public void OpenTab(PreferenceTab tab)
        {
        }

        public void Close()
        {
        }
    }
}
