using GladosSwAddin.Api.Models;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Covers CustomerFolderMatcher, the guess that pre-selects a new customer item's customer
    /// from a folder of the document's path named after that customer.
    /// </summary>
    [TestClass]
    public class CustomerFolderMatcherTests
    {
        private static readonly CustomerLookupResponse Acme =
            new CustomerLookupResponse { Id = 1, Name = "ACME Industries" };

        private static readonly CustomerLookupResponse Contoso =
            new CustomerLookupResponse { Id = 2, Name = "Contoso" };

        private static readonly CustomerLookupResponse[] Customers = { Acme, Contoso };

        [TestMethod]
        public void Match_FindsTheCustomerFolder()
        {
            var match = CustomerFolderMatcher.Match(
                @"C:\Vault\Customers\Contoso\Bracket\bracket.SLDPRT", Customers);

            Assert.AreSame(Contoso, match);
        }

        [TestMethod]
        public void Match_IgnoresCaseAndExtraWhitespace()
        {
            var match = CustomerFolderMatcher.Match(
                @"\\vault\data\acme  industries \part.sldprt", Customers);

            Assert.AreSame(Acme, match);
        }

        [TestMethod]
        public void Match_PrefersTheFolderClosestToTheFile()
        {
            var match = CustomerFolderMatcher.Match(
                @"C:\Vault\Contoso\ACME Industries\part.sldprt", Customers);

            Assert.AreSame(Acme, match);
        }

        [TestMethod]
        public void Match_NeverMatchesTheFileName()
        {
            Assert.IsNull(CustomerFolderMatcher.Match(@"C:\Vault\Parts\Contoso", Customers));
        }

        [TestMethod]
        public void Match_SkipsArchivedCustomers()
        {
            var archived = new CustomerLookupResponse { Id = 3, Name = "Fabrikam", Archived = true };

            var match = CustomerFolderMatcher.Match(
                @"C:\Vault\Fabrikam\part.sldprt", new[] { Acme, archived });

            Assert.IsNull(match);
        }

        [TestMethod]
        public void Match_ReturnsNullWhenTwoCustomersShareTheName()
        {
            var twin = new CustomerLookupResponse { Id = 4, Name = "contoso" };

            var match = CustomerFolderMatcher.Match(
                @"C:\Vault\Contoso\part.sldprt", new[] { Contoso, twin });

            Assert.IsNull(match);
        }

        [TestMethod]
        public void Match_ReturnsNullForAnUnsavedDocumentOrNoMatch()
        {
            Assert.IsNull(CustomerFolderMatcher.Match(string.Empty, Customers));
            Assert.IsNull(CustomerFolderMatcher.Match(@"C:\Vault\Parts\part.sldprt", Customers));
        }
    }
}
