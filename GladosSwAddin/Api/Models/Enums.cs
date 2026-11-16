using System.Runtime.Serialization;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors src/modules/catalogue/domain/value_objects/data_source.py.</summary>
    public enum DataSource
    {
        [EnumMember(Value = "WEB")]
        Web,

        [EnumMember(Value = "CAD")]
        Cad,
    }

    /// <summary>Mirrors BoughtItemCatalogueStatus in src/modules/catalogue/domain/value_objects/bought_item_status.py.</summary>
    public enum BoughtItemCatalogueStatus
    {
        [EnumMember(Value = "active")]
        Active,

        [EnumMember(Value = "inactive")]
        Inactive,

        [EnumMember(Value = "replaced")]
        Replaced,
    }

    /// <summary>Mirrors StepRole in src/modules/catalogue/domain/value_objects/step_role.py. A step is either
    /// tied to a fabrication method (a machine) or an inspection method (a QA checkpoint with
    /// no machine) — never both.</summary>
    public enum StepRole
    {
        [EnumMember(Value = "fabrication")]
        Fabrication,

        [EnumMember(Value = "inspection")]
        Inspection,
    }

    /// <summary>Mirrors MadeItemCatalogueStatus in src/modules/catalogue/domain/value_objects/made_item_status.py.</summary>
    public enum MadeItemCatalogueStatus
    {
        [EnumMember(Value = "active")]
        Active,

        [EnumMember(Value = "inactive")]
        Inactive,

        [EnumMember(Value = "replaced")]
        Replaced,
    }

    /// <summary>Mirrors SourceItemCatalogueStatus in src/modules/catalogue/domain/value_objects/source_item_status.py.</summary>
    public enum SourceItemCatalogueStatus
    {
        [EnumMember(Value = "active")]
        Active,

        [EnumMember(Value = "inactive")]
        Inactive,

        [EnumMember(Value = "replaced")]
        Replaced,
    }

    /// <summary>Mirrors src/modules/catalogue/domain/value_objects/supplier_level.py.</summary>
    public enum SupplierLevel
    {
        [EnumMember(Value = "A")]
        A,

        [EnumMember(Value = "B")]
        B,

        [EnumMember(Value = "C")]
        C,
    }

    /// <summary>Mirrors src/modules/platform/domain/value_objects/language.py.</summary>
    public enum Language
    {
        [EnumMember(Value = "deAT")]
        DeAT,

        [EnumMember(Value = "enGB")]
        EnGB,
    }

    /// <summary>Mirrors src/modules/workspace/domain/value_objects/project_status.py.</summary>
    public enum ProjectStatus
    {
        [EnumMember(Value = "active")]
        Active,

        [EnumMember(Value = "on_hold")]
        OnHold,

        [EnumMember(Value = "completed")]
        Completed,

        [EnumMember(Value = "archived")]
        Archived,
    }

    /// <summary>Mirrors src/modules/platform/domain/value_objects/date_format.py.</summary>
    public enum DateFormat
    {
        [EnumMember(Value = "iso")]
        Iso,

        [EnumMember(Value = "dmy_dot")]
        DmyDot,

        [EnumMember(Value = "dmy_slash")]
        DmySlash,

        [EnumMember(Value = "mdy_slash")]
        MdySlash,

        [EnumMember(Value = "d_mon_y")]
        DMonY,
    }

    /// <summary>Mirrors ThemePreset in src/modules/platform/domain/value_objects/theme.py.</summary>
    public enum ThemePreset
    {
        [EnumMember(Value = "Aura")]
        Aura,

        [EnumMember(Value = "Lara")]
        Lara,

        [EnumMember(Value = "Material")]
        Material,

        [EnumMember(Value = "Nora")]
        Nora,
    }

    /// <summary>Mirrors ThemeSurface in src/modules/platform/domain/value_objects/theme.py.</summary>
    public enum ThemeSurface
    {
        [EnumMember(Value = "glados")]
        Glados,

        [EnumMember(Value = "slate")]
        Slate,

        [EnumMember(Value = "gray")]
        Gray,

        [EnumMember(Value = "zinc")]
        Zinc,

        [EnumMember(Value = "neutral")]
        Neutral,

        [EnumMember(Value = "stone")]
        Stone,
    }

    /// <summary>Mirrors the "would" field of POST /api/v1/pat/pdm/validate's response — what
    /// ingesting this document would do to the catalogue if it arrived from PDM right now.
    /// Unknown is both the tolerant fallback for a value this build doesn't recognize yet (see
    /// GladosApiClient.TolerantStringEnumConverter) and itself a real wire value the backend
    /// returns when it can't tell.</summary>
    public enum PdmValidationOutcome
    {
        [EnumMember(Value = "unknown")]
        Unknown,

        [EnumMember(Value = "created")]
        Created,

        [EnumMember(Value = "revised")]
        Revised,

        [EnumMember(Value = "updated")]
        Updated,

        [EnumMember(Value = "claimed")]
        Claimed,

        [EnumMember(Value = "status")]
        Status,
    }

    /// <summary>Mirrors the "file_identity" field of POST /api/v1/pat/pdm/validate's response —
    /// whether the request's file_path (when the caller sent one) resolved to a PDM vault
    /// identity. NotApplicable covers both an older backend that doesn't send the field at all
    /// (the property stays null, see PdmValidationResponse.FileIdentity) and a request that sent
    /// no file_path in the first place.</summary>
    public enum PdmFileIdentity
    {
        [EnumMember(Value = "resolved")]
        Resolved,

        [EnumMember(Value = "unresolved")]
        Unresolved,

        [EnumMember(Value = "not_applicable")]
        NotApplicable,
    }

    /// <summary>Mirrors ThemePrimary in src/modules/platform/domain/value_objects/theme.py.</summary>
    public enum ThemePrimary
    {
        [EnumMember(Value = "glados")]
        Glados,

        [EnumMember(Value = "noir")]
        Noir,

        [EnumMember(Value = "soul")]
        Soul,

        [EnumMember(Value = "emerald")]
        Emerald,

        [EnumMember(Value = "green")]
        Green,

        [EnumMember(Value = "lime")]
        Lime,

        [EnumMember(Value = "red")]
        Red,

        [EnumMember(Value = "orange")]
        Orange,

        [EnumMember(Value = "amber")]
        Amber,

        [EnumMember(Value = "yellow")]
        Yellow,

        [EnumMember(Value = "teal")]
        Teal,

        [EnumMember(Value = "cyan")]
        Cyan,

        [EnumMember(Value = "sky")]
        Sky,

        [EnumMember(Value = "blue")]
        Blue,

        [EnumMember(Value = "indigo")]
        Indigo,

        [EnumMember(Value = "violet")]
        Violet,

        [EnumMember(Value = "purple")]
        Purple,

        [EnumMember(Value = "fuchsia")]
        Fuchsia,

        [EnumMember(Value = "pink")]
        Pink,

        [EnumMember(Value = "rose")]
        Rose,
    }
}
