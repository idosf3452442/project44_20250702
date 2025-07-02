using System; // DateExtractor.cs - Universal Date Extraction System
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConsoleApp1
{
    /// <summary>
    /// Universal date extraction system supporting multiple sanctions list formats
    /// Handles birth dates, death dates, listing dates, effective dates
    /// </summary>
    public static class DateExtractor
    {
        /// <summary>
        /// Main entry point for date extraction
        /// </summary>
        /// <param name="record">Record dictionary with all field data</param>
        /// <returns>DateExtractionResult with all extracted dates</returns>
        public static DateExtractionResult ExtractDates(Dictionary<string, object> record)
        {
            var result = new DateExtractionResult
            {
                BirthDates = new List<DateInfo>(),
                DeathDate = null,
                ListingDates = new List<ListingDateInfo>(),
                SourceFields = new List<string>()
            };

            try
            {
                FileLogger.LogProgress("📅 Starting date extraction...");

                // Extract birth dates
                ExtractBirthDates(record, result);

                // Extract death dates
                ExtractDeathDates(record, result);

                // Extract listing/designation dates
                ExtractListingDates(record, result);

                FileLogger.LogSuccess($"📅 Date extraction completed - Found {result.BirthDates.Count} birth dates, " +
                    $"{(result.DeathDate != null ? 1 : 0)} death date, {result.ListingDates.Count} listing dates");

                return result;
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error in date extraction: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Extract birth dates from multiple patterns
        /// </summary>
        private static void ExtractBirthDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            // PATTERN 1: OFAC dateOfBirth patterns
            ExtractOFACBirthDates(record, result);

            // PATTERN 2: UN INDIVIDUAL_DATE_OF_BIRTH patterns
            ExtractUNBirthDates(record, result);

            // PATTERN 3: EU birthdate patterns
            ExtractEUBirthDates(record, result);

            // PATTERN 4: Generic birth date fields
            ExtractGenericBirthDates(record, result);
        }

        /// <summary>
        /// Extract OFAC birth date patterns
        /// </summary>
        private static void ExtractOFACBirthDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            // OFAC: dateOfBirthList_dateOfBirthItem_dateOfBirth
            var ofacDateField = FindDateField(record, new[] {
                "dateOfBirthList_dateOfBirthItem_dateOfBirth",
                "dateOfBirth",
                "Individual_DateOfBirth"
            });

            if (!string.IsNullOrEmpty(ofacDateField.Value))
            {
                var dateInfo = ParseDateValue(ofacDateField.Value, "EXACT", ofacDateField.SourceField);
                if (dateInfo != null)
                {
                    // Check if it's main entry
                    var mainEntryField = FindField(record, new[] { "dateOfBirthList_dateOfBirthItem_mainEntry" });
                    dateInfo.IsMainEntry = mainEntryField.Value?.ToLower() == "true";

                    result.BirthDates.Add(dateInfo);
                    result.SourceFields.Add(ofacDateField.SourceField);
                }
            }
        }

        /// <summary>
        /// Extract UN birth date patterns
        /// </summary>
        private static void ExtractUNBirthDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            // UN patterns: INDIVIDUAL_DATE_OF_BIRTH with TYPE_OF_DATE and YEAR/DATE
            var typeField = FindField(record, new[] { "INDIVIDUAL_DATE_OF_BIRTH_TYPE_OF_DATE", "TYPE_OF_DATE" });
            var yearField = FindField(record, new[] { "INDIVIDUAL_DATE_OF_BIRTH_YEAR", "YEAR" });
            var dateField = FindField(record, new[] { "INDIVIDUAL_DATE_OF_BIRTH_DATE", "DATE" });

            string dateType = typeField.Value ?? "UNKNOWN";

            if (!string.IsNullOrEmpty(yearField.Value))
            {
                var dateInfo = new DateInfo
                {
                    Year = yearField.Value,
                    Type = dateType,
                    SourceField = $"{typeField.SourceField}, {yearField.SourceField}",
                    IsMainEntry = true
                };

                if (!string.IsNullOrEmpty(dateField.Value))
                {
                    dateInfo.Date = dateField.Value;
                    dateInfo.SourceField += $", {dateField.SourceField}";
                }

                result.BirthDates.Add(dateInfo);
                result.SourceFields.Add(dateInfo.SourceField);
            }
        }

        /// <summary>
        /// Extract EU birth date patterns
        /// </summary>
        private static void ExtractEUBirthDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            var euBirthField = FindField(record, new[] { "birthdate_remark", "birthdate" });

            if (!string.IsNullOrEmpty(euBirthField.Value))
            {
                var dateInfo = ParseDateValue(euBirthField.Value, "REMARK", euBirthField.SourceField);
                if (dateInfo != null)
                {
                    result.BirthDates.Add(dateInfo);
                    result.SourceFields.Add(euBirthField.SourceField);
                }
            }
        }

        /// <summary>
        /// Extract generic birth date patterns
        /// </summary>
        private static void ExtractGenericBirthDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            var genericFields = new[] {
                "birthDate", "birth_date", "dob", "date_of_birth",
                "Individual_DateOfBirth", "Birth_Date"
            };

            foreach (var fieldPattern in genericFields)
            {
                var field = FindField(record, new[] { fieldPattern });
                if (!string.IsNullOrEmpty(field.Value))
                {
                    var dateInfo = ParseDateValue(field.Value, "EXACT", field.SourceField);
                    if (dateInfo != null)
                    {
                        result.BirthDates.Add(dateInfo);
                        result.SourceFields.Add(field.SourceField);
                        break; // Take first match for generic patterns
                    }
                }
            }
        }

        /// <summary>
        /// Extract death dates from remarks and comments
        /// </summary>
        private static void ExtractDeathDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            var remarkFields = new[] {
                "remark", "remarks", "COMMENTS1", "comments",
                "identification_remark", "address_remark"
            };

            foreach (var fieldPattern in remarkFields)
            {
                var field = FindField(record, new[] { fieldPattern });
                if (!string.IsNullOrEmpty(field.Value))
                {
                    var deathInfo = ExtractDeathFromRemark(field.Value, field.SourceField);
                    if (deathInfo != null)
                    {
                        result.DeathDate = deathInfo;
                        result.SourceFields.Add(field.SourceField);
                        break; // Take first death date found
                    }
                }
            }
        }

        /// <summary>
        /// Extract death information from remark text
        /// </summary>
        private static DeathInfo ExtractDeathFromRemark(string remark, string sourceField)
        {
            if (string.IsNullOrEmpty(remark)) return null;

            var lowerRemark = remark.ToLower();

            // Look for death indicators
            var deathPatterns = new[] {
                @"died?\s+in\s+([^.]+)",
                @"confirmed\s+to\s+have\s+died\s+in\s+([^.]+)",
                @"death\s+in\s+([^.]+)",
                @"deceased\s+in\s+([^.]+)"
            };

            foreach (var pattern in deathPatterns)
            {
                var match = Regex.Match(lowerRemark, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var deathText = match.Groups[1].Value.Trim();

                    return new DeathInfo
                    {
                        Date = ExtractDateFromText(deathText),
                        Type = "CONFIRMED",
                        Location = ExtractLocationFromText(deathText),
                        SourceField = sourceField
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Extract listing and designation dates
        /// </summary>
        private static void ExtractListingDates(Dictionary<string, object> record, DateExtractionResult result)
        {
            var listingInfo = new ListingDateInfo
            {
                SourceFields = new List<string>()
            };

            // Listed on date
            var listedOnField = FindField(record, new[] {
                "LISTED_ON", "listed_on", "DateListed", "date_listed"
            });
            if (!string.IsNullOrEmpty(listedOnField.Value))
            {
                listingInfo.ListedOn = listedOnField.Value;
                listingInfo.SourceFields.Add(listedOnField.SourceField);
            }

            // Last updated dates
            var lastUpdatedField = FindField(record, new[] {
                "LAST_DAY_UPDATED", "last_updated", "LastUpdated", "DateDesignated"
            });
            if (!string.IsNullOrEmpty(lastUpdatedField.Value))
            {
                listingInfo.LastUpdated = ParseMultipleDates(lastUpdatedField.Value);
                listingInfo.SourceFields.Add(lastUpdatedField.SourceField);
            }

            // Effective dates from directives
            var effectiveDateField = FindField(record, new[] {
                "Effective Date (EO 14024 Directive 3):",
                "effective_date", "effectiveDate"
            });
            if (!string.IsNullOrEmpty(effectiveDateField.Value))
            {
                listingInfo.EffectiveDate = effectiveDateField.Value;
                listingInfo.SourceFields.Add(effectiveDateField.SourceField);
            }

            if (listingInfo.SourceFields.Count > 0)
            {
                result.ListingDates.Add(listingInfo);
                result.SourceFields.AddRange(listingInfo.SourceFields);
            }
        }

        /// <summary>
        /// Parse date value into DateInfo object
        /// </summary>
        private static DateInfo ParseDateValue(string dateValue, string defaultType, string sourceField)
        {
            if (string.IsNullOrEmpty(dateValue)) return null;

            return new DateInfo
            {
                Date = dateValue,
                Type = defaultType,
                Year = ExtractYearFromDate(dateValue),
                SourceField = sourceField,
                IsMainEntry = false
            };
        }

        /// <summary>
        /// Extract year from date string
        /// </summary>
        private static string ExtractYearFromDate(string dateValue)
        {
            if (string.IsNullOrEmpty(dateValue)) return "";

            // Try to extract 4-digit year
            var yearMatch = Regex.Match(dateValue, @"\b(19|20)\d{2}\b");
            return yearMatch.Success ? yearMatch.Value : "";
        }

        /// <summary>
        /// Extract date from text description
        /// </summary>
        private static string ExtractDateFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Look for date patterns in text
            var datePatterns = new[] {
                @"\b(january|february|march|april|may|june|july|august|september|october|november|december)\s+\d{4}\b",
                @"\b\d{4}\b",
                @"\b\d{1,2}[-/]\d{1,2}[-/]\d{4}\b"
            };

            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value.Trim();
                }
            }

            return "";
        }

        /// <summary>
        /// Extract location from text description
        /// </summary>
        private static string ExtractLocationFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Remove date information and extract remaining location
            var cleanText = Regex.Replace(text, @"\b(january|february|march|april|may|june|july|august|september|october|november|december)\s+\d{4}\b", "", RegexOptions.IgnoreCase);
            cleanText = Regex.Replace(cleanText, @"\b\d{4}\b", "");
            cleanText = Regex.Replace(cleanText, @"\b\d{1,2}[-/]\d{1,2}[-/]\d{4}\b", "");

            return cleanText.Trim();
        }

        /// <summary>
        /// Parse multiple dates from a value (e.g., comma-separated)
        /// </summary>
        private static List<string> ParseMultipleDates(string dateValue)
        {
            if (string.IsNullOrEmpty(dateValue)) return new List<string>();

            // Split by common separators and clean up
            var dates = dateValue.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(d => d.Trim())
                                 .Where(d => !string.IsNullOrEmpty(d))
                                 .ToList();

            return dates.Count > 0 ? dates : new List<string> { dateValue };
        }

        /// <summary>
        /// Find date field using multiple patterns
        /// </summary>
        private static FieldMappingResult FindDateField(Dictionary<string, object> record, string[] patterns)
        {
            return FindField(record, patterns);
        }

        /// <summary>
        /// Generic field finder
        /// </summary>
        private static FieldMappingResult FindField(Dictionary<string, object> record, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                // Exact match
                if (record.ContainsKey(pattern) && !string.IsNullOrEmpty(record[pattern]?.ToString()))
                {
                    return new FieldMappingResult
                    {
                        Value = record[pattern].ToString().Trim(),
                        SourceField = pattern
                    };
                }

                // Partial match
                var matchingKey = record.Keys.FirstOrDefault(key =>
                    key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchingKey != null && !string.IsNullOrEmpty(record[matchingKey]?.ToString()))
                {
                    return new FieldMappingResult
                    {
                        Value = record[matchingKey].ToString().Trim(),
                        SourceField = matchingKey
                    };
                }
            }

            return new FieldMappingResult { Value = "", SourceField = "No source found" };
        }

        /// <summary>
        /// Log detailed date extraction results for debugging
        /// </summary>
        public static void LogDateExtractionDetails(DateExtractionResult result, string fileName)
        {
            FileLogger.LogMessage($"\n📅 DATE EXTRACTION DETAILS: {fileName}");

            FileLogger.LogMessage($"   Birth Dates: {result.BirthDates.Count}");
            foreach (var birthDate in result.BirthDates.Take(3))
            {
                FileLogger.LogMessage($"     Date: '{birthDate.Date}' | Type: {birthDate.Type} | Year: {birthDate.Year}");
            }

            if (result.DeathDate != null)
            {
                FileLogger.LogMessage($"   Death Date: '{result.DeathDate.Date}' | Location: {result.DeathDate.Location}");
            }

            FileLogger.LogMessage($"   Listing Dates: {result.ListingDates.Count}");
            foreach (var listingDate in result.ListingDates.Take(2))
            {
                FileLogger.LogMessage($"     Listed: '{listingDate.ListedOn}' | Updates: {listingDate.LastUpdated?.Count ?? 0}");
            }
        }
    }

    // ========================================================================
    // DATA MODELS FOR DATE EXTRACTOR ONLY
    // ========================================================================

    /// <summary>
    /// Date information structure
    /// </summary>
    public class DateInfo
    {
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";         // EXACT, APPROXIMATELY, REMARK
        public string Year { get; set; } = "";
        public bool IsMainEntry { get; set; } = false;
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Death information structure
    /// </summary>
    public class DeathInfo
    {
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";         // CONFIRMED, REPORTED
        public string Location { get; set; } = "";
        public string SourceField { get; set; } = "";
    }

    /// <summary>
    /// Listing date information structure
    /// </summary>
    public class ListingDateInfo
    {
        public string ListedOn { get; set; } = "";
        public List<string> LastUpdated { get; set; } = new List<string>();
        public string EffectiveDate { get; set; } = "";
        public List<string> SourceFields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result structure for date extraction
    /// </summary>
    public class DateExtractionResult
    {
        public List<DateInfo> BirthDates { get; set; } = new List<DateInfo>();
        public DeathInfo DeathDate { get; set; } = null;
        public List<ListingDateInfo> ListingDates { get; set; } = new List<ListingDateInfo>();
        public List<string> SourceFields { get; set; } = new List<string>();
    }
}