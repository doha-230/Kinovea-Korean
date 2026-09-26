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

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// One measurement of the audio level at a point in time.
    /// Levels are expressed in dBFS (0 = full scale, negative = quieter).
    /// </summary>
    public class AudioLoudnessSample
    {
        /// <summary>Time relative to the start of the video, in seconds.</summary>
        public double Time { get; set; }

        /// <summary>RMS level in dBFS. Negative infinity for digital silence.</summary>
        public double Rms { get; set; }

        /// <summary>Peak level in dBFS. Negative infinity for digital silence.</summary>
        public double Peak { get; set; }

        public AudioLoudnessSample(double time, double rms, double peak)
        {
            Time = time;
            Rms = rms;
            Peak = peak;
        }
    }
}
