using System;
using System.Globalization;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Resources;

namespace GladosSwAddin.Api
{
    /// <summary>How the backend's answer to GET pat/version relates to the contract this add-in
    /// is built for.</summary>
    internal enum PatContractStatus
    {
        /// <summary>The answer was missing or not a major.minor pair, so nothing can be said
        /// about compatibility.</summary>
        Unknown = 0,
        Supported,
        Unsupported,
    }

    /// <summary>
    /// The pat contract this add-in is built against and the check the task pane runs on every
    /// successful connection. A backend serving another contract minor may have changed or
    /// removed endpoints the add-in calls, so a mismatch earns a warning that the add-in needs an
    /// update. It deliberately never blocks the connection: most calls usually still work, and
    /// an engineer in the middle of a task is better served by a working pane with a warning
    /// than by a locked one. An answer the check cannot read is Unknown and earns no warning, so
    /// a malformed response never claims a mismatch that may not exist.
    /// </summary>
    internal static class PatContract
    {
        /// <summary>The major.minor of the backend's pat contract this build was written and
        /// tested against. Moves only together with the code that follows a new contract.</summary>
        internal const string Supported = "0.1";

        /// <summary>Compares a pat_contract value from the backend with Supported, as numbers,
        /// so "0.1" and "00.1" agree. Anything that is not exactly two non-negative integers
        /// separated by a dot is Unknown.</summary>
        internal static PatContractStatus Check(string serverContract)
        {
            int serverMajor, serverMinor, supportedMajor, supportedMinor;
            if (!TryParseMajorMinor(serverContract, out serverMajor, out serverMinor))
                return PatContractStatus.Unknown;
            if (!TryParseMajorMinor(Supported, out supportedMajor, out supportedMinor))
                throw new InvalidOperationException("PatContract.Supported is not a major.minor pair.");

            return serverMajor == supportedMajor && serverMinor == supportedMinor
                ? PatContractStatus.Supported
                : PatContractStatus.Unsupported;
        }

        /// <summary>The warning the task pane shows for a version answer, or null when there is
        /// nothing to warn about: the contract is supported, or the answer could not be read.
        /// Names the server version, the add-in version and the supported contract, so a user
        /// can quote all three when asking for the update.</summary>
        internal static string WarningFor(PatVersionResponse response, string addInVersion)
        {
            if (response == null || Check(response.PatContract) != PatContractStatus.Unsupported)
                return null;

            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.BackendVersionWarningFormat,
                string.IsNullOrWhiteSpace(response.Version) ? "?" : response.Version.Trim(),
                response.PatContract.Trim(),
                string.IsNullOrWhiteSpace(addInVersion) ? "?" : addInVersion,
                Supported);
        }

        private static bool TryParseMajorMinor(string value, out int major, out int minor)
        {
            major = 0;
            minor = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Trim().Split('.');
            return parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor);
        }
    }
}
