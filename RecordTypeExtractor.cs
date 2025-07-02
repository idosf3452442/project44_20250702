using System;//last edited on 20250630-001
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>
    /// Advanced record type classification with source-specific patterns and confidence scoring
    /// Determines if a record represents an INDIVIDUAL, ENTITY, or other classification
    /// </summary>
    public static class RecordTypeExtractor
    {
        /// <summary>
        /// Main entry point for record type classification
        /// Returns enhanced result with confidence and detailed analysis
        /// </summary>
        /// <param name="record">Record dictionary to analyze</param>
        /// <param name="fileName">Source file name for context</param>
        /// <returns>FieldMappingResult with classified record type</returns>
        public static FieldMappingResult FindFieldForRecordType(Dictionary<string, object> record, string fileName = "")
        {
            FileLogger.LogProgress("🔍 Starting record type classification...");

            // STEP 1: Try explicit record type fields first
            var explicitResult = FindExplicitRecordType(record);
            if (!string.IsNullOrEmpty(explicitResult.Value))
            {
                var normalizedType = NormalizeRecordType(explicitResult.Value);
                FileLogger.LogSuccess($"🎯 Explicit type found: '{explicitResult.Value}' → '{normalizedType}' from {explicitResult.SourceField}");

                return new FieldMappingResult
                {
                    Value = normalizedType,
                    SourceField = explicitResult.SourceField,
                    SecondaryValue = "HIGH_CONFIDENCE"
                };
            }

            // STEP 2: Use source-specific pattern analysis
            var inferredResult = InferRecordTypeFromPatterns(record, fileName);
            FileLogger.LogMessage($"🔍 Inferred type: '{inferredResult.Value}' (confidence: {inferredResult.SecondaryValue})");

            return inferredResult;
        }

        /// <summary>
        /// Search for explicit record type fields with comprehensive patterns
        /// </summary>
        /// <param name="record">Record to search</param>
        /// <returns>Explicit record type if found</returns>
        public static FieldMappingResult FindExplicitRecordType(Dictionary<string, object> record)
        {
            // Comprehensive explicit type field patterns
            string[] explicitTypePatterns = {
                // Standard patterns
                "recordType", "record_type", "RecordType",
                "type", "Type", "TYPE",
                "category", "Category", "CATEGORY",
                "classification", "Classification",
                "entityType", "entity_type", "EntityType",
                
                // Source-specific patterns
                "sdnType",           // OFAC SDN
                "UN_LIST_TYPE",      // UNSCR
                "subjectType",       // EU Lists
                "listType",          // General
                "personType",        // Person-specific
                "organizationType"   // Organization-specific
            };

            foreach (var pattern in explicitTypePatterns)
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

            return new FieldMappingResult { Value = "", SourceField = "No explicit type field found" };
        }

        /// <summary>
        /// Infer record type from field patterns and content analysis
        /// </summary>
        /// <param name="record">Record to analyze</param>
        /// <param name="fileName">Source file for context</param>
        /// <returns>Inferred record type with confidence</returns>
        public static FieldMappingResult InferRecordTypeFromPatterns(Dictionary<string, object> record, string fileName)
        {
            var analysis = AnalyzeRecordFields(record);

            // Determine type based on field analysis
            if (analysis.IndividualScore > analysis.EntityScore)
            {
                string confidence = analysis.IndividualScore >= 3 ? "HIGH_CONFIDENCE" :
                                   analysis.IndividualScore >= 2 ? "MEDIUM_CONFIDENCE" : "LOW_CONFIDENCE";

                FileLogger.LogMessage($"📊 Individual indicators: {analysis.IndividualScore}, Entity indicators: {analysis.EntityScore}");

                return new FieldMappingResult
                {
                    Value = "INDIVIDUAL",
                    SourceField = $"Inferred from patterns ({string.Join(", ", analysis.IndividualIndicators)})",
                    SecondaryValue = confidence
                };
            }
            else if (analysis.EntityScore > analysis.IndividualScore)
            {
                string confidence = analysis.EntityScore >= 3 ? "HIGH_CONFIDENCE" :
                                   analysis.EntityScore >= 2 ? "MEDIUM_CONFIDENCE" : "LOW_CONFIDENCE";

                FileLogger.LogMessage($"📊 Entity indicators: {analysis.EntityScore}, Individual indicators: {analysis.IndividualScore}");

                return new FieldMappingResult
                {
                    Value = "ENTITY",
                    SourceField = $"Inferred from patterns ({string.Join(", ", analysis.EntityIndicators)})",
                    SecondaryValue = confidence
                };
            }

            // Default to UNKNOWN with low confidence
            FileLogger.LogMessage("🔄 Using default classification: UNKNOWN");
            return new FieldMappingResult
            {
                Value = "UNKNOWN",
                SourceField = "Default classification (insufficient indicators)",
                SecondaryValue = "LOW_CONFIDENCE"
            };
        }

        /// <summary>
        /// Analyze record fields to score individual vs entity likelihood
        /// </summary>
        /// <param name="record">Record to analyze</param>
        /// <returns>Analysis result with scores and indicators</returns>
        private static RecordTypeAnalysis AnalyzeRecordFields(Dictionary<string, object> record)
        {
            var analysis = new RecordTypeAnalysis();

            foreach (var field in record.Keys)
            {
                string fieldLower = field.ToLower();

                // Individual indicators (high confidence)
                if (IsIndividualField(fieldLower, 3))
                {
                    analysis.IndividualScore += 3;
                    analysis.IndividualIndicators.Add(field);
                }
                // Individual indicators (medium confidence)
                else if (IsIndividualField(fieldLower, 2))
                {
                    analysis.IndividualScore += 2;
                    analysis.IndividualIndicators.Add(field);
                }
                // Individual indicators (low confidence)
                else if (IsIndividualField(fieldLower, 1))
                {
                    analysis.IndividualScore += 1;
                    analysis.IndividualIndicators.Add(field);
                }

                // Entity indicators (high confidence)
                if (IsEntityField(fieldLower, 3))
                {
                    analysis.EntityScore += 3;
                    analysis.EntityIndicators.Add(field);
                }
                // Entity indicators (medium confidence)
                else if (IsEntityField(fieldLower, 2))
                {
                    analysis.EntityScore += 2;
                    analysis.EntityIndicators.Add(field);
                }
                // Entity indicators (low confidence)
                else if (IsEntityField(fieldLower, 1))
                {
                    analysis.EntityScore += 1;
                    analysis.EntityIndicators.Add(field);
                }
            }

            return analysis;
        }

        /// <summary>
        /// Check if field name indicates individual/person
        /// </summary>
        /// <param name="fieldName">Field name to check</param>
        /// <param name="confidenceLevel">1=low, 2=medium, 3=high confidence</param>
        /// <returns>True if field indicates individual</returns>
        private static bool IsIndividualField(string fieldName, int confidenceLevel)
        {
            switch (confidenceLevel)
            {
                case 3: // High confidence individual indicators
                    return fieldName.Contains("firstname") || fieldName.Contains("first_name") ||
                           fieldName.Contains("lastname") || fieldName.Contains("last_name") ||
                           fieldName.Contains("middlename") || fieldName.Contains("middle_name") ||
                           fieldName.Contains("fathername") || fieldName.Contains("father_name") ||
                           fieldName.Contains("dateofbirth") || fieldName.Contains("birthdate") ||
                           fieldName.Contains("placeofbirth") || fieldName.Contains("birthplace");

                case 2: // Medium confidence individual indicators
                    return fieldName.Contains("individual") || fieldName.Contains("person") ||
                           fieldName.Contains("gender") || fieldName.Contains("nationality") ||
                           fieldName.Contains("passport") || fieldName.Contains("cnic") ||
                           fieldName.Contains("alias") || fieldName.Contains("title");

                case 1: // Low confidence individual indicators
                    return fieldName.Contains("name") && !fieldName.Contains("company") &&
                           !fieldName.Contains("organization") && !fieldName.Contains("corp");

                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if field name indicates entity/organization
        /// </summary>
        /// <param name="fieldName">Field name to check</param>
        /// <param name="confidenceLevel">1=low, 2=medium, 3=high confidence</param>
        /// <returns>True if field indicates entity</returns>
        private static bool IsEntityField(string fieldName, int confidenceLevel)
        {
            switch (confidenceLevel)
            {
                case 3: // High confidence entity indicators
                    return fieldName.Contains("organization") || fieldName.Contains("company") ||
                           fieldName.Contains("corporation") || fieldName.Contains("enterprise") ||
                           fieldName.Contains("business") || fieldName.Contains("firm") ||
                           fieldName.Contains("agency") || fieldName.Contains("institution");

                case 2: // Medium confidence entity indicators
                    return fieldName.Contains("entity") || fieldName.Contains("legal") ||
                           fieldName.Contains("commercial") || fieldName.Contains("trade") ||
                           fieldName.Contains("industry") || fieldName.Contains("group");

                case 1: // Low confidence entity indicators
                    return fieldName.Contains("registration") || fieldName.Contains("license") ||
                           fieldName.Contains("tax") || fieldName.Contains("vat") ||
                           fieldName.Contains("ein") || fieldName.Contains("duns");

                default:
                    return false;
            }
        }

        /// <summary>
        /// Normalize various record type values to standard classifications
        /// </summary>
        /// <param name="rawType">Raw type value from source</param>
        /// <returns>Normalized type (INDIVIDUAL, ENTITY, or UNKNOWN)</returns>
        public static string NormalizeRecordType(string rawType)
        {
            if (string.IsNullOrWhiteSpace(rawType))
                return "INDIVIDUAL";

            string normalized = rawType.ToUpper().Trim();

            // Individual variations
            if (normalized.Contains("INDIVIDUAL") || normalized.Contains("PERSON") ||
                normalized.Contains("NATURAL") || normalized.Contains("HUMAN") ||
                normalized == "I" || normalized == "P")
            {
                return "INDIVIDUAL";
            }

            // Entity variations
            if (normalized.Contains("ENTITY") || normalized.Contains("ORGANIZATION") ||
                normalized.Contains("COMPANY") || normalized.Contains("CORPORATION") ||
                normalized.Contains("ENTERPRISE") || normalized.Contains("BUSINESS") ||
                normalized.Contains("LEGAL") || normalized == "E" || normalized == "O")
            {
                return "ENTITY";
            }

            // Everything else becomes UNKNOWN
            FileLogger.LogWarning($"🔄 Unrecognized record type '{rawType}' set to UNKNOWN");
            return "UNKNOWN";
        }

        /// <summary>
        /// Helper class for field analysis results
        /// </summary>
        private class RecordTypeAnalysis
        {
            public int IndividualScore { get; set; } = 0;
            public int EntityScore { get; set; } = 0;
            public List<string> IndividualIndicators { get; set; } = new List<string>();
            public List<string> EntityIndicators { get; set; } = new List<string>();
        }
    }
}