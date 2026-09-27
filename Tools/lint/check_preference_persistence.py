#!/usr/bin/env python3
"""Fail when a fork option would not survive a restart.

The fork's promise is: every improvement is opt-in, and once enabled it is remembered. That
only holds while each option has a default, is written to the preference file, is read back,
and is saved when it changes.

Upstream has a number of fields that are deliberately not persisted (session flags, nested
objects serialized by their own writer under another name, constants), so instead of
classifying every field by name this check works from an explicit manifest:

  * every field of the manifest is checked strictly (default, write, read, and for the global
    preferences a setter that calls Save());
  * any field that is neither in the manifest nor in the upstream snapshot is reported as an
    error, so a new option has to be added to the manifest once its persistence is verified.

Usage:
  python3 Tools/lint/check_preference_persistence.py [--verbose]

Exit code: 0 = clean, 1 = at least one option cannot be persisted.
"""
from __future__ import annotations

import argparse
import re
import sys

PREFS = 'Kinovea.Services/Preferences/PlayerPreferences.cs'
PARAMS = 'Kinovea.Services/Types/TrackingParameters.cs'

# Fork-added options: field -> (file, needs a saving setter)
MANIFEST = {
    # global options (setter must call Save)
    'enableFrameSkipping': (PREFS, True),
    'frameSkipMode': (PREFS, True),
    'frameSkipCount': (PREFS, True),
    'frameSkipMotionSensitivity': (PREFS, True),
    'csvEncoding': (PREFS, True),
    'videoHardwareEncoder': (PREFS, True),
    'audioLoudnessWindowMs': (PREFS, True),
    'stopTrackingOnFailure': (PREFS, True),
    'trackingRetryOnFailure': (PREFS, True),
    'trackingValidateParameters': (PREFS, True),
    'trackingPanelExtras': (PREFS, True),
    'trackingFollowObject': (PREFS, True),
    'trackingCandidatesEnabled': (PREFS, True),
    'trackingCandidateMax': (PREFS, True),
    # per drawing parameters (saved inside the KVA of the drawing)
    'predictiveSearch': (PARAMS, False),
    'rejectOutliers': (PARAMS, False),
    'scaleAdaptive': (PARAMS, False),
}

# Fields that were already in upstream before the fork's work (see the commit that created
# this check). They are out of scope: upstream decides how they are persisted.
UPSTREAM = {
    'accelerationUnit', 'angleUnit', 'angularAccelerationUnit', 'angularVelocityUnit',
    'aspectRatio', 'cadenceUnit', 'cameraMotionParameters', 'csvDecimalSeparator', 'customLengthAbbreviation',
    'customLengthUnit', 'decimalPlaces', 'defaultFading', 'deinterlaceByDefault',
    'detectImageSequences', 'drawOnPlay', 'enableCustomToolsDebugMode', 'enableFiltering',
    'enableHardwareDecoding', 'enableHardwareScaling', 'enableHighSpeedDerivativesSmoothing',
    'enablePixelFiltering', 'enablePreviewScaling', 'exportImagesInDocuments', 'exportProfile',
    'exportSpace', 'imageFormat', 'interactiveFrameTracker', 'keyframePresetsParameters',
    'kinogramParameters', 'lensCalibrationParameters', 'loopPlayback', 'maxRecentColors',
    'pandocPath', 'playbackKVA', 'preloadKeyframes', 'recentColors', 'showCacheInTimeline',
    'sideBySideHorizontal', 'speedLabelFactor', 'speedLabelFramerate', 'speedLabelInterval',
    'speedUnit', 'syncByMotion', 'syncLockSpeed', 'timecodeFormat', 'timelineJumpLargeSize',
    'timelineJumpLargeUnit', 'timelineJumpSmallSize', 'timelineJumpSmallUnit',
    'trackingParameters', 'videoFormat', 'workingZoneMemory',
    'blockWindow', 'dilate', 'erode', 'hsvRange', 'maxWindowSize', 'resetOnMove', 'searchWindow',
    'similarityThreshold', 'templateUpdateThreshold', 'trackingAlgorithm', 'useMask',
}

FIELD_RE = re.compile(
    r'^ {8}private\s+(?:readonly\s+|static\s+)?[\w<>\[\]\.]+\s+(\w+)\s*(?:=\s*([^;]+))?;', re.M)


def pascal(name):
    return name[0].upper() + name[1:] if name else name


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--verbose', action='store_true')
    args = ap.parse_args()

    sources = {PREFS: open(PREFS, encoding='utf-8-sig').read(),
               PARAMS: open(PARAMS, encoding='utf-8-sig').read()}
    problems = []

    for path, text in sources.items():
        for m in FIELD_RE.finditer(text):
            field = m.group(1)
            if field in MANIFEST or field in UPSTREAM:
                continue
            problems.append('{0}: unclassified preference field {1} - add it to the manifest '
                            'after checking that it is persisted'.format(path, field))

    for field, (path, need_setter) in sorted(MANIFEST.items()):
        text = sources[path]
        prop = pascal(field)

        m = re.search(r'^ {8}private\s+(?:readonly\s+|static\s+)?[\w<>\[\]\.]+\s+' + field + r'\s*=\s*([^;]+);',
                      text, re.M)
        if not m or not m.group(1).strip():
            problems.append('{0}.{1}: no default value'.format(path, field))

        if need_setter:
            setter = re.search(r'public\s+[\w<>\[\]\.]+\s+' + prop + r'\s*\{', text, re.I)
            if not setter:
                problems.append('{0}.{1}: no property'.format(path, field))
            else:
                body = text[setter.end():setter.end() + 400]
                s = re.search(r'set\s*\{(.*?)\}', body, re.S)
                if not s or 'Save()' not in s.group(1):
                    problems.append('{0}.{1}: setter does not call Save()'.format(path, field))

        written = re.search('(WriteElementString|WriteStartElement)\\("' + prop + '\\"', text, re.I)
        if not written:
            problems.append('{0}.{1}: not written to the preference file'.format(path, field))

        read = re.search('(case \\"' + prop + '\\"|ReadStartElement\\("' + prop + '\\")', text, re.I)
        if not read:
            problems.append('{0}.{1}: not read back from the preference file'.format(path, field))

    for p in problems:
        print('ERROR option not persisted: {0}'.format(p))
    print('preference persistence: {0} options checked, {1} problems'.format(len(MANIFEST), len(problems)))
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
