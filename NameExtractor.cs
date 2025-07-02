using System;//last edited on 20250630-003 - FIXED: No longer breaks full names into components
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>
    /// Pattern-agnostic name extraction with support for multiple data sources
    /// CORRECTED: Does NOT break full names into components when only full name is available
    /// </summary>
    public static class NameExtractor
    {
        /// <summary>
        /// Main entry point for name extraction - tries multiple strategies
        /// STRATEGY: Only populate individual components IF individual fields exist
        /// If only full name exists, keep it as full name only
        /// </summary>
        /// <param name="record">Record dictionary with field data</param>
        /// <returns>NameExtractionResult with extracted name components</returns>
        public static NameExtractionResult ExtractNameComponents(Dictionary<string, object> record)
        {
            // TIER 1: Look for individual name components first (highest priority)
            var firstNameField = FindIndividualNameField(record, new[] {
                "firstName", "first_name", "FirstName", "fname", "givenName", "FIRST_NAME", "given_name"
            });
            var middleNameField = FindIndividualNameField(record, new[] {
                "middleName", "middle_name", "MiddleName", "mname", "additionalName", "MIDDLE_NAME", "middleInitial"
            });
            var lastNameField = FindIndividualNameField(record, new[] {
                "lastName", "last_name", "LastName", "lname", "familyName", "surname", "LAST_NAME", "family_name"
            });

            // TIER 1A: Enhanced UNSCR multi-part name detection (SECOND_NAME, THIRD_NAME, FOURTH_NAME)
            var secondNameField = FindIndividualNameField(record, new[] { "SECOND_NAME", "second_name" });
            var thirdNameField = FindIndividualNameField(record, new[] { "THIRD_NAME", "third_name" });
            var fourthNameField = FindIndividualNameField(record, new[] { "FOURTH_NAME", "fourth_name" });

            // PRIORITY 1: UNSCR multi-field pattern (FIRST_NAME + SECOND_NAME + etc.)
            if (!string.IsNullOrEmpty(firstNameField.Value) &&
                (!string.IsNullOrEmpty(secondNameField.Value) || !string.IsNullOrEmpty(thirdNameField.Value) || !string.IsNullOrEmpty(fourthNameField.Value)))
            {
                return ProcessUNSCRMultiFields(firstNameField, secondNameField, thirdNameField, fourthNameField);
            }

            // PRIORITY 2: Standard individual fields (firstName, middleName, lastName)
            if (!string.IsNullOrEmpty(firstNameField.Value) || !string.IsNullOrEmpty(lastNameField.Value))
            {
                return ProcessIndividualFields(firstNameField, middleNameField, lastNameField);
            }

            // PRIORITY 3: Full name field - DO NOT BREAK INTO COMPONENTS
            var fullNameField = FindIndividualNameField(record, new[] {
                "fullName", "full_name", "FullName", "Name", "name", "PersonName", "person_name",
                "Individual", "wholeName", "displayName", "completeName"
            });

            if (!string.IsNullOrEmpty(fullNameField.Value))
            {
                return ProcessFullNameFieldCorrectly(fullNameField);
            }

            // PRIORITY 4: UNSCR INDIVIDUAL_*_NAME pattern fields
            var unscrResult = ProcessUNSCRPatternFields(record);
            if (!string.IsNullOrEmpty(unscrResult.FullName))
            {
                return unscrResult;
            }

            // PRIORITY 5: Fallback - no name found
            return new NameExtractionResult
            {
                FirstName = "",
                MiddleName = "",
                LastName = "",
                FullName = "NAME MISSING",
                SourceType = "NOT_FOUND",
                SourceFields = "No name fields detected"
            };
        }

        /// <summary>
        /// CORRECTED: Process full name field WITHOUT breaking into components
        /// Our decision: Keep full name as full name, leave individual components empty
        /// </summary>
        private static NameExtractionResult ProcessFullNameFieldCorrectly(FieldMappingResult fullNameResult)
        {
            return new NameExtractionResult
            {
                FirstName = "",           // ✅ LEAVE EMPTY - don't break full name
                MiddleName = "",          // ✅ LEAVE EMPTY - don't break full name  
                LastName = "",            // ✅ LEAVE EMPTY - don't break full name
                FullName = fullNameResult.Value,  // ✅ KEEP FULL NAME INTACT
                SourceType = "FULL_NAME_FIELD",
                SourceFields = fullNameResult.SourceField
            };
        }

        /// <summary>
        /// Process individual name fields (firstName, middleName, lastName)
        /// </summary>
        private static NameExtractionResult ProcessIndividualFields(FieldMappingResult firstNameField,
            FieldMappingResult middleNameField, FieldMappingResult lastNameField)
        {
            string combinedFullName = CombineNameParts(firstNameField.Value, middleNameField.Value, lastNameField.Value);

            return new NameExtractionResult
            {
                FirstName = firstNameField.Value ?? "",
                MiddleName = middleNameField.Value ?? "",
                LastName = lastNameField.Value ?? "",
                FullName = combinedFullName,
                SourceType = "INDIVIDUAL_FIELDS",
                SourceFields = $"{firstNameField.SourceField}, {middleNameField.SourceField}, {lastNameField.SourceField}"
            };
        }

        /// <summary>
        /// Process UNSCR multi-field pattern (FIRST_NAME, SECOND_NAME, THIRD_NAME, FOURTH_NAME)
        /// </summary>
        private static NameExtractionResult ProcessUNSCRMultiFields(FieldMappingResult firstNameField,
            FieldMappingResult secondNameField, FieldMappingResult thirdNameField, FieldMappingResult fourthNameField)
        {
            var nameParts = new List<string>();
            if (!string.IsNullOrEmpty(firstNameField.Value)) nameParts.Add(firstNameField.Value);
            if (!string.IsNullOrEmpty(secondNameField.Value)) nameParts.Add(secondNameField.Value);
            if (!string.IsNullOrEmpty(thirdNameField.Value)) nameParts.Add(thirdNameField.Value);
            if (!string.IsNullOrEmpty(fourthNameField.Value)) nameParts.Add(fourthNameField.Value);

            string combinedFullName = string.Join(" ", nameParts);

            // For UNSCR: first part is firstName, last part is lastName, middle parts combine
            string firstName = firstNameField.Value ?? "";
            string lastName = !string.IsNullOrEmpty(fourthNameField.Value) ? fourthNameField.Value :
                             !string.IsNullOrEmpty(thirdNameField.Value) ? thirdNameField.Value :
                             secondNameField.Value ?? "";

            var middleParts = new List<string>();
            if (!string.IsNullOrEmpty(secondNameField.Value) && lastName != secondNameField.Value)
                middleParts.Add(secondNameField.Value);
            if (!string.IsNullOrEmpty(thirdNameField.Value) && lastName != thirdNameField.Value)
                middleParts.Add(thirdNameField.Value);
            string middleName = string.Join(" ", middleParts);

            return new NameExtractionResult
            {
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                FullName = combinedFullName,
                SourceType = "UNSCR_MULTI_FIELDS",
                SourceFields = $"{firstNameField.SourceField}, {secondNameField.SourceField}, {thirdNameField.SourceField}, {fourthNameField.SourceField}"
            };
        }

        /// <summary>
        /// Process UNSCR pattern fields (INDIVIDUAL_*_NAME style)
        /// </summary>
        private static NameExtractionResult ProcessUNSCRPatternFields(Dictionary<string, object> record)
        {
            var unscr_name_parts = record.Keys
                .Where(k => k.StartsWith("INDIVIDUAL_") && k.Contains("_NAME") && !k.Contains("ALIAS"))
                .Select(k => new { Key = k, Value = record[k]?.ToString()?.Trim() })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList();

            if (!unscr_name_parts.Any())
            {
                return new NameExtractionResult(); // Empty result
            }

            var result = new NameExtractionResult
            {
                FullName = string.Join(" ", unscr_name_parts.Select(x => x.Value)),
                SourceType = "UNSCR_PATTERN",
                SourceFields = string.Join(", ", unscr_name_parts.Select(x => x.Key))
            };

            // Try to map to standard components if possible
            var firstNamePart = unscr_name_parts.FirstOrDefault(x => x.Key.Contains("FIRST"));
            var lastNamePart = unscr_name_parts.FirstOrDefault(x => x.Key.Contains("LAST"));

            if (firstNamePart != null) result.FirstName = firstNamePart.Value;
            if (lastNamePart != null) result.LastName = lastNamePart.Value;

            return result;
        }

        /// <summary>
        /// Find individual name field using patterns
        /// </summary>
        private static FieldMappingResult FindIndividualNameField(Dictionary<string, object> record, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var match = record.FirstOrDefault(kv =>
                    kv.Key.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrWhiteSpace(match.Value?.ToString()))
                {
                    return new FieldMappingResult
                    {
                        Value = match.Value.ToString().Trim(),
                        SourceField = match.Key
                    };
                }
            }

            return new FieldMappingResult { Value = "", SourceField = "No source found" };
        }

        /// <summary>
        /// Combine name parts into full name (only when individual components exist)
        /// </summary>
        private static string CombineNameParts(string firstName, string middleName, string lastName)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(firstName)) parts.Add(firstName);
            if (!string.IsNullOrEmpty(middleName)) parts.Add(middleName);
            if (!string.IsNullOrEmpty(lastName)) parts.Add(lastName);

            return parts.Count > 0 ? string.Join(" ", parts) : "";
        }

        /// <summary>
        /// Log detailed name extraction information
        /// </summary>
        /// <param name="result">Name extraction result</param>
        /// <param name="fileName">Source file name</param>
        public static void LogNameExtractionDetails(NameExtractionResult result, string fileName)
        {
            FileLogger.LogMessage($"\n🔍 NAME EXTRACTION DETAILS: {fileName}");
            FileLogger.LogMessage($"   First: '{result.FirstName}' | Middle: '{result.MiddleName}' | Last: '{result.LastName}'");
            FileLogger.LogMessage($"   Full: '{result.FullName}'");
            FileLogger.LogMessage($"   Source Type: {result.SourceType}");
            FileLogger.LogMessage($"   Source Fields: {result.SourceFields}");
        }
    }
}