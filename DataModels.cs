//last edited on 20250630-001 - Added AddressExtractionResult
using System.Collections.Generic;

namespace ConsoleApp1
{
    /// <summary>
    /// Result of name extraction with individual components and metadata
    /// </summary>
    public class NameExtractionResult
    {
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string SourceFields { get; set; } = "";
    }

    /// <summary>
    /// Result of field mapping with value and source information
    /// </summary>
    public class FieldMappingResult
    {
        public string Value { get; set; } = "";
        public string SecondaryValue { get; set; } = "";
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Enhanced address extraction result with multiple address support
    /// </summary>
    public class AddressExtractionResult
    {
        public string FullAddress { get; set; } = "";
        public string Address1 { get; set; } = "";      // Street address line 1
        public string Address2 { get; set; } = "";      // Street address line 2  
        public string City { get; set; } = "";
        public string StateOrProvince { get; set; } = "";
        public string Country { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string AddressType { get; set; } = "";   // "components", "full", "list", "structured"
        public string SourceFields { get; set; } = "";
        public List<string> AllSourceFields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of identifier extraction (for future use)
    /// </summary>
    public class IdentifierResult
    {
        public string OriginalId { get; set; } = "";
        public string QrCode { get; set; } = "";
        public string QrCode2 { get; set; } = "";
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Result of date extraction (for future use)
    /// </summary>
    public class DateResult
    {
        public string OriginalValue { get; set; } = "";
        public string StandardizedDate { get; set; } = "";
        public string DateType { get; set; } = "";  // "birth", "death", "listed", etc.
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Result of alias extraction (for future use)
    /// </summary>
    public class AliasResult
    {
        public string AliasName { get; set; } = "";
        public string AliasType { get; set; } = "";     // "a.k.a.", "f.k.a.", "weak", "strong"
        public string Quality { get; set; } = "";
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Result structure for extracted identifiers
    /// </summary>
    public class IdentifierResult2
    {
        public string Category { get; set; } = "";     // CNIC, PASSPORT, NATIONAL_ID, SSN
        public string Value { get; set; } = "";        // The actual identifier value
        public string SourceField { get; set; } = "";  // Which field(s) this came from
    }
}