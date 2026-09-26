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
namespace Kinovea.Services
{
    /// <summary>
    /// How the player picks the number of skipped frames.
    /// Whether frame skipping happens at all is controlled by the master switch
    /// <see cref="PlayerPreferences.EnableFrameSkipping"/>.
    /// </summary>
    public enum FrameSkipMode
    {
        /// <summary>
        /// The player decides how many frames to skip, based on the current lag,
        /// so playback can keep up with real time.
        /// </summary>
        Auto,

        /// <summary>
        /// The user chooses the number of skipped frames
        /// (<see cref="PlayerPreferences.FrameSkipCount"/>, 0 to MaxFrameSkip).
        /// </summary>
        Manual,

        /// <summary>
        /// The player skips frames according to the amount of movement in the
        /// scene: when nothing moves it skips up to MaxFrameSkip frames, when
        /// movement is fast it skips none. Opt-in (the default is Auto).
        /// </summary>
        MotionAdaptive,
    }
}
