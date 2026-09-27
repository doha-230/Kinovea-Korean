using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kinovea.Services;


namespace Kinovea.ScreenManager
{
    /// <summary>
    /// This controls exposes Tracking UI for the active track.
    /// It is used in the side panel.
    /// 
    /// This control contains a mini viewport with a track in "solo mode".
    /// It must handle the following events gracefully and sync with the main viewport.
    /// - drawing selected
    /// - drawing deleted
    /// - tracking status changed
    /// - tracking parameters changed, incl. search/template boxes and thresholds.
    /// - search box is moving
    /// - current point was moved.
    /// Some of these events come from the outside, some can be triggered in the 
    /// control itself, either in the mini viewport, nuds or buttons.
    /// 
    /// Some of these events are raised by the drawing itself, others by a container (metadata, viewport).
    /// We have two viewports handling the same drawing at the same time.
    /// 
    /// Must handle undo/redo gracefully.
    /// </summary>
    public partial class ControlDrawingTrackingSetup : UserControl
    {
        #region Events
        public event EventHandler<DrawingEventArgs> DrawingModified;
        #endregion

        #region Properties
        /// <summary>
        /// Returns true if any text editor is being edited.
        /// This must be consulted before triggering a shortcut that would conflict with text input.
        /// </summary>
        public bool Editing
        {
            get { return editing; }
        }
        #endregion

        #region Members
        private AbstractDrawing drawing;
        private DrawingTrack track;
        private Metadata metadata;
        private Guid managerId;
        private bool manualUpdate;

        /// <summary>Selected candidate index, -1 when the overlay view is selected.</summary>
        private int selectedCandidateIndex = -1;
        private bool updatingCandidatesUi;
        private bool editing;
        public static readonly List<TrackingAlgorithm> options = new List<TrackingAlgorithm>() {
            TrackingAlgorithm.Correlation,
            TrackingAlgorithm.Circle,
            TrackingAlgorithm.Blob,
        };

        // Viewport
        private ViewportController viewportController = new ViewportController(false, false, false);
        private MetadataRenderer metadataRenderer;
        private MetadataManipulator metadataManipulator;
        private ScreenToolManager screenToolManager = new ScreenToolManager();
        private System.Windows.Forms.Timer interactionTimer = new System.Windows.Forms.Timer();
        private IDrawingHostView hostView;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor
        public ControlDrawingTrackingSetup()
        {
            InitializeComponent();
            RefreshCulture();

            this.Paint += Control_Paint;
            cbTrackingAlgorithm.DrawItem += cbTrackingAlgorithm_DrawItem;

            pnlViewport.Controls.Add(viewportController.View);

            // The live tracking status is an overlay on top of the mini viewport.
            lblTrackingLive.BringToFront();
            viewportController.View.Dock = DockStyle.Fill;
            viewportController.View.DoubleClick += pnlViewport_DoubleClick;
            viewportController.View.MouseEnter += miniViewport_MouseEnter;
            viewportController.View.MouseLeave += miniViewport_MouseLeave;

            NudHelper.FixNudScroll(nudSearchWindowWidth);
            NudHelper.FixNudScroll(nudSearchWindowHeight);
            NudHelper.FixNudScroll(nudObjWindowWidth);
            NudHelper.FixNudScroll(nudObjWindowHeight);
            NudHelper.FixNudScroll(nudMatchTreshold);
            NudHelper.FixNudScroll(nudUpdateThreshold);

            btnStartStop.Image = Properties.Drawings.play_green2;
            btnStartStop.ImageAlign = ContentAlignment.MiddleLeft;

            btnTrimTrack.Image = Properties.Drawings.tracking_trim;
                    UpdatePanelExtras();
}

        public void RefreshCulture()
        {
            grpTracking.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_Tracking;
            lblTrackingAlgorithm.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_TrackingAlgorithm;
            lblSearchWindow.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_SearchWindow;
            lblObjectWindow.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_ObjectWindow;
            lblMatchThreshold.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_MatchThreshold;
            lblUpdateThreshold.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.track_UpdateThreshold;
            btnStartStop.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_Start;
            btnTrackAll.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_TrackAll;
            btnApplyToAll.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_ApplyToAllTracks;
            grpCandidates.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_Candidates;
            btnCandidateAdd.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateAdd;
            btnCandidateDuplicate.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateDuplicate;
            btnCandidateRemove.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateRemove;
            btnCandidateRun.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateRun;
            btnCandidateAdopt.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateAdopt;
            UpdateCandidatesUi();
            chkPredictiveSearch.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_PredictiveSearch;
            btnTrimTrack.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_DeleteEndOfTrack;
            lblTrackingLive.Text = string.Empty;
        }
        #endregion

        #region Public methods
        public void SetHostView(IDrawingHostView hostView)
        {
            this.hostView = hostView;
        }

        public void SetMetadata(Metadata metadata)
        {
            ForgetMetadata();
            this.metadata = metadata;

            metadataRenderer = new MetadataRenderer(metadata, true);

            metadataManipulator = new MetadataManipulator(metadata, screenToolManager);
            metadataManipulator.SetFixedTimestamp(hostView.CurrentTimestamp);
            metadataManipulator.SetFixedKeyframe(-1);
            metadataManipulator.DrawingModified += MetadataManipulator_DrawingModified;

            viewportController.MetadataRenderer = metadataRenderer;
            viewportController.MetadataManipulator = metadataManipulator;
        }

        /// <summary>
        /// Set the drawing this control is managing.
        /// This is called when a drawing is selected or "nothing" is selected.
        /// </summary>
        public void SetDrawing(AbstractDrawing drawing, Metadata metadata, Guid managerId, Guid drawingId)
        {
            if (metadata != null && metadata != this.metadata)
            {
                SetMetadata(metadata);
            }
            
            // Bail out if deselected.
            if (drawing == null)
            {
                manualUpdate = true;
                ForgetDrawing();
                manualUpdate = false;
                return;
            }

            // Bail out if it's the same drawing we are already managing.
            if (this.drawing != null && drawing != null && this.drawing.Id == drawing.Id)
            {
                return;
            }

            manualUpdate = true;
            ForgetDrawing();

            this.drawing = drawing;
            this.track = drawing as DrawingTrack;
            this.managerId = managerId;

            if (drawing == null || !(drawing is IDecorable))
            {
                manualUpdate = false;
                return;
            }

            metadataRenderer.SetSoloMode(true, drawing.Id, true);
            screenToolManager.SetSoloMode(true, drawing.Id, true);

            UpdateContent();

            if (track != null)
            {
                track.TrackingStatusChanged += Track_TrackingStatusChanged;
            }
            
            // Interaction timer for the mini viewport.
            // Right now this is constantly turned on. 
            // Maybe we can enable this only when mouse is over the mini viewport.
            // but we also want to update when the object is moved from the main viewport.
            interactionTimer.Interval = 40; // 25 fps.
            interactionTimer.Tick += InteractionTimer_Tick;
            interactionTimer.Start();

            SetupControls();

            manualUpdate = false;
        }

        /// <summary>
        /// The timestamp, bitmap or tracking parameters were updated from the main viewport.
        /// Update video image and recenter.
        /// </summary>
        /// <summary>
        /// Refresh the live tracking status line (progress and match score).
        /// </summary>
        private void UpdateLiveStatus()
        {
            if (track == null || track.Status != TrackStatus.Edit || metadata == null)
            {
                // Keep the candidate tabs in sync (count and ranks) without rebuilding them every frame.
                UpdateCandidatesUi();

                // Idle: report the size of the path and how much of it looks stuck.
                lblTrackingLive.Text = string.Empty;
                lblTrackingLive.ForeColor = System.Drawing.SystemColors.ControlText;

                if (track != null)
                {
                    int points;
                    int stuck;
                    double maxJump;
                    track.GetTrackingQuality(out points, out stuck, out maxJump);

                    if (points > 0)
                    {
                        lblTrackingLive.Text = string.Format(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_QualitySummary, points, stuck);
                        lblTrackingLive.ForeColor = stuck > 0 ? System.Drawing.Color.Firebrick : System.Drawing.SystemColors.ControlText;
                    }
                }

                return;
            }

            long start = metadata.SelectionStart;
            long end = metadata.SelectionEnd;
            long now = hostView.CurrentTimestamp;
            int percent = 0;
            if (end > start)
                percent = (int)Math.Max(0, Math.Min(100, Math.Round(100.0 * (now - start) / (end - start))));

            double score = track.LastMatchScore;
            double threshold = track.TrackingParameters.SimilarityThreshold;

            // Only the correlation tracker reports a comparable match score.
            bool hasScore = score > 0 && track.TrackingParameters.TrackingAlgorithm == TrackingAlgorithm.Correlation;
            lblTrackingLive.Text = hasScore
                ? string.Format(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_LiveStatus, percent, score)
                : string.Format(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_LiveProgress, percent);
            if (score <= 0)
                lblTrackingLive.ForeColor = System.Drawing.SystemColors.GrayText;
            else if (score < threshold)
                lblTrackingLive.ForeColor = System.Drawing.Color.Firebrick;
            else if (score < Math.Min(1.0, threshold + 0.15))
                lblTrackingLive.ForeColor = System.Drawing.Color.DarkGoldenrod;
            else
                lblTrackingLive.ForeColor = System.Drawing.Color.ForestGreen;
        }

        public void UpdateContent()
        {
            UpdatePanelExtras();
            metadataManipulator.SetFixedTimestamp(hostView.CurrentTimestamp);
            Bitmap bitmap = BitmapHelper.CopyBasic(hostView.CurrentImage);
            viewportController.Bitmap = bitmap;
            viewportController.Timestamp = hostView.CurrentTimestamp;

            InitializeDisplayRectangle(bitmap.Size, hostView.CurrentTimestamp);
            viewportController.Refresh();
            UpdateLiveStatus();
        }
        #endregion

        private void ForgetMetadata()
        {
            if (metadata == null)
                return;

            metadataManipulator.DrawingModified -= MetadataManipulator_DrawingModified;
            metadata = null;
        }

        private void ForgetDrawing()
        {

            if (track != null)
            {
                track.TrackingStatusChanged -= Track_TrackingStatusChanged;
            }

            drawing = null;
            track = null;
            managerId = Guid.Empty;

            interactionTimer.Tick -= InteractionTimer_Tick;
        }

        private void SetupControls()
        {
            // Tracking algorithm combo-box.
            cbTrackingAlgorithm.Items.Clear();
            cbTrackingAlgorithm.ItemHeight = 21;
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                // Use the enum value as the item.
                cbTrackingAlgorithm.Items.Add(options[i]);
                if (track != null && options[i] == track.TrackingParameters.TrackingAlgorithm)
                    selectedIndex = cbTrackingAlgorithm.Items.Count - 1;
            }

            cbTrackingAlgorithm.SelectedIndex = selectedIndex;
            
            UpdateTrackingParameters();
            UpdateStartStopButton();

            this.Height = this.Height - this.ClientRectangle.Height + grpTracking.Bottom + 10;
        }

        private void FitSearchBox()
        {
            // Refit and recenter.
            Size imgSize = viewportController.Bitmap.Size;
            long timestamp = viewportController.Timestamp;
            InitializeDisplayRectangle(imgSize, timestamp);
        }

        private void InitializeDisplayRectangle(Size imgSize, long timestamp)
        {
            // Find an appropriate point to center the mini editor.
            PointF center = imgSize.Center();
            if (track != null)
            {
                center = track.GetPosition(timestamp);
                if (float.IsNaN(center.X) || float.IsNaN(center.Y) || center.IsEmpty)
                    center = imgSize.Center();
            }

            // Scale such that the search window fits in the viewport.
            float scale = 1.0f;
            if (track != null)
            {
                Size searchSize = track.TrackingParameters.SearchWindow;
                float scaleX = (float)pnlViewport.Width / searchSize.Width;
                float scaleY = (float)pnlViewport.Height / searchSize.Height;
                scale = Math.Min(scaleX, scaleY) * 0.9f;
            }

            PointF normalizedPosition = new PointF(center.X / imgSize.Width, center.Y / imgSize.Height);
            SizeF normalizedHostSize = new SizeF((float)pnlViewport.Width / imgSize.Width, (float)pnlViewport.Height / imgSize.Height);
            PointF normalizedHostCenter = new PointF(normalizedHostSize.Width / 2, normalizedHostSize.Height / 2);
            PointF normalizedDisplayLocation = new PointF(normalizedHostCenter.X - (normalizedPosition.X * scale), normalizedHostCenter.Y - (normalizedPosition.Y * scale));
            PointF topLeft = new PointF(normalizedDisplayLocation.X * imgSize.Width, normalizedDisplayLocation.Y * imgSize.Height);
            Size fullSize = new Size((int)(imgSize.Width * scale), (int)(imgSize.Height * scale));
            Rectangle display = new Rectangle((int)topLeft.X, (int)topLeft.Y, fullSize.Width, fullSize.Height);
            viewportController.InitializeDisplayRectangle(display, imgSize);
        }

        /// <summary>
        /// Update the tracking nuds with values from the drawing.
        /// </summary>
        /// <summary>
        /// The extra panel controls (status line and the bulk buttons) are opt-in,
        /// the default panel is the upstream one.
        /// </summary>
        private void UpdatePanelExtras()
        {
            bool extras = PreferencesManager.PlayerPreferences.TrackingPanelExtras;
            lblTrackingLive.Visible = extras;
            btnTrackAll.Visible = extras;
            btnApplyToAll.Visible = extras;
            if (!extras)
                lblTrackingLive.Text = string.Empty;
        }

        private void UpdateTrackingParameters()
        {
            manualUpdate = true;

            if (track != null)
            {
                TrackingParameters tp = track.TrackingParameters;
                nudSearchWindowWidth.Value = tp.SearchWindow.Width;
                nudSearchWindowHeight.Value = tp.SearchWindow.Height;
                nudObjWindowWidth.Value = tp.BlockWindow.Width;
                nudObjWindowHeight.Value = tp.BlockWindow.Height;
                nudMatchTreshold.Value = (decimal)tp.SimilarityThreshold;
                nudUpdateThreshold.Value = (decimal)tp.TemplateUpdateThreshold;

                bool enableThresholds = tp.TrackingAlgorithm == TrackingAlgorithm.Correlation;
                chkPredictiveSearch.Checked = tp.PredictiveSearch;
                chkPredictiveSearch.Enabled = tp.TrackingAlgorithm == TrackingAlgorithm.Correlation;
                lblMatchThreshold.Enabled = enableThresholds;
                lblUpdateThreshold.Enabled = enableThresholds;
                nudMatchTreshold.Enabled = enableThresholds;
                nudUpdateThreshold.Enabled = enableThresholds;
            }

            manualUpdate = false;
        }

        /// <summary>
        /// Update the start/stop button to reflect the current tracking status.
        /// </summary>
        private void UpdateStartStopButton()
        {
            if (track != null)
            {
                btnStartStop.Image = track.Status == TrackStatus.Interactive ? Properties.Drawings.tracking_start : Properties.Drawings.tracking_stop;
                btnStartStop.Text = track.Status == TrackStatus.Interactive ? 
                    Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_Start : 
                    Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_Stop;
            }
        }

        /// <summary>
        /// Force turn tracking ON if it's not the case already.
        /// This should be called for any change done via the panel (controls or mini viewport).
        /// This makes things coherent and improves perfs as the main 
        /// viewport will only draw a subset of the track in this case.
        /// </summary>
        private void EnsureTracking()
        {
            if (track == null)
                return;

            if (track.Status == TrackStatus.Interactive)
                track.StartTracking();
        }

        /// <summary>
        /// Cheap sanity check of the search and object windows before starting a track.
        /// </summary>
        private bool ValidateTrackingParameters()
        {
            // Opt-in: upstream starts the track whatever the window sizes are.
            if (!PreferencesManager.PlayerPreferences.TrackingValidateParameters)
                return true;

            TrackingParameters tp = track.TrackingParameters;
            bool objectTooSmall = tp.BlockWindow.Width < 6 || tp.BlockWindow.Height < 6;
            bool searchTooSmall = tp.SearchWindow.Width < tp.BlockWindow.Width + 8 ||
                                  tp.SearchWindow.Height < tp.BlockWindow.Height + 8;

            if (!objectTooSmall && !searchTooSmall)
                return true;

            MessageBox.Show(this,
                Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_InvalidParameters_Text,
                Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_InvalidParameters_Title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return false;
        }

        private void RaiseDrawingModified(DrawingAction action)
        {
            if (drawing != null)
            {
                DrawingModified?.Invoke(this, new DrawingEventArgs(drawing, managerId, action));
            }
        }

        #region Data events

        /// <summary>
        /// The tracking status (active vs inactive) was changed.
        /// This may originate from our own button or from the context menu on the drawing.
        /// Does not raise the DrawingModified event. Should it though?
        /// </summary>
        private void Track_TrackingStatusChanged(object sender, EventArgs e)
        {
            // Update local UI.
            UpdateStartStopButton();
        }

        private void Metadata_DrawingModified(object sender, DrawingEventArgs e)
        {
            log.DebugFormat("Non track drawing modified from the outside.");
            // A non-track drawing was modified from the outside.
            //if (drawing != null && drawing.Id == e.Drawing.Id)
            //    UpdateContent();
        }
        #endregion

        #region UI events to modify the data

        private void cbTrackingAlgorithm_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (manualUpdate || track == null)
                return;

            // Update the data.

            TrackingAlgorithm ta = (TrackingAlgorithm)cbTrackingAlgorithm.Items[cbTrackingAlgorithm.SelectedIndex];
            track.SetTrackingAlgorithm(ta);
            EnsureTracking();

            // Update local UI.
            viewportController.Refresh();
            UpdateTrackingParameters();

            // Update other controllers.
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        private void nudSearchWindow_ValueChanged(object sender, EventArgs e)
        {
            if (manualUpdate || track == null)
                return;

            int width = (int)nudSearchWindowWidth.Value;
            int height = (int)nudSearchWindowHeight.Value;
            
            // Update the data.
            track.TrackingParameters.SearchWindow = new Size(width, height);
            EnsureTracking();

            // Update local UI.
            FitSearchBox();
            viewportController.Refresh();

            // Update other controllers.
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        private void nudObjWindow_ValueChanged(object sender, EventArgs e)
        {
            if (manualUpdate || track == null)
                return;

            // For non-rectangular algorithms make sure the other dimension is in sync.
            if (track.TrackingParameters.TrackingAlgorithm != TrackingAlgorithm.Correlation)
            {
                manualUpdate = true;
                if (sender == nudObjWindowWidth)
                    nudObjWindowHeight.Value = nudObjWindowWidth.Value;
                else if (sender == nudObjWindowHeight)
                    nudObjWindowWidth.Value = nudObjWindowHeight.Value;
                manualUpdate = false;
            }

            int width = (int)nudObjWindowWidth.Value;
            int height = (int)nudObjWindowHeight.Value;
            
            // Update the data.
            track.TrackingParameters.BlockWindow = new Size(width, height);
            EnsureTracking();

            // Make sure the current point is updated with the new block size or radius.
            // The update call only works if the track is the currently selected object.
            metadata.SelectDrawing(track, metadata.TrackManager);
            metadata.UpdateTrackPoint(hostView.CurrentImage);

            // Update local UI.
            viewportController.Refresh();

            // Update other controllers.
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        private void nudThresholds_ValueChanged(object sender, EventArgs e)
        {
            if (manualUpdate || track == null)
                return;

            double matchThreshold = (double)nudMatchTreshold.Value;
            double updateThreshold = (double)nudUpdateThreshold.Value;
         
            // Update the data.
            track.TrackingParameters.SimilarityThreshold = matchThreshold;
            track.TrackingParameters.TemplateUpdateThreshold = updateThreshold;
            EnsureTracking();

            // Update local UI.
            viewportController.Refresh();

            // Update other controllers.
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        /// <summary>
        /// Arm every trackable drawing of the document at once so that the next
        /// playback pass tracks them all, instead of starting them one by one.
        /// </summary>
        /// <summary>
        /// Copy the settings of the current track to every other track of the document,
        /// so that a multi-point analysis does not have to be configured point by point.
        /// </summary>
        private void BtnApplyToAll_Click(object sender, EventArgs e)
        {
            if (metadata == null || track == null)
                return;

            foreach (DrawingTrack t in metadata.Tracks())
            {
                if (t == track)
                    continue;

                t.TrackingParameters.CopyFrom(track.TrackingParameters);
            }

            lblTrackingLive.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_ApplyToAllHint;
            lblTrackingLive.ForeColor = System.Drawing.SystemColors.ControlText;
        }

        private void BtnTrackAll_Click(object sender, EventArgs e)
        {
            if (metadata == null)
                return;

            int armed = 0;
            foreach (DrawingTrack t in metadata.Tracks())
            {
                if (t.Status == TrackStatus.Interactive)
                {
                    t.StartTracking();
                    armed++;
                }
            }

            if (armed == 0)
                return;

            RaiseDrawingModified(DrawingAction.TrackingStatusChanged);

            lblTrackingLive.Text = Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_TrackAllHint;
            lblTrackingLive.ForeColor = System.Drawing.SystemColors.ControlText;
        }

        /// <summary>
        /// Enable or disable the predictive search of the underlying track.
        /// </summary>
        private void ChkPredictiveSearch_CheckedChanged(object sender, EventArgs e)
        {
            if (manualUpdate || track == null)
                return;

            track.TrackingParameters.PredictiveSearch = chkPredictiveSearch.Checked;
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        #region Tracking candidates

        /// <summary>Upper bound for the number of candidates, from the preferences.</summary>
        private static int MaxCandidateCount
        {
            get { return PreferencesManager.PlayerPreferences.TrackingCandidateMax; }
        }

        /// <summary>Signature of the last rendered candidate list, to avoid rebuilding the tabs every frame.</summary>
        private string candidatesUiSignature = string.Empty;

        /// <summary>
        /// Refresh the candidate tabs and the button states. Tab 0 compares all the trajectories
        /// on the canvas (overlay), the other tabs show one candidate on its own.
        /// </summary>
        private void UpdateCandidatesUi()
        {
            if (tabsCandidates == null)
                return;

            int count = track == null ? 0 : track.CandidateSet.Count;
            string signature = count.ToString();
            for (int i = 0; i < count; i++)
                signature += "|" + track.CandidateSet[i].Name + ":" + track.CandidateSet[i].Rank;

            bool sameList = signature == candidatesUiSignature;
            if (track != null)
                track.CandidateSet.VisibleIndex = selectedCandidateIndex;

            if (!sameList)
            {
                candidatesUiSignature = signature;
                updatingCandidatesUi = true;
                try
                {
                    tabsCandidates.TabPages.Clear();
                    tabsCandidates.TabPages.Add(MakeCandidatePage(
                        Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateCompareTab, string.Empty));

                    for (int i = 0; i < count; i++)
                    {
                        TrackCandidate candidate = track.CandidateSet[i];
                        string label = string.Format(
                            Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateEntry,
                            candidate.Rank > 0 ? candidate.Rank : i + 1,
                            candidate.Name);
                        tabsCandidates.TabPages.Add(MakeCandidatePage(label, DescribeCandidate(candidate)));
                    }

                    int wanted = selectedCandidateIndex + 1;
                    tabsCandidates.SelectedIndex = wanted >= 0 && wanted < tabsCandidates.TabPages.Count ? wanted : 0;
                }
                finally
                {
                    updatingCandidatesUi = false;
                }
            }

            // The whole feature is opt-in: with the option off the group box is not shown at all.
            grpCandidates.Visible = PreferencesManager.PlayerPreferences.TrackingCandidatesEnabled;

            btnCandidateDuplicate.Enabled = count > 0;
            btnCandidateRemove.Enabled = count > 0;
            btnCandidateRun.Enabled = count > 0 && track != null && !track.CandidatesActive;
            btnCandidateAdopt.Enabled = count > 0 && track != null && !track.CandidatesActive;
        }

        private static TabPage MakeCandidatePage(string text, string info)
        {
            TabPage page = new TabPage(text);
            page.UseVisualStyleBackColor = true;

            if (!string.IsNullOrEmpty(info))
            {
                Label label = new Label();
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Text = info;
                page.Controls.Add(label);
            }

            return page;
        }

        /// <summary>One line summary of the proxy metrics, empty while the sweep is running.</summary>
        private static string DescribeCandidate(TrackCandidate candidate)
        {
            if (candidate == null || candidate.Metrics == null)
                return string.Empty;

            CandidateMetrics m = candidate.Metrics;
            double score = double.IsNaN(m.MeanScore) ? 0 : m.MeanScore;
            return string.Format(
                Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateMetrics,
                candidate.Rank, score, m.StuckRatio, m.MaxJump, m.Coverage, m.Smoothness);
        }

        /// <summary>First unused default name, so a sweep can be built by pressing add repeatedly.</summary>
        private static string NextCandidateName(TrackCandidateSet set)
        {
            for (int i = 1; i <= 99; i++)
            {
                string name = string.Format(
                    Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateDefaultName, i);
                bool used = false;
                for (int j = 0; j < set.Count; j++)
                {
                    if (set[j].Name == name)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used)
                    return name;
            }
            return "case";
        }

        /// <summary>Hint in the status line, only when the extended panel is enabled.</summary>
        private void ShowCandidateHint(string text)
        {
            if (lblTrackingLive == null)
                return;
            if (!PreferencesManager.PlayerPreferences.TrackingPanelExtras)
                return;

            lblTrackingLive.Visible = true;
            lblTrackingLive.Text = text;
            lblTrackingLive.ForeColor = System.Drawing.SystemColors.ControlText;
        }

        private void AfterCandidatesChanged()
        {
            candidatesUiSignature = string.Empty;
            UpdateCandidatesUi();
            if (hostView != null)
                hostView.InvalidateFromMenu();
        }

        /// <summary>
        /// Snapshot the parameters currently set in the panel: this is how a sweep is built, tweak
        /// the values then add another case.
        /// </summary>
        private void BtnCandidateAdd_Click(object sender, EventArgs e)
        {
            if (track == null || track.TrackingParameters == null)
            {
                ShowCandidateHint(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateNoTrack);
                return;
            }

            string name = NextCandidateName(track.CandidateSet);
            track.CandidateSet.Add(new TrackCandidate(name, track.TrackingParameters.Clone()));
            selectedCandidateIndex = track.CandidateSet.Count - 1;
            AfterCandidatesChanged();
        }

        private void BtnCandidateDuplicate_Click(object sender, EventArgs e)
        {
            if (track == null || !track.HasCandidates)
                return;

            int source = selectedCandidateIndex >= 0 ? selectedCandidateIndex : 0;
            string name = NextCandidateName(track.CandidateSet);
            if (track.CandidateSet.Duplicate(source, name) == null)
                return;

            selectedCandidateIndex = track.CandidateSet.Count - 1;
            AfterCandidatesChanged();
        }

        private void BtnCandidateRemove_Click(object sender, EventArgs e)
        {
            if (track == null || selectedCandidateIndex < 0 || selectedCandidateIndex >= track.CandidateSet.Count)
                return;

            track.CandidateSet.RemoveAt(selectedCandidateIndex);
            if (selectedCandidateIndex >= track.CandidateSet.Count)
                selectedCandidateIndex = track.CandidateSet.Count - 1;

            AfterCandidatesChanged();
        }

        private void BtnCandidateRun_Click(object sender, EventArgs e)
        {
            if (track == null || !track.HasCandidates)
            {
                ShowCandidateHint(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateNoTrack);
                return;
            }

            CandidateValidationResult validation = TrackCandidateValidator.Validate(track.CandidateSet.Candidates, MaxCandidateCount);
            if (!validation.IsValid)
            {
                ShowCandidateHint(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateInvalid);
                return;
            }

            if (track.StartCandidates())
            {
                ShowCandidateHint(string.Format(
                    Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateRunning,
                    track.CandidateSet.Count));
                AfterCandidatesChanged();
            }
        }

        private void BtnCandidateAdopt_Click(object sender, EventArgs e)
        {
            if (track == null || selectedCandidateIndex < 0 || selectedCandidateIndex >= track.CandidateSet.Count)
                return;

            string name = track.CandidateSet[selectedCandidateIndex].Name;
            if (!track.AdoptCandidate(selectedCandidateIndex))
            {
                ShowCandidateHint(Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateInvalid);
                return;
            }

            selectedCandidateIndex = -1;
            candidatesUiSignature = string.Empty;
            ShowCandidateHint(string.Format(
                Kinovea.ScreenManager.Languages.ScreenManagerLang.tracking_CandidateAdopted, name));

            UpdateCandidatesUi();
            RaiseDrawingModified(DrawingAction.TrackingParametersChanged);
        }

        private void TabsCandidates_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingCandidatesUi || track == null)
                return;

            int index = tabsCandidates.SelectedIndex;
            selectedCandidateIndex = index <= 0 ? -1 : index - 1;
            track.CandidateSet.VisibleIndex = selectedCandidateIndex;

            if (hostView != null)
                hostView.InvalidateFromMenu();
        }

        #endregion
        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (track != null)
            {
                // Refuse to start with parameters that cannot work, and say why.
                if (track.Status != TrackStatus.Edit && !ValidateTrackingParameters())
                    return;

                // Update the data.
                track.ToggleTracking();
                
                // Update local UI.
                // Already handled since we listen to tracking status change events.

                // Update other controllers.
                RaiseDrawingModified(DrawingAction.TrackingStatusChanged);
            }
        }

        private void btnTrimTrack_Click(object sender, EventArgs e)
        {
            if (track != null)
            {
                // Update the data.
                track.Trim(viewportController.Timestamp);
                
                // Do not force open tracking. One scenario of trimming is 
                // when the tracking is closed and we go back to the last 
                // good point and trim the rest.

                // Update local UI.
                // Nothing to do.
                // TODO: have a control with the number of tracked frames.

                // Update other controllers.
                RaiseDrawingModified(DrawingAction.StateChanged);
            }
        }
        #endregion

        #region Misc UI events
        private void InteractionTimer_Tick(object sender, EventArgs e)
        {
            viewportController.Refresh();
        }

        private void pnlViewport_Resize(object sender, EventArgs e)
        {
            if (drawing == null)
                return;

            FitSearchBox();
        }

        private void pnlViewport_DoubleClick(object sender, EventArgs e)
        {
            if (drawing == null)
                return;

            FitSearchBox();
        }

        /// <summary>
        /// Draw one item of the tracking algorithm combo-box.
        /// </summary>
        private void cbTrackingAlgorithm_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= options.Count)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int top = e.Bounds.Height / 2;

            Brush backgroundBrush = Brushes.White;
            if ((e.State & DrawItemState.Focus) != 0)
                backgroundBrush = Brushes.LightSteelBlue;

            e.Graphics.FillRectangle(backgroundBrush, e.Bounds.Left, e.Bounds.Top, e.Bounds.Width, e.Bounds.Height);

            Point topLeft = new Point(e.Bounds.Left + 2, e.Bounds.Top + 2);
            Size size = new Size(16, 16);
            Rectangle rect = new Rectangle(topLeft, size);
            PointF textTopLeft = new PointF(e.Bounds.Left + 20, e.Bounds.Top + 4);

            // Do not rely on the index itself as the options may be out of order or 
            // not all enabled.
            TrackingAlgorithm algo = (TrackingAlgorithm)(int)cbTrackingAlgorithm.Items[e.Index];
            switch (algo)
            {
                case TrackingAlgorithm.Correlation:
                    {
                        e.Graphics.DrawImage(Properties.Resources.bring_forward_16, rect);
                        e.Graphics.DrawString("Correlation", e.Font, Brushes.Black, textTopLeft);
                        break;
                    }
                case TrackingAlgorithm.Blob:
                    {
                        e.Graphics.DrawImage(Properties.Resources.ellipse_16, rect);
                        e.Graphics.DrawString("Blob", e.Font, Brushes.Black, textTopLeft);
                        break;
                    }
                case TrackingAlgorithm.Circle:
                    {
                        e.Graphics.DrawImage(Properties.Resources.circle3_16, rect);
                        e.Graphics.DrawString("Circle", e.Font, Brushes.Black, textTopLeft);

                        break;
                    }
            }
        }

        /// <summary>
        /// Custom outline color.
        /// </summary>
        private void Control_Paint(object sender, PaintEventArgs e)
        {
        }
        private void miniViewport_MouseEnter(object sender, EventArgs e)
        {
            //interactionTimer.Start();
        }

        private void miniViewport_MouseLeave(object sender, EventArgs e)
        {
            //interactionTimer.Stop();
        }

        /// <summary>
        /// The track is being moved from the mini viewport.
        /// </summary>
        private void miniViewport_Moving(object sender, EventArgs e)
        {
            EnsureTracking();

            // Signal to the main viewport for invalidation.
            RaiseDrawingModified(DrawingAction.Moving);
        }

        private void MetadataManipulator_DrawingModified(object sender, DrawingEventArgs e)
        {
            if (track == null || track.Id != e.Drawing.Id)
                return;

            if (e.DrawingAction == DrawingAction.Resizing)
            {
                // Keep the nuds up to date but don't trigger invalidation of the main viewport
                // as this is raised for every mouse move while dragging the corners.
                UpdateTrackingParameters();
            }
            else if (e.DrawingAction == DrawingAction.Resized || e.DrawingAction == DrawingAction.Moved)
            {
                FitSearchBox();
                EnsureTracking();

                // Update other controllers.
                RaiseDrawingModified(e.DrawingAction);
            }
        }
        #endregion

        private void tbHue_Scroll(object sender, EventArgs e)
        {

        }
    }
}
