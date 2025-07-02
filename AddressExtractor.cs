using System; //AddressExtractor.cs - Created 20250630
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConsoleApp1
{
    /// <summary>
    /// Extracts and standardizes address information from parsed records
    /// Handles multiple address formats and components
    /// </summary>
    public static class AddressExtractor
    {
        /// <summary>
        /// Main method to extract address components from a record
        /// </summary>
        /// <param name="record">Parsed record dictionary</param>
        /// <returns>Primary address extraction result</returns>
        public static AddressExtractionResult ExtractAddressComponents(Dictionary<string, object> record)
        {
            try
            {
                // First try to find multiple structured addresses
                var multipleAddresses = ExtractMultipleAddresses(record);
                if (multipleAddresses.Count > 0)
                {
                    // Return the first/primary address
                    var primaryAddress = multipleAddresses.First();
                    primaryAddress.SourceFields = $"Multiple addresses found ({multipleAddresses.Count})";
                    return primaryAddress;
                }

                // Fallback to single address extraction
                return ExtractSingleAddress(record);
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error in address extraction: {ex.Message}");
                return new AddressExtractionResult
                {
                    SourceFields = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extract multiple addresses from records that contain address lists
        /// </summary>
        /// <param name="record">Parsed record dictionary</param>
        /// <returns>List of extracted addresses</returns>
        private static List<AddressExtractionResult> ExtractMultipleAddresses(Dictionary<string, object> record)
        {
            var addresses = new List<AddressExtractionResult>();

            // Look for numbered address patterns (address1, address2, etc.)
            var addressGroups = GroupAddressesByIndex(record);
            foreach (var group in addressGroups)
            {
                var address = BuildAddressFromGroup(group.Value, group.Key);
                if (!IsEmptyAddress(address))
                {
                    addresses.Add(address);
                }
            }

            // Look for addressList or similar container patterns
            var addressListFields = record.Where(kv =>
                kv.Key.IndexOf("address", StringComparison.OrdinalIgnoreCase) >= 0 &&
                kv.Key.IndexOf("list", StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var listField in addressListFields)
            {
                // This would contain multiple address entries
                // For now, we'll mark it as found but not fully parse
                if (!string.IsNullOrEmpty(listField.Value?.ToString()))
                {
                    addresses.Add(new AddressExtractionResult
                    {
                        FullAddress = "Multiple addresses in list",
                        SourceFields = listField.Key,
                        AddressType = "list"
                    });
                }
            }

            return addresses;
        }

        /// <summary>
        /// Group address fields by their numeric suffix or UID
        /// </summary>
        /// <param name="record">Parsed record dictionary</param>
        /// <returns>Dictionary of grouped address fields</returns>
        private static Dictionary<string, Dictionary<string, object>> GroupAddressesByIndex(Dictionary<string, object> record)
        {
            var groups = new Dictionary<string, Dictionary<string, object>>();

            // Address patterns with potential grouping
            var addressFields = record.Where(kv =>
                IsAddressRelatedField(kv.Key) && !string.IsNullOrEmpty(kv.Value?.ToString()));

            foreach (var field in addressFields)
            {
                var groupKey = ExtractGroupKey(field.Key);
                if (!groups.ContainsKey(groupKey))
                {
                    groups[groupKey] = new Dictionary<string, object>();
                }
                groups[groupKey][field.Key] = field.Value;
            }

            return groups.Where(g => g.Value.Count > 1).ToDictionary(g => g.Key, g => g.Value);
        }

        /// <summary>
        /// Check if a field name is address-related
        /// </summary>
        /// <param name="fieldName">Field name to check</param>
        /// <returns>True if field is address-related</returns>
        private static bool IsAddressRelatedField(string fieldName)
        {
            string[] addressPatterns = {
                "address", "Address", "street", "Street", "city", "City",
                "country", "Country", "state", "State", "province", "Province",
                "postal", "Postal", "PostCode", "zip", "Zip", "location", "Location"
            };

            return addressPatterns.Any(pattern =>
                fieldName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Extract group key for address field grouping
        /// </summary>
        /// <param name="fieldName">Field name</param>
        /// <returns>Group key</returns>
        private static string ExtractGroupKey(string fieldName)
        {
            // Look for numeric suffix (address1, Address1, etc.)
            var match = Regex.Match(fieldName, @"(\d+)$");
            if (match.Success)
            {
                return $"group_{match.Value}";
            }

            // Look for UID patterns (address_uid_123, etc.)
            var uidMatch = Regex.Match(fieldName, @"uid[_]?(\d+)", RegexOptions.IgnoreCase);
            if (uidMatch.Success)
            {
                return $"uid_{uidMatch.Groups[1].Value}";
            }

            // Default grouping
            return "default";
        }

        /// <summary>
        /// Build address from grouped fields
        /// </summary>
        /// <param name="groupFields">Grouped address fields</param>
        /// <param name="groupKey">Group identifier</param>
        /// <returns>Address extraction result</returns>
        private static AddressExtractionResult BuildAddressFromGroup(Dictionary<string, object> groupFields, string groupKey)
        {
            var result = new AddressExtractionResult();
            result.AllSourceFields = groupFields.Keys.ToList();
            result.SourceFields = string.Join(", ", groupFields.Keys);

            // Map common field patterns
            result.Address1 = FindFieldValue(groupFields, new[] { "address1", "Address1", "street1", "line1" });
            result.Address2 = FindFieldValue(groupFields, new[] { "address2", "Address2", "street2", "line2" });
            result.City = FindFieldValue(groupFields, new[] { "city", "City", "town", "Town" });
            result.StateOrProvince = FindFieldValue(groupFields, new[] { "state", "State", "province", "Province", "stateOrProvince" });
            result.Country = FindFieldValue(groupFields, new[] { "country", "Country", "nation", "Nation" });
            result.PostalCode = FindFieldValue(groupFields, new[] { "postal", "Postal", "PostCode", "zip", "Zip", "postalCode" });

            // Build full address
            result.FullAddress = BuildFullAddress(result);
            result.AddressType = "structured";

            return result;
        }

        /// <summary>
        /// Extract single address when no structured multiple addresses found
        /// </summary>
        /// <param name="record">Parsed record dictionary</param>
        /// <returns>Address extraction result</returns>
        private static AddressExtractionResult ExtractSingleAddress(Dictionary<string, object> record)
        {
            var result = new AddressExtractionResult();
            var sourceFields = new List<string>();

            // Primary address extraction patterns
            result.Address1 = FindFieldValueFromRecord(record, new[] { "address1", "Address1", "street", "Street", "line1" }, sourceFields);
            result.Address2 = FindFieldValueFromRecord(record, new[] { "address2", "Address2", "street2", "line2" }, sourceFields);
            result.City = FindFieldValueFromRecord(record, new[] { "city", "City", "town", "Town" }, sourceFields);
            result.StateOrProvince = FindFieldValueFromRecord(record, new[] { "state", "State", "province", "Province", "stateOrProvince" }, sourceFields);
            result.Country = FindFieldValueFromRecord(record, new[] { "country", "Country", "nation", "Nation" }, sourceFields);
            result.PostalCode = FindFieldValueFromRecord(record, new[] { "postal", "Postal", "PostCode", "zip", "Zip", "postalCode" }, sourceFields);

            // Try broader address patterns if specific components not found
            if (IsEmptyAddress(result))
            {
                var fullAddressValue = FindFieldValueFromRecord(record, new[] { "address", "Address", "location", "Location", "fullAddress" }, sourceFields);
                if (!string.IsNullOrEmpty(fullAddressValue))
                {
                    result.FullAddress = fullAddressValue;
                    result.AddressType = "full";
                }
            }
            else
            {
                result.FullAddress = BuildFullAddress(result);
                result.AddressType = "components";
            }

            result.SourceFields = sourceFields.Count > 0 ? string.Join(", ", sourceFields) : "No address fields found";
            result.AllSourceFields = sourceFields;

            return result;
        }

        /// <summary>
        /// Find field value from a group using patterns
        /// </summary>
        /// <param name="fields">Field dictionary</param>
        /// <param name="patterns">Field name patterns to search</param>
        /// <returns>Field value or empty string</returns>
        private static string FindFieldValue(Dictionary<string, object> fields, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var match = fields.FirstOrDefault(kv =>
                    kv.Key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrEmpty(match.Value?.ToString()))
                {
                    return match.Value.ToString().Trim();
                }
            }
            return "";
        }

        /// <summary>
        /// Find field value from record using patterns and track source fields
        /// </summary>
        /// <param name="record">Full record dictionary</param>
        /// <param name="patterns">Field name patterns to search</param>
        /// <param name="sourceFields">List to track source field names</param>
        /// <returns>Field value or empty string</returns>
        private static string FindFieldValueFromRecord(Dictionary<string, object> record, string[] patterns, List<string> sourceFields)
        {
            foreach (var pattern in patterns)
            {
                var match = record.FirstOrDefault(kv =>
                    kv.Key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrEmpty(match.Value?.ToString()))
                {
                    sourceFields.Add(match.Key);
                    return match.Value.ToString().Trim();
                }
            }
            return "";
        }

        /// <summary>
        /// Build full address string from components
        /// </summary>
        /// <param name="address">Address extraction result</param>
        /// <returns>Formatted full address string</returns>
        private static string BuildFullAddress(AddressExtractionResult address)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(address.Address1)) parts.Add(address.Address1);
            if (!string.IsNullOrEmpty(address.Address2)) parts.Add(address.Address2);
            if (!string.IsNullOrEmpty(address.City)) parts.Add(address.City);
            if (!string.IsNullOrEmpty(address.StateOrProvince)) parts.Add(address.StateOrProvince);
            if (!string.IsNullOrEmpty(address.PostalCode)) parts.Add(address.PostalCode);
            if (!string.IsNullOrEmpty(address.Country)) parts.Add(address.Country);

            return string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>
        /// Check if address extraction result is empty
        /// </summary>
        /// <param name="address">Address to check</param>
        /// <returns>True if address has no meaningful data</returns>
        private static bool IsEmptyAddress(AddressExtractionResult address)
        {
            return string.IsNullOrEmpty(address.Address1) &&
                   string.IsNullOrEmpty(address.Address2) &&
                   string.IsNullOrEmpty(address.City) &&
                   string.IsNullOrEmpty(address.StateOrProvince) &&
                   string.IsNullOrEmpty(address.Country) &&
                   string.IsNullOrEmpty(address.PostalCode) &&
                   string.IsNullOrEmpty(address.FullAddress);
        }

        /// <summary>
        /// Log detailed address extraction information
        /// </summary>
        /// <param name="result">Address extraction result</param>
        /// <param name="fileName">Source file name</param>
        public static void LogAddressExtractionDetails(AddressExtractionResult result, string fileName)
        {
            FileLogger.LogMessage($"\n🏠 ADDRESS EXTRACTION: {fileName}");
            FileLogger.LogMessage($"   Full: '{result.FullAddress}'");
            FileLogger.LogMessage($"   Street: '{result.Address1}' | City: '{result.City}' | Country: '{result.Country}'");
            FileLogger.LogMessage($"   State: '{result.StateOrProvince}' | Postal: '{result.PostalCode}'");
            FileLogger.LogMessage($"   Type: {result.AddressType} | Source Fields: {result.SourceFields}");
        }
    }
}