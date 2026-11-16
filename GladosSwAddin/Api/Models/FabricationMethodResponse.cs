using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors FabricationMethodResponse in src/modules/catalogue/adapters/inbound/api/schemas/fabrication_method_schema.py.</summary>
    public class FabricationMethodResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public string Name { get; set; }

        public string LabelColor { get; set; }

        public string LabelFontColor { get; set; }

        public string Description { get; set; }

        public bool RequiresMachine { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }

        public bool Archived { get; set; }

        /// <summary>Place in the hand-kept order Glados' pickers offer these in (see
        /// LookupOrder); 0 from a server that predates it.</summary>
        public int SortOrder { get; set; }

        public bool RequiredExternal { get; set; }

        public int? DefaultSupplierId { get; set; }

        /// <summary>The one method preselected for a made assembly's routing (see
        /// AssemblyRoutingDefaults). At most one method carries it, and never an archived one —
        /// the backend clears it on archive.</summary>
        public bool DefaultForAssemblies { get; set; }
    }
}
