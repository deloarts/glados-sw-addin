using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors ArticleGroupResponse in src/modules/catalogue/adapters/inbound/api/schemas/article_group_schema.py.</summary>
    public class ArticleGroupResponse
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
