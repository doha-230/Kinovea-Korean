#region License
/*
Copyright © Joan Charmant 2012.
jcharmant@gmail.com 
 
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
using System.Drawing;
using System.Globalization;
using System.Xml;

namespace Kinovea.Services
{
    /// <summary>
    /// Preferences for the player, including annotations.
    /// </summary>
    public class PlayerPreferences : IPreferenceSerializer
    {
        #region Properties
        public string Name
        {
            get { return "Player"; }
        }
        public int DecimalPlaces
        {
            get { BeforeRead(); return decimalPlaces; }
            set { decimalPlaces = value; Save(); }
        }
        public TimecodeFormat TimecodeFormat
        {
            get { BeforeRead(); return timecodeFormat; }
            set { timecodeFormat = value; Save(); }
        }
        public SpeedUnit SpeedUnit
        {
            get { BeforeRead(); return speedUnit; }
            set { speedUnit = value; Save(); }
        }
        public AccelerationUnit AccelerationUnit
        {
            get { BeforeRead(); return accelerationUnit; }
            set { accelerationUnit = value; Save(); }
        }
        public AngleUnit AngleUnit
        {
            get { BeforeRead(); return angleUnit; }
            set { angleUnit = value; Save(); }
        }
        public AngularVelocityUnit AngularVelocityUnit
        {
            get { BeforeRead(); return angularVelocityUnit; }
            set { angularVelocityUnit = value; Save(); }
        }
        public AngularAccelerationUnit AngularAccelerationUnit
        {
            get { BeforeRead(); return angularAccelerationUnit; }
            set { angularAccelerationUnit = value; Save(); }
        }
        public string CustomLengthUnit
        {
            get { BeforeRead(); return customLengthUnit; }
            set { customLengthUnit = value; Save(); }
        }
        public string CustomLengthAbbreviation
        {
            get { BeforeRead(); return customLengthAbbreviation; }
            set { customLengthAbbreviation = value; Save(); }
        }

        public CadenceUnit CadenceUnit
        {
            get { BeforeRead(); return cadenceUnit; }
            set { cadenceUnit = value; Save(); }
        }
        public ImageAspectRatio AspectRatio
        {
            get { BeforeRead(); return aspectRatio; }
            set { aspectRatio = value; Save(); }
        }
        public CSVDecimalSeparator CSVDecimalSeparator
        {
            get { BeforeRead(); return csvDecimalSeparator; }
            set { csvDecimalSeparator = value; Save(); }
        }

        /// <summary>
        /// Encoding used when writing exported text files.
        /// </summary>
        public CSVEncoding CSVEncoding
        {
            get { BeforeRead(); return csvEncoding; }
            set { csvEncoding = value; Save(); }
        }

        /// <summary>
        /// Window used when exporting the audio loudness, in milliseconds.
        /// Clamped to [10, 5000].
        /// </summary>
        public int AudioLoudnessWindowMs
        {
            get { BeforeRead(); return audioLoudnessWindowMs; }
            set { audioLoudnessWindowMs = Math.Max(10, Math.Min(value, 5000)); Save(); }
        }

        /// <summary>
        /// Multiplier applied to the amount of motion that the motion-adaptive
        /// frame skipping policy considers "fast". Higher values skip more
        /// aggressively: only larger movements stop the skipping.
        /// Clamped to [MinMotionSensitivity, MaxMotionSensitivity].
        /// </summary>
        public double FrameSkipMotionSensitivity
        {
            get { BeforeRead(); return frameSkipMotionSensitivity; }
            set { frameSkipMotionSensitivity = Math.Max(MinMotionSensitivity, Math.Min(value, MaxMotionSensitivity)); Save(); }
        }
        /// <summary>
        /// Whether the player stops the playback when a track fails to match.
        /// When false the tracker keeps trying with its reference template and can
        /// recover after a brief occlusion. Default: true (historical behaviour).
        /// </summary>

        /// <summary>
        /// Retry a failed match with a larger search window. Changes the tracking results, so it is off by default.
        /// </summary>
        /// <summary>
        /// Enable the tracking candidate sweep (compare several parameter sets at once).
        /// Off by default: an untouched installation behaves like upstream.
        /// </summary>
        public bool TrackingCandidatesEnabled
        {
            get { BeforeRead(); return trackingCandidatesEnabled; }
            set { trackingCandidatesEnabled = value; Save(); }
        }

        /// <summary>Maximum number of candidates that can be tracked at once.</summary>
        public int TrackingCandidateMax
        {
            get { BeforeRead(); return trackingCandidateMax; }
            set { trackingCandidateMax = Math.Max(1, Math.Min(value, MaxTrackingCandidates)); Save(); }
        }

        /// <summary>Hard upper bound for the candidate count, follows the core count.</summary>
        public static int MaxTrackingCandidates
        {
            get { return Math.Max(1, Math.Min(Environment.ProcessorCount, 8)); }
        }

        public bool TrackingRetryOnFailure
        {
            get { BeforeRead(); return trackingRetryOnFailure; }
            set { trackingRetryOnFailure = value; Save(); }
        }

        /// <summary>
        /// Warn about, and refuse to start, a track whose search and object windows look wrong. Off by default.
        /// </summary>
        public bool TrackingValidateParameters
        {
            get { BeforeRead(); return trackingValidateParameters; }
            set { trackingValidateParameters = value; Save(); }
        }

        /// <summary>
        /// Show the extended tracking panel: live status line, "track all" and "apply to all". Off by default.
        /// </summary>
        /// <summary>
        /// Keep the tracked object inside the visible area by panning the viewport
        /// while a track is running. Off by default: upstream never moves the viewport.
        /// </summary>
        public bool TrackingFollowObject
        {
            get { BeforeRead(); return trackingFollowObject; }
            set { trackingFollowObject = value; Save(); }
        }

        public bool TrackingPanelExtras
        {
            get { BeforeRead(); return trackingPanelExtras; }
            set { trackingPanelExtras = value; Save(); }
        }

        public bool StopTrackingOnFailure
        {
            get { BeforeRead(); return stopTrackingOnFailure; }
            set { stopTrackingOnFailure = value; Save(); }
        }

        public ExportSpace ExportSpace
        {
            get { BeforeRead(); return exportSpace; }
            set { exportSpace = value; Save(); }
        }

        /// <summary>
        /// Hardware encoder used for H.264/H.265 video export. Software encoding
        /// (None) is the default and always works; hardware encoders are much
        /// faster but require a compatible GPU and the matching ffmpeg encoder.
        /// </summary>
        public HardwareEncoder VideoHardwareEncoder
        {
            get { BeforeRead(); return videoHardwareEncoder; }
            set { videoHardwareEncoder = value; Save(); }
        }
        public bool ExportImagesInDocuments
        {
            get { BeforeRead(); return exportImagesInDocuments; }
            set { exportImagesInDocuments = value; Save(); }
        }
        public bool DeinterlaceByDefault
        {
            get { BeforeRead(); return deinterlaceByDefault; }
            set { deinterlaceByDefault = value; Save(); }
        }
        public bool InteractiveFrameTracker
        {
            get { BeforeRead(); return interactiveFrameTracker; }
            set { interactiveFrameTracker = value; Save(); }
        }
        public int WorkingZoneMemory
        {
            get { BeforeRead(); return workingZoneMemory; }
            set { workingZoneMemory = value; Save(); }
        }
        public bool ShowCacheInTimeline
        {
            get { BeforeRead(); return showCacheInTimeline; }
            set { showCacheInTimeline = value; Save(); }
        }
        public bool SyncLockSpeed
        {
            get { BeforeRead(); return syncLockSpeed;}
            set { syncLockSpeed = value; Save(); }
        }

        public bool SyncByMotion
        {
            get { BeforeRead(); return syncByMotion; }
            set { syncByMotion = value; Save(); }
        }
        
        public InfosFading DefaultFading
        {
            get { BeforeRead(); return defaultFading; }
            set { defaultFading = value; Save(); }
        }

        public bool EnablePixelFiltering
        {
            get { BeforeRead(); return enablePixelFiltering; }
            set { enablePixelFiltering = value; Save(); }
        }

        public bool DrawOnPlay
        {
            get { BeforeRead(); return drawOnPlay; }
            set { drawOnPlay = value; Save(); }
        }
        public List<Color> RecentColors
        {
            get { BeforeRead(); return recentColors; }
        }
        public KinoveaImageFormat ImageFormat
        {
            get { BeforeRead(); return imageFormat; }
            set { imageFormat = value; Save(); }
        }
        public VideoContainer VideoFormat
        {
            get { BeforeRead(); return videoFormat; }
            set { videoFormat = value; Save(); }
        }
        public TrackingParameters TrackingParameters
        {
            get { BeforeRead(); return trackingParameters; }
            set { trackingParameters = value; Save(); }
        }
        public bool EnableFiltering
        {
            get { BeforeRead(); return enableFiltering; }
            set { enableFiltering = value; Save(); }
        }
        public bool EnableHighSpeedDerivativesSmoothing
        {
            get { BeforeRead(); return enableHighSpeedDerivativesSmoothing; }
            set { enableHighSpeedDerivativesSmoothing = value; Save(); }
        }
        public bool EnableCustomToolsDebugMode
        {
            get { BeforeRead(); return enableCustomToolsDebugMode; }
            set { enableCustomToolsDebugMode = value; Save(); }
        }
        public bool DetectImageSequences
        {
            get { BeforeRead(); return detectImageSequences; }
            set { detectImageSequences = value; Save(); }
        }
        public int PreloadKeyframes
        {
            get { BeforeRead(); return preloadKeyframes; }
            set { preloadKeyframes = value; Save(); }
        }
        public string PlaybackKVA
        {
            get { BeforeRead(); return playbackKVA; }
            set { playbackKVA = value; Save(); }
        }
        public ExportProfile ExportProfile
        {
            get { BeforeRead(); return exportProfile; }
            set { exportProfile = value; Save(); }
        }

        public KinogramParameters Kinogram
        {
            get { BeforeRead(); return kinogramParameters.Clone(); }
            set { kinogramParameters = value; Save(); }
        }

        public LensCalibrationParameters LensCalibration
        {
            get { BeforeRead(); return lensCalibrationParameters.Clone(); }
            set { lensCalibrationParameters = value; Save(); }
        }

        public CameraMotionParameters CameraMotionParameters
        {
            get { BeforeRead(); return cameraMotionParameters.Clone(); }
            set { cameraMotionParameters = value; Save(); }
        }

        public KeyframePresetsParameters KeyframePresets
        {
            get { BeforeRead(); return keyframePresetsParameters.Clone(); }
            set { keyframePresetsParameters = value; Save(); }
        }
        public string PandocPath
        {
            get { BeforeRead(); return pandocPath; }
            set { pandocPath = value; Save(); }
        }

        public bool SideBySideHorizontal
        {
            get { BeforeRead(); return sideBySideHorizontal; }
            set { sideBySideHorizontal = value; Save(); }
        }

        public bool EnableFrameSkipping
        {
            get { BeforeRead(); return enableFrameSkipping; }
            set { enableFrameSkipping = value; Save(); }
        }

        /// <summary>
        /// How the player picks the number of skipped frames, when frame skipping
        /// is enabled (see <see cref="EnableFrameSkipping"/>).
        /// </summary>
        public FrameSkipMode FrameSkipMode
        {
            get { BeforeRead(); return frameSkipMode; }
            set { frameSkipMode = value; Save(); }
        }

        /// <summary>
        /// Number of frames to skip between two rendered frames, used in
        /// <see cref="FrameSkipMode.Manual"/> mode. Clamped to [0, MaxFrameSkip].
        /// </summary>
        public int FrameSkipCount
        {
            get { BeforeRead(); return frameSkipCount; }
            set { frameSkipCount = Math.Max(0, Math.Min(value, MaxFrameSkip)); Save(); }
        }

        /// <summary>Hard upper bound for the manual frame skip count.</summary>
        public const int MaxFrameSkip = 10;

        /// <summary>Lower bound of the motion-adaptive sensitivity.</summary>
        public const double MinMotionSensitivity = 0.25;

        /// <summary>Upper bound of the motion-adaptive sensitivity.</summary>
        public const double MaxMotionSensitivity = 4.0;

        public float TimelineJumpSmallSize
        {
            get { BeforeRead(); return timelineJumpSmallSize; }
            set { timelineJumpSmallSize = value; Save(); }
        }

        public TimelineJumpUnit TimelineJumpSmallUnit
        {
            get { BeforeRead(); return timelineJumpSmallUnit; }
            set { timelineJumpSmallUnit = value; Save(); }
        }

        public float TimelineJumpLargeSize
        {
            get { BeforeRead(); return timelineJumpLargeSize; }
            set { timelineJumpLargeSize = value; Save(); }
        }

        public TimelineJumpUnit TimelineJumpLargeUnit
        {
            get { BeforeRead(); return timelineJumpLargeUnit; }
            set { timelineJumpLargeUnit = value; Save(); }
        }

        public bool SpeedLabelFactor
        {
            get { BeforeRead(); return speedLabelFactor; }
            set { speedLabelFactor = value; Save(); }
        }
        
        public bool SpeedLabelFramerate
        {
            get { BeforeRead(); return speedLabelFramerate; }
            set { speedLabelFramerate = value; Save(); }
        }

        public bool SpeedLabelInterval
        {
            get { BeforeRead(); return speedLabelInterval; }
            set { speedLabelInterval = value; Save(); }
        }

        public bool LoopPlayback
        {
            get { BeforeRead(); return loopPlayback; }
            set { loopPlayback = value; Save(); }
        }

        public bool EnableHardwareDecoding
        {
            get { BeforeRead(); return enableHardwareDecoding; }
            set { enableHardwareDecoding = value; Save(); }
        }

        public bool EnablePreviewScaling
        {
            get { BeforeRead(); return enablePreviewScaling; }
            set { enablePreviewScaling = value; Save(); }
        }

        public bool EnableHardwareScaling
        {
            get { BeforeRead(); return enableHardwareScaling; }
            set { enableHardwareScaling = value; Save(); }
        }

        #endregion

        #region Members
        private int decimalPlaces = 2;
        private TimecodeFormat timecodeFormat = TimecodeFormat.ClassicTime;
        private SpeedUnit speedUnit = SpeedUnit.MetersPerSecond;
        private AccelerationUnit accelerationUnit = AccelerationUnit.MetersPerSecondSquared;
        private AngleUnit angleUnit = AngleUnit.Degree;
        private AngularVelocityUnit angularVelocityUnit = AngularVelocityUnit.DegreesPerSecond;
        private AngularAccelerationUnit angularAccelerationUnit = AngularAccelerationUnit.DegreesPerSecondSquared;
        private string customLengthUnit = "";
        private string customLengthAbbreviation = "";
        private CadenceUnit cadenceUnit = CadenceUnit.Hertz;
        private CSVDecimalSeparator csvDecimalSeparator = CSVDecimalSeparator.System;
        private CSVEncoding csvEncoding = CSVEncoding.Utf8NoBom;
        private int audioLoudnessWindowMs = 50;
        private double frameSkipMotionSensitivity = 1.0;
        private ExportSpace exportSpace = ExportSpace.WorldSpace;
        private bool stopTrackingOnFailure = true;
        private bool trackingRetryOnFailure = false;
        private bool trackingValidateParameters = false;
        private bool trackingPanelExtras = false;
        private bool trackingFollowObject = false;
        private bool trackingCandidatesEnabled = false;
        private int trackingCandidateMax = 5;
        private HardwareEncoder videoHardwareEncoder = HardwareEncoder.None;
        private bool exportImagesInDocuments = true;
        private string pandocPath = "";
        private ImageAspectRatio aspectRatio = ImageAspectRatio.Auto;
        private bool deinterlaceByDefault;
        private bool interactiveFrameTracker = true;
        private int workingZoneMemory = 768;
        private InfosFading defaultFading = new InfosFading();
        private bool enablePixelFiltering = true;
        private bool drawOnPlay = true;
        private List<Color> recentColors = new List<Color>();
        private int maxRecentColors = 12;
        private bool syncLockSpeed = true;
        private bool syncByMotion = false;
        private KinoveaImageFormat imageFormat = KinoveaImageFormat.JPG;
        private VideoContainer videoFormat = VideoContainer.MKV;
        private TrackingParameters trackingParameters = new TrackingParameters();
        private bool enableFiltering = true;
        private bool enableHighSpeedDerivativesSmoothing = true;
        private bool enableCustomToolsDebugMode = false;
        private bool detectImageSequences = true;
        private int preloadKeyframes = 20;
        private string playbackKVA;
        private ExportProfile exportProfile = new ExportProfile();
        private KinogramParameters kinogramParameters = new KinogramParameters();
        private LensCalibrationParameters lensCalibrationParameters = new LensCalibrationParameters();
        private CameraMotionParameters cameraMotionParameters = new CameraMotionParameters();
        private KeyframePresetsParameters keyframePresetsParameters = new KeyframePresetsParameters();
        private bool showCacheInTimeline = false;
        private bool sideBySideHorizontal = true;
        private bool enableFrameSkipping = true;
        private FrameSkipMode frameSkipMode = FrameSkipMode.Auto;
        private int frameSkipCount = 0;
        private float timelineJumpSmallSize = 0.5f;
        private TimelineJumpUnit timelineJumpSmallUnit = TimelineJumpUnit.Second;
        private float timelineJumpLargeSize = 10f;
        private TimelineJumpUnit timelineJumpLargeUnit = TimelineJumpUnit.SnapPercent;
        private bool speedLabelFactor = true;
        private bool speedLabelFramerate = false;
        private bool speedLabelInterval = false;
        private bool loopPlayback = true;
        private bool enableHardwareDecoding = true;
        private bool enablePreviewScaling = true;
        private bool enableHardwareScaling = true;
        #endregion

        private void Save()
        {
            PreferencesManager.Save();
        }

        private void BeforeRead()
        {
            PreferencesManager.BeforeRead();
        }


        public void AddRecentColor(Color _color)
        {
            PreferencesHelper.UpdateRecents(_color, recentColors, maxRecentColors);
            Save();
        }

        #region Serialization

        public void WriteXML(XmlWriter writer)
        {
            writer.WriteElementString("DecimalPlaces", decimalPlaces.ToString());
            writer.WriteElementString("TimecodeFormat", timecodeFormat.ToString());
            writer.WriteElementString("SpeedUnit", speedUnit.ToString());
            writer.WriteElementString("AccelerationUnit", accelerationUnit.ToString());
            writer.WriteElementString("AngleUnit", angleUnit.ToString());
            writer.WriteElementString("AngularVelocityUnit", angularVelocityUnit.ToString());
            writer.WriteElementString("AngularAccelerationUnit", angularAccelerationUnit.ToString());
            writer.WriteElementString("CustomLengthUnit", customLengthUnit);
            writer.WriteElementString("CustomLengthAbbreviation", customLengthAbbreviation);
            writer.WriteElementString("CadenceUnit", cadenceUnit.ToString());
            writer.WriteElementString("CSVDecimalSeparator", csvDecimalSeparator.ToString());
            writer.WriteElementString("CSVEncoding", csvEncoding.ToString());
            writer.WriteElementString("AudioLoudnessWindowMs", audioLoudnessWindowMs.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("FrameSkipMotionSensitivity", frameSkipMotionSensitivity.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("StopTrackingOnFailure", XmlHelper.WriteBoolean(stopTrackingOnFailure));
            writer.WriteElementString("TrackingRetryOnFailure", XmlHelper.WriteBoolean(trackingRetryOnFailure));
            writer.WriteElementString("TrackingValidateParameters", XmlHelper.WriteBoolean(trackingValidateParameters));
            writer.WriteElementString("TrackingPanelExtras", XmlHelper.WriteBoolean(trackingPanelExtras));
            writer.WriteElementString("TrackingFollowObject", XmlHelper.WriteBoolean(trackingFollowObject));
            writer.WriteElementString("TrackingCandidatesEnabled", XmlHelper.WriteBoolean(trackingCandidatesEnabled));
            writer.WriteElementString("TrackingCandidateMax", trackingCandidateMax.ToString());
            writer.WriteElementString("ExportSpace", exportSpace.ToString());
            writer.WriteElementString("VideoHardwareEncoder", videoHardwareEncoder.ToString());
            writer.WriteElementString("ExportImagesInDocuments", XmlHelper.WriteBoolean(exportImagesInDocuments));
            writer.WriteElementString("AspectRatio", aspectRatio.ToString());
            writer.WriteElementString("DeinterlaceByDefault", XmlHelper.WriteBoolean(deinterlaceByDefault));
            writer.WriteElementString("InteractiveFrameTracker", XmlHelper.WriteBoolean(interactiveFrameTracker));
            writer.WriteElementString("WorkingZoneMemory", workingZoneMemory.ToString());
            writer.WriteElementString("ShowCacheInTimeline", XmlHelper.WriteBoolean(showCacheInTimeline));
            writer.WriteElementString("SyncLockSpeed", XmlHelper.WriteBoolean(syncLockSpeed));
            writer.WriteElementString("SyncByMotion", XmlHelper.WriteBoolean(syncByMotion));
            writer.WriteElementString("ImageFormat", imageFormat.ToString());
            writer.WriteElementString("VideoFormat", videoFormat.ToString());
            
            writer.WriteStartElement("InfoFading");
            defaultFading.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteElementString("EnablePixelFiltering", XmlHelper.WriteBoolean(enablePixelFiltering));

            writer.WriteElementString("DrawOnPlay", XmlHelper.WriteBoolean(drawOnPlay));
            
            if(recentColors.Count > 0)
            {
                writer.WriteStartElement("RecentColors");
                
                for(int i = 0; i < maxRecentColors; i++)
                {
                    if(i >= recentColors.Count)
                        break;
                    
                    writer.WriteElementString("RecentColor", string.Format("{0};{1};{2}", recentColors[i].R.ToString(), recentColors[i].G.ToString(), recentColors[i].B.ToString()));
                }
                writer.WriteEndElement();
            }
            
            writer.WriteElementString("MaxRecentColors", maxRecentColors.ToString());

            writer.WriteStartElement("TrackingParameters");
            trackingParameters.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteElementString("EnableFiltering", XmlHelper.WriteBoolean(enableFiltering));
            writer.WriteElementString("EnableCustomToolsDebugMode", XmlHelper.WriteBoolean(enableCustomToolsDebugMode));
            writer.WriteElementString("DetectImageSequences", XmlHelper.WriteBoolean(detectImageSequences));
            writer.WriteElementString("PreloadKeyframes", preloadKeyframes.ToString());
            writer.WriteElementString("PlaybackKVA", playbackKVA);

            writer.WriteStartElement("ExportProfile");
            exportProfile.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement("Kinogram");
            kinogramParameters.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement("LensCalibration");
            lensCalibrationParameters.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement("CameraMotion");
            cameraMotionParameters.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement("KeyframePresets");
            keyframePresetsParameters.WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteElementString("PandocPath", pandocPath);
            writer.WriteElementString("SideBySideHorizontal", XmlHelper.WriteBoolean(sideBySideHorizontal));
            writer.WriteElementString("EnableFrameSkipping", XmlHelper.WriteBoolean(enableFrameSkipping));
            writer.WriteElementString("FrameSkipMode", frameSkipMode.ToString());
            writer.WriteElementString("FrameSkipCount", frameSkipCount.ToString(CultureInfo.InvariantCulture));

            writer.WriteElementString("TimelineJumpSmallSize", XmlHelper.WriteFloat(timelineJumpSmallSize));
            writer.WriteElementString("TimelineJumpSmallUnit", timelineJumpSmallUnit.ToString());
            writer.WriteElementString("TimelineJumpLargeSize", XmlHelper.WriteFloat(timelineJumpLargeSize));
            writer.WriteElementString("TimelineJumpLargeUnit", timelineJumpLargeUnit.ToString());
            writer.WriteElementString("SpeedLabelFactor", XmlHelper.WriteBoolean(speedLabelFactor));
            writer.WriteElementString("SpeedLabelFramerate", XmlHelper.WriteBoolean(speedLabelFramerate));
            writer.WriteElementString("SpeedLabelInterval", XmlHelper.WriteBoolean(speedLabelInterval));
            writer.WriteElementString("LoopPlayback", XmlHelper.WriteBoolean(loopPlayback));
            writer.WriteElementString("EnableHardwareDecoding", XmlHelper.WriteBoolean(enableHardwareDecoding));
            writer.WriteElementString("EnablePreviewScaling", XmlHelper.WriteBoolean(enablePreviewScaling));
            writer.WriteElementString("EnableHardwareScaling", XmlHelper.WriteBoolean(enableHardwareScaling));
        }
        
        public void ReadXML(XmlReader reader)
        {
            reader.ReadStartElement();

            while(reader.NodeType == XmlNodeType.Element)
            {
                switch(reader.Name)
                {
                    case "DecimalPlaces":
                        decimalPlaces = reader.ReadElementContentAsInt();
                        break;
                    case "TimecodeFormat":
                        timecodeFormat = (TimecodeFormat) Enum.Parse(typeof(TimecodeFormat), reader.ReadElementContentAsString());
                        break;
                    case "SpeedUnit":
                        speedUnit = (SpeedUnit) Enum.Parse(typeof(SpeedUnit), reader.ReadElementContentAsString());
                        break;
                    case "AccelerationUnit":
                        accelerationUnit = (AccelerationUnit)Enum.Parse(typeof(AccelerationUnit), reader.ReadElementContentAsString());
                        break;
                    case "AngleUnit":
                        angleUnit = (AngleUnit)Enum.Parse(typeof(AngleUnit), reader.ReadElementContentAsString());
                        break;
                    case "AngularVelocityUnit":
                        angularVelocityUnit = (AngularVelocityUnit)Enum.Parse(typeof(AngularVelocityUnit), reader.ReadElementContentAsString());
                        break;
                    case "AngularAccelerationUnit":
                        angularAccelerationUnit = (AngularAccelerationUnit)Enum.Parse(typeof(AngularAccelerationUnit), reader.ReadElementContentAsString());
                        break;
                    case "CustomLengthUnit":
                        customLengthUnit = reader.ReadElementContentAsString();
                        break;
                    case "CustomLengthAbbreviation":
                        customLengthAbbreviation = reader.ReadElementContentAsString();
                        break;
                    case "CadenceUnit":
                        cadenceUnit = XmlHelper.ParseEnum(reader.ReadElementContentAsString(), CadenceUnit.Hertz);
                        break;
                    case "CSVDecimalSeparator":
                        csvDecimalSeparator = (CSVDecimalSeparator)Enum.Parse(typeof(CSVDecimalSeparator), reader.ReadElementContentAsString());
                        break;
                    case "TrackingRetryOnFailure":
                        trackingRetryOnFailure = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "TrackingValidateParameters":
                        trackingValidateParameters = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "TrackingCandidatesEnabled":
                        trackingCandidatesEnabled = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "TrackingCandidateMax":
                        trackingCandidateMax = int.Parse(reader.ReadElementContentAsString(), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "TrackingFollowObject":
                        trackingFollowObject = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "TrackingPanelExtras":
                        trackingPanelExtras = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "StopTrackingOnFailure":
                        stopTrackingOnFailure = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "ExportSpace":
                        exportSpace = (ExportSpace)Enum.Parse(typeof(ExportSpace), reader.ReadElementContentAsString());
                        break;
                    case "CSVEncoding":
                        csvEncoding = XmlHelper.ParseEnum<CSVEncoding>(reader.ReadElementContentAsString(), CSVEncoding.Utf8NoBom);
                        break;
                    case "AudioLoudnessWindowMs":
                        audioLoudnessWindowMs = Math.Max(10, Math.Min(reader.ReadElementContentAsInt(), 5000));
                        break;
                    case "FrameSkipMotionSensitivity":
                        frameSkipMotionSensitivity = Math.Max(MinMotionSensitivity, Math.Min(reader.ReadElementContentAsDouble(), MaxMotionSensitivity));
                        break;
                    case "VideoHardwareEncoder":
                        videoHardwareEncoder = XmlHelper.ParseEnum<HardwareEncoder>(reader.ReadElementContentAsString(), HardwareEncoder.None);
                        break;
                    case "ExportImagesInDocuments":
                        exportImagesInDocuments = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "AspectRatio":
                        aspectRatio = (ImageAspectRatio) Enum.Parse(typeof(ImageAspectRatio), reader.ReadElementContentAsString());
                        break;
                    case "DeinterlaceByDefault":
                        deinterlaceByDefault = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "InteractiveFrameTracker":
                        interactiveFrameTracker = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "WorkingZoneMemory":
                        workingZoneMemory = reader.ReadElementContentAsInt();
                        break;
                    case "ShowCacheInTimeline":
                        showCacheInTimeline = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "SyncLockSpeed":
                        syncLockSpeed = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "SyncByMotion":
                        syncByMotion = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "ImageFormat":
                        imageFormat = (KinoveaImageFormat)Enum.Parse(typeof(KinoveaImageFormat), reader.ReadElementContentAsString());
                        break;
                    case "VideoFormat":
                        videoFormat = (VideoContainer)Enum.Parse(typeof(VideoContainer), reader.ReadElementContentAsString());
                        break;
                    case "InfoFading":
                        defaultFading.ReadXml(reader);
                        break;
                    case "EnablePixelFiltering":
                        enablePixelFiltering = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "DrawOnPlay":
                        drawOnPlay = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;                        
                    case "RecentColors":
                        ParseRecentColors(reader);
                        break;
                    case "MaxRecentColors":
                        maxRecentColors = reader.ReadElementContentAsInt();
                        break;
                    case "TrackingParameters":
                        trackingParameters.ReadXml(reader);
                        break;
                    case "EnableFiltering":
                        enableFiltering = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "EnableCustomToolsDebugMode":
                        enableCustomToolsDebugMode = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "DetectImageSequences":
                        detectImageSequences = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "PreloadKeyframes":
                        preloadKeyframes = reader.ReadElementContentAsInt();
                        break;
                    case "PlaybackKVA":
                        playbackKVA = reader.ReadElementContentAsString();
                        break;
                    case "ExportProfile":
                        exportProfile.ReadXml(reader);
                        break;
                    case "Kinogram":
                        kinogramParameters.ReadXml(reader);
                        break;
                    case "LensCalibration":
                        lensCalibrationParameters.ReadXml(reader);
                        break;
                    case "CameraMotion":
                        cameraMotionParameters.ReadXml(reader);
                        break;
                    case "KeyframePresets":
                        keyframePresetsParameters.ReadXml(reader);
                        break;
                    case "PandocPath":
                        pandocPath = reader.ReadElementContentAsString();
                        break;
                    case "SideBySideHorizontal":
                        sideBySideHorizontal = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "EnableFrameSkipping":
                        enableFrameSkipping = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "FrameSkipMode":
                        frameSkipMode = XmlHelper.ParseEnum<FrameSkipMode>(reader.ReadElementContentAsString(), FrameSkipMode.Auto);
                        break;
                    case "FrameSkipCount":
                        frameSkipCount = Math.Max(0, Math.Min(reader.ReadElementContentAsInt(), MaxFrameSkip));
                        break;
                    case "TimelineJumpSmallSize":
                        timelineJumpSmallSize = XmlHelper.ParseFloat(reader.ReadElementContentAsString());
                        break;
                    case "TimelineJumpSmallUnit":
                        timelineJumpSmallUnit = (TimelineJumpUnit)Enum.Parse(typeof(TimelineJumpUnit), reader.ReadElementContentAsString());
                        break;
                    case "TimelineJumpLargeSize":
                        timelineJumpLargeSize = XmlHelper.ParseFloat(reader.ReadElementContentAsString());
                        break;
                    case "TimelineJumpLargeUnit":
                        timelineJumpLargeUnit = (TimelineJumpUnit)Enum.Parse(typeof(TimelineJumpUnit), reader.ReadElementContentAsString());
                        break;
                    case "SpeedLabelFactor":
                        speedLabelFactor = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "SpeedLabelFramerate":
                        speedLabelFramerate = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "SpeedLabelInterval":
                        speedLabelInterval = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "LoopPlayback":
                        loopPlayback = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "EnableHardwareDecoding":
                        enableHardwareDecoding = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "EnablePreviewScaling":
                        enablePreviewScaling = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    case "EnableHardwareScaling":
                        enableHardwareScaling = XmlHelper.ParseBoolean(reader.ReadElementContentAsString());
                        break;
                    default:
                        reader.ReadOuterXml();
                        break;
                }
            }
            
            reader.ReadEndElement();
        }
        
        private void ParseRecentColors(XmlReader reader)
        {
            recentColors.Clear();
            bool empty = reader.IsEmptyElement;
            
            reader.ReadStartElement();
            
            if(empty)
                return;
            
            while(reader.NodeType == XmlNodeType.Element)
            {
                if(reader.Name == "RecentColor")
                    recentColors.Add(XmlHelper.ParseColor(reader.ReadElementContentAsString(), Color.Black));
                else
                    reader.ReadOuterXml();
            }
            
            reader.ReadEndElement();
        }
        #endregion
    }
}
