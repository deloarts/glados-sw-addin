namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors BoughtItemPdfFileResponse in src/modules/catalogue/adapters/inbound/api/schemas/bought_item_schema.py.
    /// A slim PDF-file projection embedded in BoughtItemResponse so the catalogue list can
    /// render one icon per PDF; full metadata lives in BoughtItemFileResponse (not modeled
    /// here, no add-in call site fetches it).</summary>
    public class BoughtItemPdfFileResponse
    {
        public int Id { get; set; }

        public string FileName { get; set; }

        public string MimeType { get; set; }
    }
}
