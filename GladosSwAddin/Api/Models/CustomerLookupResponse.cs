namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors CustomerLookupResponse in src/modules/catalogue/adapters/inbound/api/schemas/lookups_schema.py:
    /// the slim customer GET /pat/lookups carries for the Made tab's customer picker. Id, name and
    /// archived flag only, because the pat surface never hands out a customer's address,
    /// contact or VAT data.</summary>
    public class CustomerLookupResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool Archived { get; set; }
    }
}
