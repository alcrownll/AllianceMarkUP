using ASI.Basecode.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Resolves the wwwroot folder without ASP.NET hosting types.
    /// Assumes the process runs from the content root of the WebApp.
    /// Falls back to probing parent folders for a "wwwroot" directory.
    /// </summary>
    public class LocalWebRootPathAccessor : IWebRootPathAccessor
    {
        public string WebRootPath { get; }

        public LocalWebRootPathAccessor()
        {
            // Start from current directory (usually the content root).
            var baseDir = Directory.GetCurrentDirectory();

            // If ./wwwroot exists, use it. Otherwise probe up to 3 parents.
            var path = TryResolveWebRoot(baseDir)
                       ?? TryResolveWebRoot(Directory.GetParent(baseDir)?.FullName)
                       ?? TryResolveWebRoot(Directory.GetParent(baseDir)?.Parent?.FullName)
                       ?? TryResolveWebRoot(Directory.GetParent(baseDir)?.Parent?.Parent?.FullName);

            // If still not found, default to ./wwwroot under the current dir.
            path ??= Path.Combine(baseDir, "wwwroot");

            // Ensure the folder exists so file saves don't fail.
            Directory.CreateDirectory(path);
            WebRootPath = path;
        }

        private static string? TryResolveWebRoot(string? dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            var candidate = Path.Combine(dir, "wwwroot");
            return Directory.Exists(candidate) ? candidate : null;
        }
    }
}
