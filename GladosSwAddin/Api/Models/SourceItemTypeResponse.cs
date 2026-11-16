using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors SourceItemTypeResponse in src/modules/catalogue/adapters/inbound/api/schemas/source_item_type_schema.py.</summary>
    public class SourceItemTypeResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public bool Archived { get; set; }

        /// <summary>Place in the hand-kept order Glados' pickers offer these in (see
        /// LookupOrder); 0 from a server that predates it.</summary>
        public int SortOrder { get; set; }

        public string Name { get; set; }

        public string LabelColor { get; set; }

        public string LabelFontColor { get; set; }

        public string Description { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }
    }
}
