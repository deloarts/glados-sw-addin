namespace GladosSwAddin.Api
{
    /// <summary>
    /// Mirrors src/modules/platform/const.py on the backend. Paths are relative (no leading slash)
    /// so they combine correctly with HttpClient.BaseAddress.
    /// </summary>
    internal static class ApiRoutes
    {
        public const string Pat = "api/v1/pat/";
        public const string Auth = "api/v1/auth/";

        /// <summary>Relative to Pat. Kept as its own constant — unlike every other PAT
        /// endpoint, which the individual *ApiService classes build inline from Pat — because
        /// this exact route is a fixed contract point between the add-in and the backend's PDM
        /// pre-release validation endpoint. See README.md's "Validation before release".</summary>
        public const string PdmValidate = "pdm/validate";
    }
}
