using System; //last edited on 20250630-001
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>
    /// Pattern-agnostic father/husband name extraction
    /// Handles various cultural and source-specific patterns
    /// </summary>
    public static class FatherNameExtractor
    {
        /// <summary>
        /// Main entry point for father/husband name extraction
        /// </summary>
        /// <param name="record">Record dictionary with field data</param>
        /// <returns>FieldMappingResult with father/husband name or empty result</returns>
        public static FieldMappingResult FindFieldForFatherName(Dictionary<string, object> record)
        {
            FileLogger.LogProgress("🔍 Starting father/husband name extraction...");

            // TIER 1: Direct father name patterns
            var fatherResult = FindDirectFatherName(record);
            if (!string.IsNullOrEmpty(fatherResult.Value))
            {
                FileLogger.LogSuccess($"🎯 Father name found: '{fatherResult.Value}' from {fatherResult.SourceField}");
                return fatherResult;
            }

            // TIER 2: Husband name patterns
            var husbandResult = FindHusbandName(record);
            if (!string.IsNullOrEmpty(husbandResult.Value))
            {
                FileLogger.LogSuccess($"🎯 Husband name found: '{husbandResult.Value}' from {husbandResult.SourceField}");
                return husbandResult;
            }

            // TIER 3: Combined father/husband patterns
            var combinedResult = FindCombinedFatherHusbandName(record);
            if (!string.IsNullOrEmpty(combinedResult.Value))
            {
                FileLogger.LogSuccess($"🎯 Combined father/husband name found: '{combinedResult.Value}' from {combinedResult.SourceField}");
                return combinedResult;
            }

            // TIER 4: Patronymic and cultural patterns
            var patronymicResult = FindPatronymicName(record);
            if (!string.IsNullOrEmpty(patronymicResult.Value))
            {
                FileLogger.LogSuccess($"🎯 Patronymic name found: '{patronymicResult.Value}' from {patronymicResult.SourceField}");
                return patronymicResult;
            }

            // TIER 5: UNSCR and source-specific patterns
            var unscrResult = FindUNSCRFatherName(record);
            if (!string.IsNullOrEmpty(unscrResult.Value))
            {
                FileLogger.LogSuccess($"🎯 UNSCR father name found: '{unscrResult.Value}' from {unscrResult.SourceField}");
                return unscrResult;
            }

            FileLogger.LogWarning("⚠️ No father/husband name found");
            return new FieldMappingResult { Value = "", SourceField = "Default value used" };
        }

        /// <summary>
        /// Find direct father name patterns
        /// </summary>
        private static FieldMappingResult FindDirectFatherName(Dictionary<string, object> record)
        {
            var fatherNamePatterns = new[] {
                "fatherName", "father_name", "FatherName", "FATHER_NAME",
                "fathersName", "fathers_name", "FathersName",
                "paternal", "paternalName", "paternal_name",
                "father", "Father", "FATHER"
            };

            return FindFieldByPatterns(record, fatherNamePatterns);
        }

        /// <summary>
        /// Find husband name patterns
        /// </summary>
        private static FieldMappingResult FindHusbandName(Dictionary<string, object> record)
        {
            var husbandNamePatterns = new[] {
                "husbandName", "husband_name", "HusbandName", "HUSBAND_NAME",
                "husbandsName", "husbands_name", "HusbandsName",
                "spouse", "spouseName", "spouse_name",
                "husband", "Husband", "HUSBAND"
            };

            return FindFieldByPatterns(record, husbandNamePatterns);
        }

        /// <summary>
        /// Find combined father/husband name patterns
        /// </summary>
        private static FieldMappingResult FindCombinedFatherHusbandName(Dictionary<string, object> record)
        {
            var combinedPatterns = new[] {
                "fatherHusbandName", "father_husband_name", "FatherHusbandName", "FATHER_HUSBAND_NAME",
                "fatherOrHusbandName", "father_or_husband_name", "FatherOrHusbandName",
                "parentSpouseName", "parent_spouse_name", "ParentSpouseName"
            };

            return FindFieldByPatterns(record, combinedPatterns);
        }

        /// <summary>
        /// Find patronymic and cultural name patterns
        /// </summary>
        private static FieldMappingResult FindPatronymicName(Dictionary<string, object> record)
        {
            var patronymicPatterns = new[] {
                "patronymic", "patronymicName", "patronymic_name", "PatronymicName", "PATRONYMIC_NAME",
                "parentage", "parentageName", "parentage_name", "Parentage",
                "guardian", "guardianName", "guardian_name", "Guardian", "GUARDIAN_NAME",
                "parentName", "parent_name", "ParentName", "PARENT_NAME"
            };

            return FindFieldByPatterns(record, patronymicPatterns);
        }

        /// <summary>
        /// Find UNSCR and source-specific father name patterns
        /// </summary>
        private static FieldMappingResult FindUNSCRFatherName(Dictionary<string, object> record)
        {
            // UNSCR-specific patterns
            var unscrPatterns = new[] {
                "INDIVIDUAL_ALIAS_ALIAS_NAME", // Sometimes contains father name in UNSCR
                "FOURTH_NAME", "fourth_name", // Sometimes used for father name
                "ADDITIONAL_NAME", "additional_name"
            };

            var result = FindFieldByPatterns(record, unscrPatterns);
            if (!string.IsNullOrEmpty(result.Value))
            {
                return result;
            }

            // Look for any field containing "father" or "parent" in the key name
            var fatherKeyMatch = record.FirstOrDefault(kv =>
                kv.Key.ToLower().Contains("father") ||
                kv.Key.ToLower().Contains("parent") ||
                kv.Key.ToLower().Contains("patron"));

            if (!string.IsNullOrEmpty(fatherKeyMatch.Value?.ToString()))
            {
                return new FieldMappingResult
                {
                    Value = fatherKeyMatch.Value.ToString().Trim(),
                    SourceField = fatherKeyMatch.Key
                };
            }

            return new FieldMappingResult { Value = "", SourceField = "Not found" };
        }

        /// <summary>
        /// Generic pattern-based field finder
        /// </summary>
        /// <param name="record">Record to search</param>
        /// <param name="patterns">Array of patterns to try</param>
        /// <returns>FieldMappingResult with found value or empty result</returns>
        private static FieldMappingResult FindFieldByPatterns(Dictionary<string, object> record, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                if (record.ContainsKey(pattern) && !string.IsNullOrWhiteSpace(record[pattern]?.ToString()))
                {
                    return new FieldMappingResult
                    {
                        Value = record[pattern].ToString().Trim(),
                        SourceField = pattern
                    };
                }
            }

            return new FieldMappingResult { Value = "", SourceField = "Not found" };
        }

        /// <summary>
        /// Clean and normalize father/husband name
        /// </summary>
        /// <param name="rawName">Raw name value</param>
        /// <returns>Cleaned name</returns>
        public static string CleanFatherHusbandName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "";

            // Remove common prefixes/suffixes
            var cleaned = rawName.Trim();

            // Remove titles
            var titlesToRemove = new[] { "Mr.", "Mr", "S/O", "s/o", "D/O", "d/o", "W/O", "w/o" };
            foreach (var title in titlesToRemove)
            {
                if (cleaned.StartsWith(title, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(title.Length).Trim();
                }
            }

            return cleaned;
        }

        /// <summary>
        /// Log detailed father name extraction results for debugging
        /// </summary>
        /// <param name="result">Father name extraction result</param>
        /// <param name="fileName">Source file name</param>
        public static void LogFatherNameExtractionDetails(FieldMappingResult result, string fileName)
        {
            FileLogger.LogMessage($"\n🔍 FATHER NAME EXTRACTION DETAILS: {fileName}");
            FileLogger.LogMessage($"   Value: '{result.Value}'");
            FileLogger.LogMessage($"   Source Field: {result.SourceField}");

            if (!string.IsNullOrEmpty(result.Value))
            {
                var cleaned = CleanFatherHusbandName(result.Value);
                if (cleaned != result.Value)
                {
                    FileLogger.LogMessage($"   Cleaned Value: '{cleaned}'");
                }
            }
        }
    }
}