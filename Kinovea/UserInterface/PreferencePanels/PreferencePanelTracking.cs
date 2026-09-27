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
            this.Size = new Size(490, 322);

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
        }

        private void RefreshCulture()
        {
            lblScope.Text = RootLang.dlgPreferences_Tracking_Scope;
            chkPredictiveSearch.Text = RootLang.dlgPreferences_Tracking_PredictiveSearch;
            chkRejectOutliers.Text = RootLang.dlgPreferences_Tracking_RejectOutliers;
            chkScaleAdaptive.Text = RootLang.dlgPreferences_Tracking_ScaleAdaptive;
            chkStopOnFailure.Text = RootLang.dlgPreferences_Tracking_StopOnFailure;
            lblStopOnFailureHelp.Text = RootLang.dlgPreferences_Tracking_StopOnFailure_Help;
        }

        private void ReadPreferences()
        {
            TrackingParameters tp = PreferencesManager.PlayerPreferences.TrackingParameters;
            chkPredictiveSearch.Checked = tp.PredictiveSearch;
            chkRejectOutliers.Checked = tp.RejectOutliers;
            chkScaleAdaptive.Checked = tp.ScaleAdaptive;
            chkStopOnFailure.Checked = PreferencesManager.PlayerPreferences.StopTrackingOnFailure;
        }

        public void CommitChanges()
        {
            TrackingParameters tp = PreferencesManager.PlayerPreferences.TrackingParameters;
            tp.PredictiveSearch = chkPredictiveSearch.Checked;
            tp.RejectOutliers = chkRejectOutliers.Checked;
            tp.ScaleAdaptive = chkScaleAdaptive.Checked;

            PreferencesManager.PlayerPreferences.StopTrackingOnFailure = chkStopOnFailure.Checked;
        }

        public void OpenTab(PreferenceTab tab)
        {
        }

        public void Close()
        {
        }
    }
}
