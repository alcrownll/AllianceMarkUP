using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Exposes the absolute path to the web root (wwwroot).
    /// Services can use this without depending on ASP.NET types.
    /// </summary>
    public interface IWebRootPathAccessor
    {
        string WebRootPath { get; }
    }
}
