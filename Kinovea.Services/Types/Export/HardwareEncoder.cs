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
    /// Hardware accelerated H.264/H.265 encoders that ffmpeg may use for video
    /// export. Software encoding (None) is always available; the hardware ones
    /// depend on the graphics hardware and on the bundled ffmpeg build, and may
    /// fail on machines that do not provide them.
    /// </summary>
    public enum HardwareEncoder
    {
        /// <summary>Software encoding with libx264 / libx265 (default).</summary>
        None,

        /// <summary>NVIDIA NVENC (h264_nvenc / hevc_nvenc).</summary>
        Nvenc,

        /// <summary>Intel Quick Sync (h264_qsv / hevc_qsv).</summary>
        Qsv,

        /// <summary>AMD Advanced Media Framework (h264_amf / hevc_amf).</summary>
        Amf,
    }
}
