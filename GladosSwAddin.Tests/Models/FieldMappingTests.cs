using System;
using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Models
{
    /// <summary>
    /// Pins EnumWireName's [EnumMember] resolution, the wire-name/wire-value helpers built on
    /// top of it, and the compiled-in field-mapping defaults (docs/FIELD_OWNERSHIP.md section 3)
    /// this add-in falls back to before a server mapping has synced.
    /// </summary>
    [TestClass]
    public class FieldMappingTests
    {
        [TestMethod]
        public void EnumWireName_Resolve_UsesEnumMemberValueWhenPresent()
        {
            Assert.AreEqual("pdm", EnumWireName.Resolve(FieldOwner.Pdm));
        }

        [TestMethod]
        public void EnumWireName_Resolve_FallsBackToBareNameWhenUndecorated()
        {
            Assert.AreEqual("Unknown", EnumWireName.Resolve(FieldOwner.Unknown));
        }

        [TestMethod]
        [DataRow(ItemField.PartNumber, "part_number")]
        [DataRow(ItemField.Revision, "revision")]
        [DataRow(ItemField.RevisionDate, "revision_date")]
        [DataRow(ItemField.RevisionNote, "revision_note")]
        [DataRow(ItemField.ArticleName, "article_name")]
        [DataRow(ItemField.Material, "material")]
        [DataRow(ItemField.Mass, "mass")]
        [DataRow(ItemField.BaseSize, "base_size")]
        [DataRow(ItemField.Note, "note")]
        [DataRow(ItemField.Weblink, "weblink")]
        [DataRow(ItemField.ArticleGroupIds, "article_group_ids")]
        [DataRow(ItemField.BoughtItemId, "bought_item_id")]
        [DataRow(ItemField.SourceItemId, "source_item_id")]
        [DataRow(ItemField.Processes, "processes")]
        [DataRow(ItemField.Status, "status")]
        [DataRow(ItemField.SuccessorItemId, "successor_item_id")]
        [DataRow(ItemField.ApproverId, "approver_id")]
        [DataRow(ItemField.Files, "files")]
        [DataRow(ItemField.DataSource, "data_source")]
        [DataRow(ItemField.OrderCode1, "order_code_1")]
        [DataRow(ItemField.OrderCode2, "order_code_2")]
        [DataRow(ItemField.Gtin, "gtin")]
        [DataRow(ItemField.Ean, "ean")]
        [DataRow(ItemField.TariffNumber, "tariff_number")]
        [DataRow(ItemField.ManufacturerId, "manufacturer_id")]
        [DataRow(ItemField.Kind, "kind")]
        [DataRow(ItemField.EdpPartId, "edp_part_id")]
        [DataRow(ItemField.ArticleGroupNames, "article_group_names")]
        [DataRow(ItemField.ManufacturerName, "manufacturer_name")]
        [DataRow(ItemField.ProcessNames, "process_names")]
        [DataRow(ItemField.SourceItemName, "source_item_name")]
        [DataRow(ItemField.FeatureTreeRepresentation, "feature_tree_representation")]
        [DataRow(ItemField.CustomerId, "customer_id")]
        [DataRow(ItemField.CustomerPartNumber, "customer_part_number")]
        [DataRow(ItemField.CustomerRevision, "customer_revision")]
        [DataRow(ItemField.CustomerName, "customer_name")]
        public void ToWireName_MatchesTheBackendFieldName(ItemField field, string expectedWireName)
        {
            Assert.AreEqual(expectedWireName, field.ToWireName());
        }

        [TestMethod]
        public void ToWireValue_MadeAndBoughtUseTheirLowercaseLiterals()
        {
            Assert.AreEqual("made", ItemKind.Made.ToWireValue());
            Assert.AreEqual("bought", ItemKind.Bought.ToWireValue());
        }

        [TestMethod]
        public void TryParseWireValue_RecognizesTheTwoExactWireValues()
        {
            ItemKind kind;

            Assert.IsTrue(ItemKindExtensions.TryParseWireValue("made", out kind));
            Assert.AreEqual(ItemKind.Made, kind);

            Assert.IsTrue(ItemKindExtensions.TryParseWireValue("bought", out kind));
            Assert.AreEqual(ItemKind.Bought, kind);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Bought")]
        [DataRow("made ")]
        [DataRow("something-else")]
        public void TryParseWireValue_RejectsAnythingThatIsNotAnExactMatch(string value)
        {
            ItemKind kind;

            Assert.IsFalse(ItemKindExtensions.TryParseWireValue(value, out kind));
            Assert.AreEqual(default(ItemKind), kind);
        }

        [TestMethod]
        [DataRow(FieldOwner.Unknown, "Unknown")]
        [DataRow(FieldOwner.Pdm, "PDM")]
        [DataRow(FieldOwner.AddIn, "Add-in")]
        [DataRow(FieldOwner.AddInWeb, "Add-in / Web")]
        [DataRow(FieldOwner.Web, "Web")]
        [DataRow(FieldOwner.Server, "Server")]
        public void ToDisplayString_MatchesFieldOwnershipDocTables(FieldOwner owner, string expected)
        {
            Assert.AreEqual(expected, owner.ToDisplayString());
        }

        [TestMethod]
        public void Defaults_EveryWritableEntryHasANonEmptyPropertyName()
        {
            foreach (var kind in new[] { ItemKind.Made, ItemKind.Bought })
            {
                foreach (var entry in FieldMapping.Defaults[kind])
                {
                    // status is PDM-owned but follows the PDM workflow state, not a datacard
                    // variable, so it is the one PDM-owned field without a custom property.
                    bool writable = (entry.Owner == FieldOwner.Pdm && entry.Field != "status")
                        || entry.Owner == FieldOwner.AddIn
                        || entry.Owner == FieldOwner.AddInWeb;

                    if (writable)
                    {
                        Assert.IsFalse(
                            string.IsNullOrEmpty(entry.PropertyName),
                            $"{kind}/{entry.Field} ({entry.Owner}) should resolve to a custom property.");
                    }
                    else
                    {
                        Assert.IsNull(
                            entry.PropertyName,
                            $"{kind}/{entry.Field} ({entry.Owner}) is never written and should carry no custom property.");
                    }
                }
            }
        }

        [TestMethod]
        public void Defaults_PdmOwnedFieldsMapToPdmDotProperties()
        {
            foreach (var kind in new[] { ItemKind.Made, ItemKind.Bought })
            {
                var pdmEntries = FieldMapping.Defaults[kind]
                    .Where(e => e.Owner == FieldOwner.Pdm && e.PropertyName != null)
                    .ToList();
                Assert.AreNotEqual(0, pdmEntries.Count, $"{kind} should have at least one PDM-owned field.");

                foreach (var entry in pdmEntries)
                {
                    Assert.IsTrue(
                        entry.PropertyName.StartsWith("PDM.", StringComparison.Ordinal),
                        $"{kind}/{entry.Field} is PDM-owned but maps to '{entry.PropertyName}'.");
                }
            }
        }

        [TestMethod]
        [DataRow(ItemKind.Made)]
        [DataRow(ItemKind.Bought)]
        public void Defaults_StatusIsPdmOwnedWithoutAProperty(ItemKind kind)
        {
            var entry = FieldMapping.Defaults[kind].Single(e => e.Field == "status");

            Assert.AreEqual(FieldOwner.Pdm, entry.Owner);
            Assert.IsNull(entry.PropertyName);
        }

        [TestMethod]
        public void Defaults_FieldNamesAreUniquePerKind()
        {
            foreach (var kind in new[] { ItemKind.Made, ItemKind.Bought })
            {
                var fields = FieldMapping.Defaults[kind].Select(e => e.Field).ToList();
                CollectionAssert.AllItemsAreUnique(fields, $"{kind} has a duplicate field entry.");
            }
        }

        [TestMethod]
        public void Defaults_LabelOfPointsToAFieldDeclaredInTheSameKind()
        {
            foreach (var kind in new[] { ItemKind.Made, ItemKind.Bought })
            {
                var fields = new HashSet<string>(FieldMapping.Defaults[kind].Select(e => e.Field), StringComparer.Ordinal);

                foreach (var entry in FieldMapping.Defaults[kind].Where(e => e.LabelOf != null))
                {
                    Assert.IsTrue(
                        fields.Contains(entry.LabelOf),
                        $"{kind}/{entry.Field}.LabelOf = '{entry.LabelOf}' has no matching field entry.");
                }
            }
        }

        [TestMethod]
        public void Defaults_BoughtEdpPartIdIsAddInWebOwnedAndMappedToItsProperty()
        {
            var entry = FieldMapping.Defaults[ItemKind.Bought].Single(e => e.Field == "edp_part_id");

            Assert.AreEqual(FieldOwner.AddInWeb, entry.Owner);
            Assert.AreEqual("Glados.EdpPartId", entry.PropertyName);
            Assert.IsFalse(entry.Required);
            Assert.IsFalse(FieldMapping.Defaults[ItemKind.Made].Any(e => e.Field == "edp_part_id"));
        }

        [TestMethod]
        public void Defaults_MadeCustomerFieldsAreAddInOwnedWithTheNameAsLabelOfTheId()
        {
            var made = FieldMapping.Defaults[ItemKind.Made];

            foreach (var expected in new[]
            {
                new { Field = "customer_id", Property = "Glados.CustomerId" },
                new { Field = "customer_part_number", Property = "Glados.CustomerPartNumber" },
                new { Field = "customer_revision", Property = "Glados.CustomerRevision" },
            })
            {
                var entry = made.Single(e => e.Field == expected.Field);
                Assert.AreEqual(FieldOwner.AddIn, entry.Owner, expected.Field);
                Assert.AreEqual(expected.Property, entry.PropertyName, expected.Field);
                Assert.AreEqual(true, entry.UpdateInPlace, expected.Field);
                Assert.IsFalse(entry.Required, expected.Field);
            }

            var label = made.Single(e => e.Field == "customer_name");
            Assert.AreEqual("Glados.CustomerName", label.PropertyName);
            Assert.AreEqual("customer_id", label.LabelOf);
            Assert.IsFalse(FieldMapping.Defaults[ItemKind.Bought].Any(e => e.Field.StartsWith("customer_")));
        }

        [TestMethod]
        public void DefaultTemplates_MatchTheDocumentedDefaults()
        {
            Assert.AreEqual(
                "$PRP:\"PDM.PartNumber\" Rev$PRP:\"PDM.Revision\" ($PRP:\"Glados.ArticleName\")",
                FieldMapping.DefaultTemplates.Made);
            Assert.AreEqual(
                "$PRP:\"Glados.ArticleName\" - $PRP:\"Glados.OrderCode1\" - $PRP:\"Glados.ManufacturerName\"",
                FieldMapping.DefaultTemplates.Bought);
        }

        [TestMethod]
        public void DefaultRules_AreEmptyForBothKindsUntilTheFirstSync()
        {
            Assert.AreEqual(0, FieldMapping.DefaultRules[ItemKind.Made].Count);
            Assert.AreEqual(0, FieldMapping.DefaultRules[ItemKind.Bought].Count);
        }
    }
}
