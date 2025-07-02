using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConsoleApp1
{
    /// <summary>
    /// Extractor for CNIC, passport numbers, and other identifiers from various sanctions list formats
    /// Supports: OFAC SDN, UN Consolidated, EU, NACTA, and other XML formats
    /// </summary>
    public static class IdentifierExtractor
    {
        /// <summary>
        /// Extract all identifiers (CNIC, passport, etc.) from a record
        /// </summary>
        /// <param name="record">Dictionary containing all fields from the record</param>
        /// <returns>List of identifier objects with category and value</returns>
        public static List<ExtractedIdentifier> ExtractIdentifiers(Dictionary<string, object> record)
        {
            var identifiers = new List<ExtractedIdentifier>();

            try
            {
                // 1. Extract CNICs
                identifiers.AddRange(ExtractCnics(record));

                // 2. Extract Passport Numbers
                identifiers.AddRange(ExtractPassports(record));

                // 3. Extract National ID Numbers
                identifiers.AddRange(ExtractNationalIds(record));

                // 4. Extract Social Security Numbers
                identifiers.AddRange(ExtractSocialSecurityNumbers(record));

                // Remove duplicates based on category and value
                identifiers = RemoveDuplicateIdentifiers(identifiers);

                return identifiers;
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error in IdentifierExtractor.ExtractIdentifiers: {ex.Message}");
                return new List<ExtractedIdentifier>();
            }
        }

        /// <summary>
        /// Extract CNIC numbers from various field formats
        /// </summary>
        private static List<ExtractedIdentifier> ExtractCnics(Dictionary<string, object> record)
        {
            var cnics = new List<ExtractedIdentifier>();

            // NACTA format: Direct CNIC field
            if (record.ContainsKey("CNIC") && record["CNIC"] != null)
            {
                string cnicValue = record["CNIC"].ToString().Trim();
                if (IsValidCnic(cnicValue))
                {
                    cnics.Add(new ExtractedIdentifier
                    {
                        Category = "CNIC",
                        Value = NormalizeCnic(cnicValue),
                        SourceField = "CNIC"
                    });
                }
            }

            // Search other fields for CNIC patterns
            var cnicPatterns = new[]
            {
                @"\b\d{5}-?\d{7}-?\d{1}\b",  // 13-digit CNIC with optional dashes
                @"\b\d{13}\b"                 // 13-digit CNIC without dashes
            };

            foreach (var field in record)
            {
                if (field.Value == null) continue;
                string fieldValue = field.Value.ToString();

                foreach (var pattern in cnicPatterns)
                {
                    var matches = Regex.Matches(fieldValue, pattern);
                    foreach (Match match in matches)
                    {
                        string potentialCnic = match.Value.Replace("-", "");
                        if (IsValidCnic(potentialCnic))
                        {
                            cnics.Add(new ExtractedIdentifier
                            {
                                Category = "CNIC",
                                Value = NormalizeCnic(potentialCnic),
                                SourceField = field.Key
                            });
                        }
                    }
                }
            }

            return cnics;
        }

        /// <summary>
        /// Extract passport numbers from various XML formats
        /// </summary>
        private static List<ExtractedIdentifier> ExtractPassports(Dictionary<string, object> record)
        {
            var passports = new List<ExtractedIdentifier>();

            // OFAC SDN format: idType="Passport"
            ExtractOfacPassports(record, passports);

            // UN Consolidated format: INDIVIDUAL_DOCUMENT with TYPE_OF_DOCUMENT="Passport"
            ExtractUnPassports(record, passports);

            // EU format: identificationTypeCode="passport"
            ExtractEuPassports(record, passports);

            // Generic passport field search
            ExtractGenericPassports(record, passports);

            return passports;
        }

        /// <summary>
        /// Extract OFAC SDN passport format
        /// </summary>
        private static void ExtractOfacPassports(Dictionary<string, object> record, List<ExtractedIdentifier> passports)
        {
            // Look for pattern: idType_X = "Passport" and idNumber_X = "number"
            var idTypeFields = record.Keys.Where(k => k.StartsWith("idType_")).ToList();

            foreach (var idTypeField in idTypeFields)
            {
                if (record[idTypeField]?.ToString() == "Passport")
                {
                    // Extract the index number
                    string index = idTypeField.Replace("idType_", "");
                    string idNumberField = $"idNumber_{index}";

                    if (record.ContainsKey(idNumberField) && record[idNumberField] != null)
                    {
                        string passportNumber = record[idNumberField].ToString().Trim();
                        if (!string.IsNullOrEmpty(passportNumber))
                        {
                            passports.Add(new ExtractedIdentifier
                            {
                                Category = "PASSPORT",
                                Value = passportNumber,
                                SourceField = $"{idTypeField}, {idNumberField}"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract UN Consolidated passport format
        /// </summary>
        private static void ExtractUnPassports(Dictionary<string, object> record, List<ExtractedIdentifier> passports)
        {
            // Look for INDIVIDUAL_DOCUMENT_TYPE_OF_DOCUMENT_X = "Passport"
            var docTypeFields = record.Keys.Where(k => k.Contains("INDIVIDUAL_DOCUMENT_TYPE_OF_DOCUMENT_")).ToList();

            foreach (var docTypeField in docTypeFields)
            {
                if (record[docTypeField]?.ToString() == "Passport")
                {
                    // Extract index and look for corresponding NUMBER field
                    string pattern = @"INDIVIDUAL_DOCUMENT_TYPE_OF_DOCUMENT_(\d+)";
                    var match = Regex.Match(docTypeField, pattern);
                    if (match.Success)
                    {
                        string index = match.Groups[1].Value;
                        string numberField = $"INDIVIDUAL_DOCUMENT_NUMBER_{index}";

                        if (record.ContainsKey(numberField) && record[numberField] != null)
                        {
                            string passportNumber = record[numberField].ToString().Trim();
                            if (!string.IsNullOrEmpty(passportNumber))
                            {
                                passports.Add(new ExtractedIdentifier
                                {
                                    Category = "PASSPORT",
                                    Value = passportNumber,
                                    SourceField = $"{docTypeField}, {numberField}"
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract EU format passport numbers
        /// </summary>
        private static void ExtractEuPassports(Dictionary<string, object> record, List<ExtractedIdentifier> passports)
        {
            // Look for identificationTypeCode_X = "passport" and number_X
            var idCodeFields = record.Keys.Where(k => k.StartsWith("identificationTypeCode_")).ToList();

            foreach (var idCodeField in idCodeFields)
            {
                if (record[idCodeField]?.ToString() == "passport")
                {
                    string index = idCodeField.Replace("identificationTypeCode_", "");
                    string numberField = $"number_{index}";

                    if (record.ContainsKey(numberField) && record[numberField] != null)
                    {
                        string passportNumber = record[numberField].ToString().Trim();
                        if (!string.IsNullOrEmpty(passportNumber))
                        {
                            passports.Add(new ExtractedIdentifier
                            {
                                Category = "PASSPORT",
                                Value = passportNumber,
                                SourceField = $"{idCodeField}, {numberField}"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract passport numbers from generic passport-related fields
        /// </summary>
        private static void ExtractGenericPassports(Dictionary<string, object> record, List<ExtractedIdentifier> passports)
        {
            var passportFields = new[] { "Passport", "passport", "PassportNumber", "passport_number" };

            foreach (var field in passportFields)
            {
                if (record.ContainsKey(field) && record[field] != null)
                {
                    string passportValue = record[field].ToString().Trim();
                    if (!string.IsNullOrEmpty(passportValue) && IsValidPassportNumber(passportValue))
                    {
                        passports.Add(new ExtractedIdentifier
                        {
                            Category = "PASSPORT",
                            Value = passportValue,
                            SourceField = field
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Extract National ID numbers (non-CNIC)
        /// </summary>
        private static List<ExtractedIdentifier> ExtractNationalIds(Dictionary<string, object> record)
        {
            var nationalIds = new List<ExtractedIdentifier>();

            // OFAC format: idType = "National ID No."
            ExtractOfacNationalIds(record, nationalIds);

            // UN format: TYPE_OF_DOCUMENT = "National Identification Number"
            ExtractUnNationalIds(record, nationalIds);

            // EU format: identificationTypeCode = "id"
            ExtractEuNationalIds(record, nationalIds);

            return nationalIds;
        }

        /// <summary>
        /// Extract OFAC National ID format
        /// </summary>
        private static void ExtractOfacNationalIds(Dictionary<string, object> record, List<ExtractedIdentifier> nationalIds)
        {
            var idTypeFields = record.Keys.Where(k => k.StartsWith("idType_")).ToList();

            foreach (var idTypeField in idTypeFields)
            {
                string idType = record[idTypeField]?.ToString() ?? "";
                if (idType == "National ID No." || idType.Contains("National"))
                {
                    string index = idTypeField.Replace("idType_", "");
                    string idNumberField = $"idNumber_{index}";

                    if (record.ContainsKey(idNumberField) && record[idNumberField] != null)
                    {
                        string nationalId = record[idNumberField].ToString().Trim();
                        if (!string.IsNullOrEmpty(nationalId))
                        {
                            nationalIds.Add(new ExtractedIdentifier
                            {
                                Category = "NATIONAL_ID",
                                Value = nationalId,
                                SourceField = $"{idTypeField}, {idNumberField}"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract UN National ID format
        /// </summary>
        private static void ExtractUnNationalIds(Dictionary<string, object> record, List<ExtractedIdentifier> nationalIds)
        {
            var docTypeFields = record.Keys.Where(k => k.Contains("INDIVIDUAL_DOCUMENT_TYPE_OF_DOCUMENT_")).ToList();

            foreach (var docTypeField in docTypeFields)
            {
                string docType = record[docTypeField]?.ToString() ?? "";
                if (docType == "National Identification Number" || docType.Contains("National"))
                {
                    string pattern = @"INDIVIDUAL_DOCUMENT_TYPE_OF_DOCUMENT_(\d+)";
                    var match = Regex.Match(docTypeField, pattern);
                    if (match.Success)
                    {
                        string index = match.Groups[1].Value;
                        string numberField = $"INDIVIDUAL_DOCUMENT_NUMBER_{index}";

                        if (record.ContainsKey(numberField) && record[numberField] != null)
                        {
                            string nationalId = record[numberField].ToString().Trim();
                            if (!string.IsNullOrEmpty(nationalId))
                            {
                                nationalIds.Add(new ExtractedIdentifier
                                {
                                    Category = "NATIONAL_ID",
                                    Value = nationalId,
                                    SourceField = $"{docTypeField}, {numberField}"
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract EU National ID format
        /// </summary>
        private static void ExtractEuNationalIds(Dictionary<string, object> record, List<ExtractedIdentifier> nationalIds)
        {
            var idCodeFields = record.Keys.Where(k => k.StartsWith("identificationTypeCode_")).ToList();

            foreach (var idCodeField in idCodeFields)
            {
                if (record[idCodeField]?.ToString() == "id")
                {
                    string index = idCodeField.Replace("identificationTypeCode_", "");
                    string numberField = $"number_{index}";

                    if (record.ContainsKey(numberField) && record[numberField] != null)
                    {
                        string nationalId = record[numberField].ToString().Trim();
                        if (!string.IsNullOrEmpty(nationalId))
                        {
                            nationalIds.Add(new ExtractedIdentifier
                            {
                                Category = "NATIONAL_ID",
                                Value = nationalId,
                                SourceField = $"{idCodeField}, {numberField}"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract Social Security Numbers
        /// </summary>
        private static List<ExtractedIdentifier> ExtractSocialSecurityNumbers(Dictionary<string, object> record)
        {
            var ssns = new List<ExtractedIdentifier>();

            // EU format: identificationTypeCode = "ssn"
            var idCodeFields = record.Keys.Where(k => k.StartsWith("identificationTypeCode_")).ToList();

            foreach (var idCodeField in idCodeFields)
            {
                if (record[idCodeField]?.ToString() == "ssn")
                {
                    string index = idCodeField.Replace("identificationTypeCode_", "");
                    string numberField = $"number_{index}";

                    if (record.ContainsKey(numberField) && record[numberField] != null)
                    {
                        string ssn = record[numberField].ToString().Trim();
                        if (!string.IsNullOrEmpty(ssn))
                        {
                            ssns.Add(new ExtractedIdentifier
                            {
                                Category = "SSN",
                                Value = ssn,
                                SourceField = $"{idCodeField}, {numberField}"
                            });
                        }
                    }
                }
            }

            // Search for SSN patterns in all fields
            var ssnPattern = @"\b\d{3}-\d{2}-\d{4}\b"; // US SSN format
            foreach (var field in record)
            {
                if (field.Value == null) continue;
                string fieldValue = field.Value.ToString();

                var matches = Regex.Matches(fieldValue, ssnPattern);
                foreach (Match match in matches)
                {
                    ssns.Add(new ExtractedIdentifier
                    {
                        Category = "SSN",
                        Value = match.Value,
                        SourceField = field.Key
                    });
                }
            }

            return ssns;
        }

        /// <summary>
        /// Validate if a string is a valid CNIC (13 digits)
        /// </summary>
        private static bool IsValidCnic(string cnic)
        {
            if (string.IsNullOrEmpty(cnic)) return false;

            // Remove dashes and check if it's exactly 13 digits
            string cleanCnic = cnic.Replace("-", "");
            return cleanCnic.Length == 13 && cleanCnic.All(char.IsDigit);
        }

        /// <summary>
        /// Normalize CNIC format (add dashes: XXXXX-XXXXXXX-X)
        /// </summary>
        private static string NormalizeCnic(string cnic)
        {
            if (string.IsNullOrEmpty(cnic)) return cnic;

            string cleanCnic = cnic.Replace("-", "");
            if (cleanCnic.Length == 13)
            {
                return $"{cleanCnic.Substring(0, 5)}-{cleanCnic.Substring(5, 7)}-{cleanCnic.Substring(12, 1)}";
            }

            return cnic; // Return original if not 13 digits
        }

        /// <summary>
        /// Basic validation for passport numbers
        /// </summary>
        private static bool IsValidPassportNumber(string passport)
        {
            if (string.IsNullOrEmpty(passport)) return false;

            // Basic checks: length between 4-20 characters, alphanumeric
            return passport.Length >= 4 && passport.Length <= 20 &&
                   passport.All(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ');
        }

        /// <summary>
        /// Remove duplicate identifiers based on category and value
        /// </summary>
        private static List<ExtractedIdentifier> RemoveDuplicateIdentifiers(List<ExtractedIdentifier> identifiers)
        {
            var uniqueIdentifiers = new List<ExtractedIdentifier>();
            var seen = new HashSet<string>();

            foreach (var identifier in identifiers)
            {
                string key = $"{identifier.Category}:{identifier.Value}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueIdentifiers.Add(identifier);
                }
            }

            return uniqueIdentifiers;
        }
    }

    /// <summary>
    /// Result structure for extracted identifiers (renamed from IdentifierResult to avoid conflicts)
    /// </summary>
    public class ExtractedIdentifier
    {
        public string Category { get; set; } = "";     // CNIC, PASSPORT, NATIONAL_ID, SSN
        public string Value { get; set; } = "";        // The actual identifier value
        public string SourceField { get; set; } = "";  // Which field(s) this came from
    }
}