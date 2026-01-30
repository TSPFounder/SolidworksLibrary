using System;
using System.Collections.Generic;
using CAD;
using Mathematics;
using SE_Library;
using System.IO;

namespace CAD
{
    /// <summary>
    /// Describes a CAD content library (e.g., parts/templates) that may live
    /// on a local/network path and/or be reachable via a URL.
    /// </summary>
    public sealed class CAD_Library
    {
        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_Library() { }

        public CAD_Library(string name, string localPath = null, Uri url = null, string description = null)
        {
            Name = name;
            Description = description;
            LocalPath = localPath;
            Url = url;
        }

        /// <summary>Create with a URL string (parsed to <see cref="Uri"/>).</summary>
        public static CAD_Library FromUrl(string name, string url, string description = null)
            => new CAD_Library(name, url: ParseUri(url), description: description);

        // -----------------------------
        // Identity / metadata
        // -----------------------------
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Local or network location (may be relative or absolute).</summary>
        public string LocalPath { get; set; }

        /// <summary>Remote location (HTTP(S), share URL, etc.).</summary>
        public Uri Url { get; set; }

        // -----------------------------
        // Derived state
        // -----------------------------
        public bool HasLocalPath => !string.IsNullOrWhiteSpace(LocalPath);
        public bool HasRemoteUrl => Url != null;

        /// <summary>True if at least a name and one location (local or remote) is provided.</summary>
        public bool IsConfigured
            => !string.IsNullOrWhiteSpace(Name) && (HasLocalPath || HasRemoteUrl);

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>
        /// Returns an absolute local path if <see cref="LocalPath"/> is set; otherwise null.
        /// Does not check existence.
        /// </summary>
        public string GetAbsoluteLocalPath()
        {
            if (!HasLocalPath) return null;
            try { return Path.GetFullPath(LocalPath); }
            catch { return null; }
        }

        /// <summary>Attempt to set <see cref="Url"/> from a string. Returns false on parse error.</summary>
        public bool TrySetUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) { Url = null; return true; }
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Url = uri;
                return true;
            }
            return false;
        }

        /// <summary>Validate minimal configuration. Returns true if OK, with reason when invalid.</summary>
        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                reason = "Library must have a Name.";
                return false;
            }

            if (!HasLocalPath && !HasRemoteUrl)
            {
                reason = "Library must specify a LocalPath and/or a Url.";
                return false;
            }

            reason = null;
            return true;
        }

        public override string ToString()
            => $"{Name ?? "(unnamed)"} | Local: {LocalPath ?? "-"} | Url: {Url?.ToString() ?? "-"}";

        // -----------------------------
        // Internal
        // -----------------------------
        private static Uri ParseUri(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("Invalid URL.", nameof(url));
            return uri;
        }
    }
}
