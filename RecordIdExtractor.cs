using System; //last edited on 20250630-001
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>
    /// Handles extraction and generation of system-level record identifiers (qrCode/qrCode2)
    /// Does NOT handle personal identity documents (passports, CNIC, etc.)
    /// </summary>
    public static class RecordIdExtractor
    {
        /// <summary>
        /// Main method to find existing system ID or generate unique identifiers
        /// Returns both qrCode (original ID) and qrCode2 (generated hash)
        /// </summary>
        /// <param name="record">Record dictionary to search</param>
        /// <param name="fileName">Source file name for hash generation</param>
        /// <returns>FieldMappingResult with Value=qrCode, SecondaryValue=qrCode2</returns>
        public static FieldMappingResult FindOrGenerateRecordId(Dictionary<string, object> record, string fileName)
        {
            // STEP 1: Look for existing system-level unique ID
            var originalIdResult = FindOriginalSystemId(record);

            // STEP 2: Always generate hash-based ID from record content
            var hashIdResult = GenerateHashFromRecordContent(record, fileName);

            // STEP 3: Return both IDs
            return new FieldMappingResult
            {
                Value = originalIdResult.Value,           // qrCode (original ID or empty)
                SecondaryValue = hashIdResult.Value,      // qrCode2 (always generated hash)
                SourceField = originalIdResult.SourceField
            };
        }

        /// <summary>
        /// Find original system-level ID fields (not personal documents)
        /// Looks for database/system IDs like uid, DATAID, recordId, etc.
        /// </summary>
        /// <param name="record">Record to search</param>
        /// <returns>Original ID if found, empty string if not found</returns>
        public static FieldMappingResult FindOriginalSystemId(Dictionary<string, object> record)
        {
            // System-level ID patterns (NOT personal documents)
            string[] systemIdPatterns = {
                "uid", "UID", "Id", "ID", "id",
                "recordId", "RecordId", "record_id", "RECORD_ID",
                "uniqueId", "UniqueId", "unique_id", "UNIQUE_ID",
                "entryId", "EntryId", "entry_id", "ENTRY_ID",
                "listId", "ListId", "list_id", "LIST_ID",
                "DATAID", "dataId", "DataId",        // UNSCR-specific
                "ssid", "SSID"                       // Other system IDs
            };

            foreach (var pattern in systemIdPatterns)
            {
                var match = record.FirstOrDefault(kv =>
                    kv.Key.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrEmpty(match.Value?.ToString()))
                {
                    FileLogger.LogMessage($"🆔 Found original system ID: {match.Value} from field: {match.Key}");
                    return new FieldMappingResult
                    {
                        Value = match.Value.ToString().Trim(),
                        SourceField = match.Key
                    };
                }
            }

            FileLogger.LogMessage("🆔 No original system ID found - leaving qrCode blank");
            return new FieldMappingResult
            {
                Value = "",  // Empty if no system ID found
                SourceField = "No original system ID found"
            };
        }

        /// <summary>
        /// Generate hash-based ID from entire record content
        /// This ensures every record has a unique identifier (qrCode2)
        /// </summary>
        /// <param name="record">Record to hash</param>
        /// <param name="fileName">Source file name</param>
        /// <returns>Generated MD5 hash ID</returns>
        public static FieldMappingResult GenerateHashFromRecordContent(Dictionary<string, object> record, string fileName)
        {
            try
            {
                // Create content string from all record fields
                var recordContent = new List<string>();

                // Add file source for uniqueness across different files
                recordContent.Add($"SOURCE:{Path.GetFileNameWithoutExtension(fileName)}");

                // Add all field values in sorted order for consistency
                foreach (var kvp in record.OrderBy(x => x.Key))
                {
                    if (kvp.Value != null && !string.IsNullOrWhiteSpace(kvp.Value.ToString()))
                    {
                        recordContent.Add($"{kvp.Key}:{kvp.Value}");
                    }
                }

                // Generate hash from combined content
                string contentString = string.Join("|", recordContent);
                string hashId = ComputeMD5Hash(contentString);

                FileLogger.LogMessage($"🆔 Generated record content hash: {hashId}");

                return new FieldMappingResult
                {
                    Value = hashId,
                    SourceField = "Generated from record content"
                };
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"❌ Error generating hash from record content: {ex.Message}");

                // Fallback to GUID if hash generation fails
                string fallbackId = Guid.NewGuid().ToString("N").ToUpper();
                FileLogger.LogMessage($"🔄 Using fallback GUID: {fallbackId}");

                return new FieldMappingResult
                {
                    Value = fallbackId,
                    SourceField = "Generated GUID (hash failed)"
                };
            }
        }

        /// <summary>
        /// Alternative method to generate hash from raw XML content
        /// Used when raw XML is available in the record
        /// </summary>
        /// <param name="rawXml">Raw XML string</param>
        /// <param name="fileName">Source file name</param>
        /// <returns>Generated hash from XML content</returns>
        public static FieldMappingResult GenerateHashFromRawXml(string rawXml, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(rawXml))
                {
                    FileLogger.LogError($"❌ Empty raw XML for hash generation from {fileName}");
                    return new FieldMappingResult
                    {
                        Value = Guid.NewGuid().ToString("N").ToUpper(),
                        SourceField = "Generated GUID (empty XML)"
                    };
                }

                // Create hash input: filename + raw XML
                string hashInput = $"SOURCE:{Path.GetFileNameWithoutExtension(fileName)}|{rawXml}";
                string hashId = ComputeMD5Hash(hashInput);

                FileLogger.LogMessage($"🆔 Generated raw XML hash: {hashId}");

                return new FieldMappingResult
                {
                    Value = hashId,
                    SourceField = "Generated from raw XML"
                };
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"❌ Error generating hash from raw XML: {ex.Message}");

                // Fallback to GUID if hash generation fails
                return new FieldMappingResult
                {
                    Value = Guid.NewGuid().ToString("N").ToUpper(),
                    SourceField = "Generated GUID (XML hash failed)"
                };
            }
        }

        /// <summary>
        /// Compute MD5 hash from input string
        /// </summary>
        /// <param name="input">String to hash</param>
        /// <returns>MD5 hash as uppercase hex string</returns>
        public static string ComputeMD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        /// <summary>
        /// Helper method to find original ID only (for qrCode field specifically)
        /// Does not generate hash - returns empty if no system ID found
        /// </summary>
        /// <param name="record">Record to search</param>
        /// <returns>Original system ID or empty string</returns>
        public static FieldMappingResult FindOriginalIdOnly(Dictionary<string, object> record)
        {
            return FindOriginalSystemId(record);
        }
    }
}