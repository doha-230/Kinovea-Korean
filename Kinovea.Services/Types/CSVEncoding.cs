using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinovea.Services
{
    /// <summary>
    /// Encoding used when writing exported text files (CSV, TXT, JSON, Markdown).
    /// </summary>
    public enum CSVEncoding
    {
        /// <summary>
        /// UTF-8 without BOM. This is the default and matches the historical
        /// behaviour (spreadsheet apps then rely on the code page to decode).
        /// </summary>
        Utf8NoBom,

        /// <summary>
        /// UTF-8 with BOM. Excel and most Windows tools then detect UTF-8
        /// reliably, which matters for non-ASCII content (Korean labels, ...).
        /// </summary>
        Utf8Bom,

        /// <summary>
        /// The system default code page (e.g. CP949 on Korean Windows).
        /// </summary>
        System,
    }
}
