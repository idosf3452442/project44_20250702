using System; //last edited on 20250701-002 - Added IdentifierExtractor Integration
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Universal File Parser - Smart Mapping (Multi-File Edition) - ENHANCED");
            Console.WriteLine("====================================================================");

            // MULTIPLE FILES CONFIGURATION
            string folderpath20250617 = @"D:\Faizan\FPM\2022-12-19\project22_import_export_redesign_20230123\6_downloaded_files\pack010158\input\small";
            string outputFolderPath = folderpath20250617.Replace("input", "output");

            // SETUP LOGGING
            FileLogger.SetupLogging(outputFolderPath);

            FileLogger.LogMessage("Universal File Parser - Smart Mapping (Multi-File Edition) - ENHANCED");
            FileLogger.LogMessage("====================================================================");

            // Define multiple files to process
            string[] fileNames = {
                "20250617_011626_unscr_consolidated - Copy.xml",
                "20250617_011241_UKConList.xml",
                "20250613_010003_sdn.xml",
                "20250216_020802_BL_Fin_San_List.xml",
                "20250617_013815_OFAC_NON_SDN.xml",
                "20250617_013621_CA_PSTEL.xml",
                "converted_Nacta_Denotified_20250617_013150.xml",
                "20250617_012742_internationalgcCA.xml",
                "20250617_012119_eu-consolidated-list.xml",
                "20250617_011626_unscr_consolidated.xml",
                "converted_Nacta_20250616_172537.xml",
                "ProscribedPersons -  - Wednesday, June 11, 2025 2_52_43 PM.xml",
                "Notifications 09 APR 2021 - Sheet2.csv",
            };

            int successCount = 0;
            int failCount = 0;
            var failedFiles = new List<string>();

            // Process each file
            for (int i = 0; i < fileNames.Length; i++)
            {
                string currentFileName = fileNames[i];
                string fullFilePath = Path.Combine(folderpath20250617, currentFileName);

                FileLogger.LogMessage($"\n{'=',-60}");
                FileLogger.LogMessage($"🔄 PROCESSING FILE {i + 1}/{fileNames.Length}: {currentFileName}");
                FileLogger.LogMessage($"{'=',-60}");

                try
                {
                    if (!File.Exists(fullFilePath))
                    {
                        FileLogger.LogError($"File not found: {currentFileName}");
                        failCount++;
                        failedFiles.Add($"{currentFileName} (File not found)");
                        continue;
                    }

                    FileLogger.LogSuccess($"File found: {currentFileName}");

                    // *** MAIN CHANGE: Use DataParser instead of local method ***
                    var discoveredRecords = DataParser.ParseAnyFile(fullFilePath);

                    if (discoveredRecords.Count > 0)
                    {
                        ShowDiscoveredStructure(discoveredRecords, currentFileName);
                        FileLogger.LogSmartMappingAnalysis(discoveredRecords, currentFileName);
                        GenerateJsonOutput(discoveredRecords, outputFolderPath, currentFileName);
                        successCount++;
                        FileLogger.LogSuccess($"Successfully processed: {currentFileName}");
                    }
                    else
                    {
                        FileLogger.LogError($"No records found in: {currentFileName}");
                        failCount++;
                        failedFiles.Add($"{currentFileName} (No records found)");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.LogError($"Error processing {currentFileName}: {ex.Message}");
                    failCount++;
                    failedFiles.Add($"{currentFileName} (Error: {ex.Message})");
                }
            }

            // Final summary
            FileLogger.LogMessage($"\n{'=',-60}");
            FileLogger.LogMessage("📊 PROCESSING SUMMARY");
            FileLogger.LogMessage($"{'=',-60}");
            FileLogger.LogMessage($"✅ Successfully processed: {successCount} files");
            FileLogger.LogMessage($"❌ Failed to process: {failCount} files");
            FileLogger.LogMessage($"📄 Total files attempted: {fileNames.Length}");

            if (failedFiles.Count > 0)
            {
                FileLogger.LogMessage("\n❌ FAILED FILES:");
                foreach (var failedFile in failedFiles)
                {
                    FileLogger.LogMessage($"  • {failedFile}");
                }
            }

            if (successCount > 0)
            {
                FileLogger.LogMessage($"\n🎯 All output files saved to: {outputFolderPath}");
            }

            FileLogger.LogMessage($"\n📝 Complete log saved to: {FileLogger.GetLogFilePath()}");
            FileLogger.LogMessage("\nPress any key to exit...");

            // CLEANUP LOGGING
            FileLogger.CloseLogging();

            Console.ReadKey();
        }

        // ========================================================================
        // DISPLAY/DEBUG METHODS (staying in Program.cs)
        // ========================================================================

        private static void ShowDiscoveredStructure(List<Dictionary<string, object>> records, string fileName)
        {
            FileLogger.LogMessage($"\n=== DISCOVERED STRUCTURE: {fileName} ===");
            FileLogger.LogMessage($"📊 Total records found: {records.Count}");

            if (records.Any())
            {
                var firstRecord = records.First();
                FileLogger.LogMessage($"🔍 Fields in first record: {firstRecord.Count}");

                foreach (var field in firstRecord.Take(10))
                {
                    var value = field.Value?.ToString();
                    var truncatedValue = value?.Length > 50 ? value.Substring(0, 50) + "..." : value;
                    FileLogger.LogMessage($"  📋 {field.Key}: {truncatedValue}");
                }

                if (firstRecord.Count > 10)
                {
                    FileLogger.LogMessage($"  ... and {firstRecord.Count - 10} more fields");
                }
            }
        }

        // UPDATED GenerateJsonOutput METHOD with IdentifierExtractor - 20250701-002
        public static void GenerateJsonOutput(List<Dictionary<string, object>> records, string outputFolderPath, string originalFileName)
        {
            try
            {
                FileLogger.LogProgress($"🔧 Starting JSON generation for {records.Count} records...");

                var profiles = new List<object>();

                foreach (var record in records)
                {
                    // EXISTING EXTRACTIONS (unchanged)
                    var nameResult = NameExtractor.ExtractNameComponents(record);
                    var fatherResult = FatherNameExtractor.FindFieldForFatherName(record);
                    var recordTypeResult = RecordTypeExtractor.FindFieldForRecordType(record);
                    var recordIdResult = RecordIdExtractor.FindOrGenerateRecordId(record, originalFileName);

                    // NEW: Address extraction (only addition)
                    var addressResult = AddressExtractor.ExtractAddressComponents(record);
                    var aliasResult = AliasExtractor.ExtractAliases(record);

                    // *** NEW: IDENTIFIER EXTRACTION - 20250701-002 ***
                    var identifierResults = IdentifierExtractor.ExtractIdentifiers(record);

                    var dateResult = DateExtractor.ExtractDates(record);

                    // CREATE ADDRESSES ARRAY - only if address data found
                    object[] addressesArray;
                    if (!string.IsNullOrEmpty(addressResult.FullAddress) ||
                        !string.IsNullOrEmpty(addressResult.City) ||
                        !string.IsNullOrEmpty(addressResult.Country))
                    {
                        // Include address data if found
                        addressesArray = new object[] {
                            new {
                                addressType = addressResult.AddressType,
                                line1 = addressResult.Address1,
                                line2 = addressResult.Address2,
                                postcode = addressResult.PostalCode,
                                districtTown = "",
                                city = addressResult.City,
                                county = addressResult.StateOrProvince,
                                countyAbbrev = "",
                                country = addressResult.Country,
                                countryIsoCode = ""
                            }
                        };
                    }
                    else
                    {
                        // Empty array if no address data
                        addressesArray = new object[0];
                    }

                    // ORIGINAL PROFILE STRUCTURE (exactly as before, only identifiers modified)
                    var profile = new
                    {
                        notes = "",
                        firstName = nameResult.FirstName ?? "",
                        middleName = nameResult.MiddleName ?? "",
                        lastName = nameResult.LastName ?? "",
                        fullName = nameResult.FullName ?? "",
                        fatherHusbandName = fatherResult.Value ?? "",
                        recordType = recordTypeResult.Value ?? "INDIVIDUAL",
                        gender = "",
                        qrCode = recordIdResult.Value ?? "",
                        qrCode2 = recordIdResult.SecondaryValue ?? "",
                        resourceUri = "",
                        resourceId = "",
                        isDeleted = false,
                        isDeceased = false,

                        aliases = aliasResult.Aliases.Select(alias => new
                        {
                            fullName = alias.FullName,
                            firstName = alias.FirstName,
                            middleName = alias.MiddleName,
                            lastName = alias.LastName,
                            aliasType = alias.AliasType,
                            category = alias.Category,
                            sourceField = alias.SourceField
                        }).ToArray(),
                        addresses = addressesArray,  // ONLY CHANGE: now populated from extraction
                        contactEntries = new object[0],
                        identifiers = identifierResults.Select(id => new
                        {
                            category = id.Category,
                            value = id.Value
                        }).ToArray(), // *** UPDATED: Now populated with extracted identifiers - 20250701-002 ***
                        individualLinks = new object[0],
                        businessLinks = new object[0],

                        // ORIGINAL EVIDENCES (unchanged)
                        evidences = new[]
                        {
                            new
                            {
                                originalUrl = "",
                                title = $"Parsed from {originalFileName}",
                                credibility = "",
                                language = "",
                                summary = $"Record extracted from {originalFileName}",
                                datasets = new[] { Path.GetFileNameWithoutExtension(originalFileName) }
                            }
                        },
                        birthDates = dateResult.BirthDates.Select(bd => new {
                            date = bd.Date,
                            type = bd.Type,
                            year = bd.Year,
                            isMainEntry = bd.IsMainEntry
                        }),
                        deathDate = dateResult.DeathDate != null ? new
                        {
                            date = dateResult.DeathDate.Date,
                            type = dateResult.DeathDate.Type,
                            location = dateResult.DeathDate.Location
                        } : null,
                        listingDates = dateResult.ListingDates.Select(ld => new {
                            listedOn = ld.ListedOn,
                            lastUpdated = ld.LastUpdated,
                            effectiveDate = ld.EffectiveDate
                        }),
                    };

                    profiles.Add(profile);
                }

                // ORIGINAL JSON OUTPUT STRUCTURE (unchanged)
                var jsonOutput = new
                {
                    profiles = profiles,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                // ORIGINAL FILE WRITING (unchanged)
                string jsonFileName = Path.GetFileNameWithoutExtension(originalFileName) + ".json";
                string jsonFilePath = Path.Combine(outputFolderPath, jsonFileName);

                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include
                };

                string jsonString = JsonConvert.SerializeObject(jsonOutput, jsonSettings);
                File.WriteAllText(jsonFilePath, jsonString);

                FileLogger.LogSuccess($"Generated JSON output: {jsonFileName} ({profiles.Count} profiles)");

                // MOVED TO FileLogger.cs - call the logging method
                FileLogger.LogAddressExtractionSummary(records, originalFileName);
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error generating JSON output: {ex.Message}");
            }
        }
    }
}