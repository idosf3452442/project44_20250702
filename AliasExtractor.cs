using System; // AliasExtractor.cs - Universal Alias Extraction System
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    /// <summary>
    /// Universal alias extraction system supporting multiple sanctions list formats
    /// Handles: OFAC (akaList/aka), Canadian (Aliases), EU (nameAlias), UN patterns
    /// Supports both individual name components and full alias names
    /// </summary>
    public static class AliasExtractor
    {
        /// <summary>
        /// Main entry point for alias extraction - detects format and extracts aliases
        /// </summary>
        /// <param name="record">Record dictionary with all field data</param>
        /// <returns>AliasExtractionResult with extracted aliases</returns>
        public static AliasExtractionResult ExtractAliases(Dictionary<string, object> record)
        {
            var result = new AliasExtractionResult
            {
                Aliases = new List<AliasInfo>(),
                SourceType = "NOT_FOUND",
                SourceFields = "No aliases found"
            };

            // PATTERN 1: OFAC Format - akaList with multiple aka entries (highest priority)
            var ofacAliases = ExtractOFACAliases(record);
            if (ofacAliases.Aliases.Count > 0)
            {
                return ofacAliases;
            }

            // PATTERN 2: Canadian Format - Simple "Aliases" field
            var canadianAliases = ExtractCanadianAliases(record);
            if (canadianAliases.Aliases.Count > 0)
            {
                return canadianAliases;
            }

            // PATTERN 3: EU Format - nameAlias fields
            var euAliases = ExtractEUAliases(record);
            if (euAliases.Aliases.Count > 0)
            {
                return euAliases;
            }

            // PATTERN 4: Generic alias fields
            var genericAliases = ExtractGenericAliases(record);
            if (genericAliases.Aliases.Count > 0)
            {
                return genericAliases;
            }

            return result;
        }

        /// <summary>
        /// Extract OFAC-style aliases (akaList/aka with firstName, lastName, category, type)
        /// Example: akaList_aka_firstName, akaList_aka_lastName, akaList_aka_category
        /// </summary>
        private static AliasExtractionResult ExtractOFACAliases(Dictionary<string, object> record)
        {
            var aliases = new List<AliasInfo>();
            var sourceFields = new List<string>();

            // Find all aka fields
            var akaFields = record.Keys.Where(k => k.Contains("akaList_aka_") || k.StartsWith("aka_")).ToList();

            if (akaFields.Count == 0)
            {
                return new AliasExtractionResult { Aliases = aliases, SourceType = "NOT_FOUND", SourceFields = "No OFAC aka fields found" };
            }

            // Group aka fields by UID or index if available
            var akaGroups = GroupOFACAliases(record, akaFields);

            foreach (var group in akaGroups)
            {
                var alias = ProcessOFACAliasGroup(group);
                if (alias != null)
                {
                    aliases.Add(alias);
                    sourceFields.AddRange(group.Keys);
                }
            }

            FileLogger.LogInfo($"🏷️ OFAC Aliases extracted: {aliases.Count} aliases from {sourceFields.Count} fields");

            return new AliasExtractionResult
            {
                Aliases = aliases,
                SourceType = "OFAC_AKALIST",
                SourceFields = string.Join(", ", sourceFields.Distinct())
            };
        }

        /// <summary>
        /// Extract Canadian-style aliases (simple "Aliases" field with full names)
        /// Example: <Aliases>Dmitry Karzyuk</Aliases>
        /// </summary>
        private static AliasExtractionResult ExtractCanadianAliases(Dictionary<string, object> record)
        {
            var aliases = new List<AliasInfo>();
            var sourceFields = new List<string>();

            // Look for Canadian-style alias fields
            var canadianAliasFields = new[] { "Aliases", "aliases", "ALIASES", "alias", "Alias", "ALIAS" };

            foreach (var fieldName in canadianAliasFields)
            {
                if (record.ContainsKey(fieldName))
                {
                    var aliasValue = record[fieldName]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(aliasValue))
                    {
                        // Split multiple aliases if separated by common delimiters
                        var splitAliases = SplitMultipleAliases(aliasValue);

                        foreach (var singleAlias in splitAliases)
                        {
                            // Use NameExtractor to parse the alias name
                            var nameComponents = ParseAliasName(singleAlias);

                            var alias = new AliasInfo
                            {
                                FullName = singleAlias,
                                FirstName = nameComponents.FirstName,
                                MiddleName = nameComponents.MiddleName,
                                LastName = nameComponents.LastName,
                                AliasType = "a.k.a.",
                                Category = "strong",
                                SourceField = fieldName
                            };
                            aliases.Add(alias);
                        }
                        sourceFields.Add(fieldName);
                    }
                }
            }

            if (aliases.Count > 0)
            {
                FileLogger.LogInfo($"🍁 Canadian Aliases extracted: {aliases.Count} aliases from fields: {string.Join(", ", sourceFields)}");
            }

            return new AliasExtractionResult
            {
                Aliases = aliases,
                SourceType = aliases.Count > 0 ? "CANADIAN_ALIASES" : "NOT_FOUND",
                SourceFields = string.Join(", ", sourceFields)
            };
        }

        /// <summary>
        /// Extract EU-style aliases (nameAlias with various patterns)
        /// Example: nameAlias_remark or nameAlias fields
        /// </summary>
        private static AliasExtractionResult ExtractEUAliases(Dictionary<string, object> record)
        {
            var aliases = new List<AliasInfo>();
            var sourceFields = new List<string>();

            // Look for EU nameAlias patterns
            var euAliasFields = record.Keys.Where(k =>
                k.Contains("nameAlias") ||
                k.Contains("aliasName") ||
                k.Contains("alternativeName")).ToList();

            foreach (var fieldName in euAliasFields)
            {
                var aliasValue = record[fieldName]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(aliasValue) && aliasValue != "good quality alias")
                {
                    var nameComponents = ParseAliasName(aliasValue);

                    var alias = new AliasInfo
                    {
                        FullName = aliasValue,
                        FirstName = nameComponents.FirstName,
                        MiddleName = nameComponents.MiddleName,
                        LastName = nameComponents.LastName,
                        AliasType = "nameAlias",
                        Category = "strong",
                        SourceField = fieldName
                    };
                    aliases.Add(alias);
                    sourceFields.Add(fieldName);
                }
            }

            if (aliases.Count > 0)
            {
                FileLogger.LogInfo($"🇪🇺 EU Aliases extracted: {aliases.Count} aliases from fields: {string.Join(", ", sourceFields)}");
            }

            return new AliasExtractionResult
            {
                Aliases = aliases,
                SourceType = aliases.Count > 0 ? "EU_NAME_ALIAS" : "NOT_FOUND",
                SourceFields = string.Join(", ", sourceFields)
            };
        }

        /// <summary>
        /// Extract generic alias patterns from various other field names
        /// </summary>
        private static AliasExtractionResult ExtractGenericAliases(Dictionary<string, object> record)
        {
            var aliases = new List<AliasInfo>();
            var sourceFields = new List<string>();

            // Generic alias field patterns
            var genericAliasFields = new[] {
                "alternativeName", "alternative_name", "AlternativeName",
                "knownAs", "known_as", "KnownAs",
                "alsoKnownAs", "also_known_as", "AlsoKnownAs",
                "pseudonym", "Pseudonym", "PSEUDONYM"
            };

            foreach (var fieldName in genericAliasFields)
            {
                if (record.ContainsKey(fieldName))
                {
                    var aliasValue = record[fieldName]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(aliasValue))
                    {
                        var nameComponents = ParseAliasName(aliasValue);

                        var alias = new AliasInfo
                        {
                            FullName = aliasValue,
                            FirstName = nameComponents.FirstName,
                            MiddleName = nameComponents.MiddleName,
                            LastName = nameComponents.LastName,
                            AliasType = "generic",
                            Category = "strong",
                            SourceField = fieldName
                        };
                        aliases.Add(alias);
                        sourceFields.Add(fieldName);
                    }
                }
            }

            return new AliasExtractionResult
            {
                Aliases = aliases,
                SourceType = aliases.Count > 0 ? "GENERIC_ALIAS" : "NOT_FOUND",
                SourceFields = string.Join(", ", sourceFields)
            };
        }

        /// <summary>
        /// Group OFAC alias fields by UID or sequential index
        /// </summary>
        private static List<Dictionary<string, object>> GroupOFACAliases(Dictionary<string, object> record, List<string> akaFields)
        {
            var groups = new List<Dictionary<string, object>>();

            // Try to group by UID first
            var uidGroups = new Dictionary<string, Dictionary<string, object>>();

            foreach (var field in akaFields)
            {
                var fieldValue = record[field]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(fieldValue)) continue;

                // Extract UID from field name if present (akaList_aka_uid)
                if (field.Contains("_uid"))
                {
                    var uid = fieldValue;
                    if (!uidGroups.ContainsKey(uid))
                    {
                        uidGroups[uid] = new Dictionary<string, object>();
                    }
                    uidGroups[uid][field] = fieldValue;
                }
                else
                {
                    // Find related fields with same pattern
                    var baseName = ExtractAliasBaseName(field);
                    var relatedFields = akaFields.Where(f => f.Contains(baseName)).ToList();

                    foreach (var relatedField in relatedFields)
                    {
                        if (record.ContainsKey(relatedField))
                        {
                            // Create a group for each set of related fields
                            var group = new Dictionary<string, object>();
                            foreach (var rf in relatedFields)
                            {
                                if (record.ContainsKey(rf))
                                {
                                    group[rf] = record[rf];
                                }
                            }
                            if (group.Count > 0 && !groups.Any(g => g.Keys.SequenceEqual(group.Keys)))
                            {
                                groups.Add(group);
                            }
                        }
                    }
                }
            }

            // Add UID-based groups
            foreach (var uidGroup in uidGroups.Values)
            {
                if (uidGroup.Count > 0)
                {
                    groups.Add(uidGroup);
                }
            }

            return groups;
        }

        /// <summary>
        /// Process a single OFAC alias group to create AliasInfo
        /// </summary>
        private static AliasInfo ProcessOFACAliasGroup(Dictionary<string, object> group)
        {
            var firstName = ExtractFromGroup(group, new[] { "firstName", "first_name", "FirstName" });
            var middleName = ExtractFromGroup(group, new[] { "middleName", "middle_name", "MiddleName" });
            var lastName = ExtractFromGroup(group, new[] { "lastName", "last_name", "LastName" });
            var aliasType = ExtractFromGroup(group, new[] { "type", "Type", "aliasType" });
            var category = ExtractFromGroup(group, new[] { "category", "Category", "quality" });

            // Build full name
            var fullName = "";
            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
            {
                var nameParts = new List<string>();
                if (!string.IsNullOrEmpty(firstName)) nameParts.Add(firstName);
                if (!string.IsNullOrEmpty(middleName)) nameParts.Add(middleName);
                if (!string.IsNullOrEmpty(lastName)) nameParts.Add(lastName);
                fullName = string.Join(" ", nameParts);
            }

            // Skip if no meaningful name data
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            return new AliasInfo
            {
                FullName = fullName,
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                AliasType = aliasType ?? "a.k.a.",
                Category = category ?? "strong",
                SourceField = string.Join(", ", group.Keys)
            };
        }

        /// <summary>
        /// Extract value from alias group by field name patterns
        /// </summary>
        private static string ExtractFromGroup(Dictionary<string, object> group, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var matchingKey = group.Keys.FirstOrDefault(k => k.EndsWith("_" + pattern) || k.EndsWith(pattern));
                if (matchingKey != null)
                {
                    return group[matchingKey]?.ToString()?.Trim() ?? "";
                }
            }
            return "";
        }

        /// <summary>
        /// Parse alias name using NameExtractor logic but adapted for aliases
        /// </summary>
        private static NameExtractionResult ParseAliasName(string aliasName)
        {
            // Create a temporary record with the alias name
            var tempRecord = new Dictionary<string, object>
            {
                ["fullName"] = aliasName
            };

            // Use NameExtractor but don't break the full name if it's a single field
            // For aliases, we generally want to keep the full name intact
            return new NameExtractionResult
            {
                FirstName = "",  // Leave empty for aliases unless clearly separated
                MiddleName = "",
                LastName = "",
                FullName = aliasName,
                SourceType = "ALIAS_FULL_NAME",
                SourceFields = "alias_field"
            };
        }

        /// <summary>
        /// Split multiple aliases separated by common delimiters
        /// </summary>
        private static List<string> SplitMultipleAliases(string aliasValue)
        {
            var delimiters = new[] { ";", "|", "\n", "\r\n" };
            var aliases = new List<string> { aliasValue }; // Start with the original

            foreach (var delimiter in delimiters)
            {
                if (aliasValue.Contains(delimiter))
                {
                    aliases = aliasValue.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(a => a.Trim())
                                      .Where(a => !string.IsNullOrEmpty(a))
                                      .ToList();
                    break;
                }
            }

            return aliases;
        }

        /// <summary>
        /// Extract base name pattern for grouping related alias fields
        /// </summary>
        private static string ExtractAliasBaseName(string fieldName)
        {
            // Extract base pattern like "akaList_aka" from "akaList_aka_firstName"
            var parts = fieldName.Split('_');
            if (parts.Length >= 3)
            {
                return string.Join("_", parts.Take(parts.Length - 1));
            }
            return fieldName;
        }

        /// <summary>
        /// Log alias extraction summary for debugging
        /// </summary>
        public static void LogAliasExtractionSummary(AliasExtractionResult result)
        {
            if (result.Aliases.Count > 0)
            {
                FileLogger.LogSuccess($"🏷️ Aliases extracted: {result.Aliases.Count} aliases using {result.SourceType}");
                FileLogger.LogMessage($"   Sources: {result.SourceFields}");

                foreach (var alias in result.Aliases.Take(3)) // Show first 3 aliases
                {
                    FileLogger.LogMessage($"   • {alias.FullName} ({alias.AliasType}, {alias.Category})");
                }

                if (result.Aliases.Count > 3)
                {
                    FileLogger.LogMessage($"   ... and {result.Aliases.Count - 3} more aliases");
                }
            }
            else
            {
                FileLogger.LogMessage($"🏷️ No aliases found ({result.SourceType})");
            }
        }
    }

    /// <summary>
    /// Data model for alias extraction results
    /// </summary>
    public class AliasExtractionResult
    {
        public List<AliasInfo> Aliases { get; set; } = new List<AliasInfo>();
        public string SourceType { get; set; } = "";
        public string SourceFields { get; set; } = "";

        internal object Select(Func<object, object> value)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Data model for individual alias information
    /// </summary>
    public class AliasInfo
    {
        public string FullName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string AliasType { get; set; } = "a.k.a.";  // a.k.a., f.k.a., nameAlias, etc.
        public string Category { get; set; } = "strong";   // strong, weak, etc.
        public string SourceField { get; set; } = "";
    }
}