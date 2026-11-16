using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors PublicUserResponse in src/modules/platform/adapters/inbound/api/schemas/user_schema.py.</summary>
    public class PublicUserResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string Username { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public bool HasAvatar { get; set; }
    }
}
