using System; //last edited on 20250701-002 - Added IdentifierExtractor Integration
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConsoleApp1
{
    public static class FileLogger
    {
        private static string logFilePath;
        private static StreamWriter logWriter;

        public static void SetupLogging(string outputFolderPath)
        {
            try
            {
                Directory.CreateDirectory(outputFolderPath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFileName = $"logOutput_{timestamp}.txt";
                logFilePath = Path.Combine(outputFolderPath, logFileName);
                logWriter = new StreamWriter(logFilePath, append: false);
                logWriter.AutoFlush = true;

                LogMessage($"Logging started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Log file: {logFileName}");
                LogMessage("");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error setting up logging: {ex.Message}");
                Console.WriteLine("Continuing without file logging...");
            }
        }

        public static void LogMessage(string message)
        {
            Console.WriteLine(message);
            try
            {
                logWriter?.WriteLine(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error writing to log file: {ex.Message}");
            }
        }

        public static void LogProgress(string message)
        {
            LogMessage($"🔄 {message}");
        }

        public static void LogSuccess(string message)
        {
            LogMessage($"✅ {message}");
        }

        public static void LogError(string message)
        {
            LogMessage($"❌ {message}");
        }

        public static void LogWarning(string message)
        {
            LogMessage($"⚠️ {message}");
        }

        public static void LogInfo(string message)
        {
            LogMessage($"ℹ️ {message}");
        }

        public static string GetLogFilePath()
        {
            return logFilePath ?? "";
        }

        public static void CloseLogging()
        {
            try
            {
                if (logWriter != null)
                {
                    LogMessage($"\nLogging ended at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    logWriter.Close();
                    logWriter.Dispose();
                    logWriter = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error closing log file: {ex.Message}");
            }
        }

        public static void LogSeparator(char character = '=', int length = 60)
        {
            LogMessage(new string(character, length));
        }

        public static void LogSectionHeader(string title)
        {
            LogSeparator();
            LogMessage(title);
            LogSeparator();
        }

        public static bool IsLoggingInitialized()
        {
            return logWriter != null && !string.IsNullOrEmpty(logFilePath);
        }

        /// <summary>
        /// Log comprehensive smart mapping analysis for debugging and verification
        /// UPDATED WITH IDENTIFIER EXTRACTION - 20250701-002
        /// </summary>
        /// <param name="records">Records to analyze</param>
        /// <param name="fileName">Source file name</param>
        public static void LogSmartMappingAnalysis(List<Dictionary<string, object>> records, string fileName)
        {
            if (records.Count == 0) return;

            LogMessage($"\n🧠 SMART FIELD MAPPING: {fileName}");

            // Take first record for mapping analysis
            var sampleRecord = records.First();

            // EXISTING EXTRACTIONS
            var nameResult = NameExtractor.ExtractNameComponents(sampleRecord);
            var fatherResult = FatherNameExtractor.FindFieldForFatherName(sampleRecord);
            var recordTypeResult = RecordTypeExtractor.FindFieldForRecordType(sampleRecord);
            var recordIdResult = RecordIdExtractor.FindOrGenerateRecordId(sampleRecord, fileName);

            // ADDRESS EXTRACTION
            var addressResult = AddressExtractor.ExtractAddressComponents(sampleRecord);
            var aliasResult = AliasExtractor.ExtractAliases(sampleRecord);
            var dateResult = DateExtractor.ExtractDates(sampleRecord);
            LogMessage($"📅 Dates: Birth={dateResult.BirthDates.Count} | Death={(dateResult.DeathDate != null ? "Found" : "None")} | Listing={dateResult.ListingDates.Count}");

            // Display mapping results
            LogMessage($"📝 Mapped fullName:      '{nameResult.FullName}'");
            LogMessage($"📝 Mapped firstName:     '{nameResult.FirstName}'");
            LogMessage($"📝 Mapped middleName:    '{nameResult.MiddleName}'");
            LogMessage($"📝 Mapped lastName:      '{nameResult.LastName}'");
            LogMessage($"👨 Mapped fatherName:    '{fatherResult.Value}'");
            LogMessage($"📋 Mapped recordType:    '{recordTypeResult.Value}'");
            LogMessage($"🆔 Mapped qrCode:        '{recordIdResult.Value}'");

            // ADDRESS MAPPING DISPLAY
            LogMessage($"🏠 Mapped address:       '{addressResult.FullAddress}'");
            LogMessage($"🏠 Mapped city:          '{addressResult.City}'");
            LogMessage($"🏠 Mapped country:       '{addressResult.Country}'");

            LogMessage($"\n🔗 MAPPING SOURCES:");
            LogMessage($"  Name extraction type: {nameResult.SourceType}");
            LogMessage($"  Name source fields:   {nameResult.SourceFields}");
            LogMessage($"  fatherName came from: {fatherResult.SourceField}");
            LogMessage($"  recordType came from: {recordTypeResult.SourceField}");
            LogMessage($"  qrCode came from:     {recordIdResult.SourceField}");

            // ADDRESS SOURCE DISPLAY
            LogMessage($"  address came from:    {addressResult.SourceFields}");
            LogMessage($"  address type:         {addressResult.AddressType}");
            //ALIASES
            LogMessage($"🏷️ Mapped aliases:       {aliasResult.Aliases.Count} aliases ({aliasResult.SourceType})");
            LogMessage($"  aliases came from:    {aliasResult.SourceFields}");

            // *** NEW ADDITION: IDENTIFIER EXTRACTION - 20250701-002 ***
            var identifierResults = IdentifierExtractor.ExtractIdentifiers(sampleRecord);
            LogMessage($"🆔 Identifiers: Found {identifierResults.Count} identifier(s)");
            foreach (var identifier in identifierResults)
            {
                LogMessage($"   {identifier.Category}: '{identifier.Value}' (from: {identifier.SourceField})");
            }
            if (identifierResults.Count == 0)
            {
                LogMessage($"   No identifiers detected");
            }
            // *** END NEW ADDITION ***

            // Log detailed extractions for debugging
            NameExtractor.LogNameExtractionDetails(nameResult, fileName);

            // Detailed address logging if address data found
            if (!string.IsNullOrEmpty(addressResult.FullAddress) ||
                !string.IsNullOrEmpty(addressResult.City))
            {
                LogDetailedAddressExtraction(addressResult, fileName, 0);
            }
            // *** NEW DETAILED LOGGING FOR DATE EXTRACTOR - 20250702-003 ***
            DateExtractor.LogDateExtractionDetails(dateResult, fileName);
        }

        /// <summary>
        /// Log detailed address extraction for debugging
        /// </summary>
        public static void LogDetailedAddressExtraction(AddressExtractionResult result, string fileName, int addressIndex)
        {
            LogMessage($"📍 Address {addressIndex + 1} Details:");
            LogMessage($"   Full='{result.FullAddress}' | Line1='{result.Address1}' | Line2='{result.Address2}'");
            LogMessage($"   City='{result.City}' | State='{result.StateOrProvince}' | Postal='{result.PostalCode}'");
            LogMessage($"   Type: '{result.AddressType}' | Sources: {result.SourceFields}");
        }

        /// <summary>
        /// Log alias extraction summary for debugging
        /// </summary>
        public static void LogAliasExtractionSummary(AliasExtractionResult result)
        {
            if (result.Aliases.Count > 0)
            {
                LogSuccess($"🏷️ Aliases extracted: {result.Aliases.Count} aliases using {result.SourceType}");
                LogMessage($"   Sources: {result.SourceFields}");

                foreach (var alias in result.Aliases.Take(3)) // Show first 3 aliases
                {
                    LogMessage($"   • {alias.FullName} ({alias.AliasType}, {alias.Category})");
                }

                if (result.Aliases.Count > 3)
                {
                    LogMessage($"   ... and {result.Aliases.Count - 3} more aliases");
                }
            }
            else
            {
                LogMessage($"🏷️ No aliases found ({result.SourceType})");
            }
        }

        /// <summary>
        /// Log summary of address extraction results
        /// </summary>
        /// <param name="records">Processed records</param>
        /// <param name="fileName">Source file name</param>
        public static void LogAddressExtractionSummary(List<Dictionary<string, object>> records, string fileName)
        {
            try
            {
                int recordsWithAddresses = 0;
                var addressTypes = new Dictionary<string, int>();

                foreach (var record in records)
                {
                    var addressResult = AddressExtractor.ExtractAddressComponents(record);

                    if (!string.IsNullOrEmpty(addressResult.FullAddress) ||
                        !string.IsNullOrEmpty(addressResult.City) ||
                        !string.IsNullOrEmpty(addressResult.Country))
                    {
                        recordsWithAddresses++;

                        // Track address types
                        string addrType = string.IsNullOrEmpty(addressResult.AddressType) ? "unknown" : addressResult.AddressType;
                        addressTypes[addrType] = addressTypes.ContainsKey(addrType) ? addressTypes[addrType] + 1 : 1;
                    }
                }

                LogMessage($"\n📊 ADDRESS EXTRACTION SUMMARY for {fileName}:");
                LogMessage($"   Records with addresses: {recordsWithAddresses}/{records.Count}");
                LogMessage($"   Address types found:");
                foreach (var addrType in addressTypes)
                {
                    LogMessage($"     {addrType.Key}: {addrType.Value}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in address extraction summary: {ex.Message}");
            }
        }
    }
}