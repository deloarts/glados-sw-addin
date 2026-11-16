namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors PatVersionResponse in
    /// src/modules/platform/adapters/inbound/api/schemas/version_schema.py: the backend's full
    /// version and the major.minor of the pat contract it serves, which PatContract compares
    /// with the contract this add-in is built for.</summary>
    public class PatVersionResponse
    {
        public string Version { get; set; }

        public string PatContract { get; set; }
    }
}
