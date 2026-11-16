namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors EdpAvailabilityResponse in
    /// src/modules/external_data/adapters/inbound/api/schemas/bought_item_edp_schema.py: whether the EPLAN Data
    /// Portal is switched on in Glados, so the task pane can disable its lookup instead of
    /// offering one that would only fail.</summary>
    public class EdpAvailabilityResponse
    {
        public bool Enabled { get; set; }
    }
}
