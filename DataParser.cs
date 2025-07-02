using System; //last edited on 20250630-001
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ConsoleApp1
{
    public static class DataParser
    {
        /// <summary>
        /// Universal file parser that detects file type and routes to appropriate parser
        /// </summary>
        /// <param name="filePath">Full path to the file to parse</param>
        /// <returns>List of records as dictionaries</returns>
        public static List<Dictionary<string, object>> ParseAnyFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();
            try
            {
                FileLogger.LogProgress("📖 Loading file...");
                string fileExtension = Path.GetExtension(filePath).ToLower();
                FileLogger.LogSuccess($"Detected file type: {fileExtension}");

                switch (fileExtension)
                {
                    case ".xml":
                        records = ParseXmlFile(filePath);
                        break;
                    case ".csv":
                        records = ParseCsvFile(filePath);
                        break;
                    case ".xlsx":
                    case ".xls":
                        records = ParseExcelFile(filePath);
                        break;
                    default:
                        FileLogger.LogError($"Unsupported file type: {fileExtension}");
                        break;
                }
                return records;
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error parsing file: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Parse XML files with automatic structure discovery
        /// </summary>
        /// <param name="filePath">Path to XML file</param>
        /// <returns>List of parsed records</returns>
        public static List<Dictionary<string, object>> ParseXmlFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();
            XDocument doc = XDocument.Load(filePath);
            FileLogger.LogSuccess($"Root element: {doc.Root.Name.LocalName}");

            var recordElements = XmlStructureDiscovery.FindRepeatingElementsWithContainers(doc.Root);
            FileLogger.LogProgress($"Found {recordElements.Count} record elements");

            // CONFIGURATION: Set your desired limit here
            int recordLimit = 0;  // 0 = fetch all, any other number = fetch that many
                                  // int recordLimit = 5;   // Example: fetch only 5 records
                                  // int recordLimit = 10;  // Example: fetch only 10 records
                                  // int recordLimit = 100; // Example: fetch only 100 records

            // Apply the limit logic
            var recordsToProcess = recordLimit == 0 ? recordElements : recordElements.Take(recordLimit);

            // Log what we're doing
            string limitDescription = recordLimit == 0 ? "ALL" : recordLimit.ToString();
            FileLogger.LogMessage($"📊 Processing {limitDescription} records (found {recordElements.Count} total)");

            int recordCount = 0;
            foreach (var recordElement in recordsToProcess)
            {
                var record = XmlStructureDiscovery.ExtractAllFieldsFromElement(recordElement);
                if (record.Count > 0)
                {
                    record["_RAW_XML_"] = recordElement.ToString();
                    record["_RECORD_INDEX_"] = recordCount;
                    records.Add(record);
                    recordCount++;
                }
            }

            FileLogger.LogSuccess($"Extracted {recordCount} records");
            return records;
        }

        /// <summary>
        /// Parse CSV files - PLACEHOLDER for future implementation
        /// </summary>
        /// <param name="filePath">Path to CSV file</param>
        /// <returns>List of parsed records</returns>
        public static List<Dictionary<string, object>> ParseCsvFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();

            try
            {
                FileLogger.LogProgress("📄 Starting CSV parsing...");

                // TODO: Implement CSV parsing logic here
                // This will be implemented in the next step
                FileLogger.LogWarning("CSV parsing not implemented yet");

                return records;
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error parsing CSV file: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Parse Excel files - PLACEHOLDER for future implementation
        /// </summary>
        /// <param name="filePath">Path to Excel file</param>
        /// <returns>List of parsed records</returns>
        public static List<Dictionary<string, object>> ParseExcelFile(string filePath)
        {
            var records = new List<Dictionary<string, object>>();

            try
            {
                FileLogger.LogProgress("📊 Starting Excel parsing...");

                // TODO: Implement Excel parsing logic here
                // This will be implemented in the next step
                FileLogger.LogWarning("Excel parsing not implemented yet");

                return records;
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"Error parsing Excel file: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Get list of supported file extensions
        /// </summary>
        /// <returns>Array of supported extensions</returns>
        public static string[] GetSupportedExtensions()
        {
            return new string[] { ".xml", ".csv", ".xlsx", ".xls" };
        }

        /// <summary>
        /// Check if a file type is supported
        /// </summary>
        /// <param name="filePath">Path to file</param>
        /// <returns>True if supported, false otherwise</returns>
        public static bool IsFileTypeSupported(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return GetSupportedExtensions().Contains(extension);
        }
    }

    /// <summary>
    /// XML structure discovery methods for automatic XML parsing
    /// </summary>
    public static class XmlStructureDiscovery
    {
        public static List<XElement> FindRepeatingElementsWithContainers(XElement root)
        {
            FileLogger.LogProgress($"🔍 Starting enhanced XML structure discovery on root: '{root.Name.LocalName}'");

            // STEP 1: First try direct children (fastest)
            FileLogger.LogProgress("🔍 STEP 1: Checking direct children for repeating patterns...");
            var directRepeatingElements = FindRepeatingElementsOriginal(root);
            if (directRepeatingElements.Count >= 1)
            {
                FileLogger.LogProgress($"✅ STEP 1 SUCCESS: Found {directRepeatingElements.Count} direct repeating elements");
                return directRepeatingElements;
            }

            // STEP 2: Look for container elements (elements with 2+ children that don't contain text)
            FileLogger.LogProgress("🔍 STEP 2: Looking for container elements...");
            var containerCandidates = root.Elements()
                .Where(e => e.Elements().Count() >= 2)
                .Where(e => e.Elements().All(child => child.HasElements ||
                    (!child.Nodes().OfType<System.Xml.Linq.XText>().Any(n => n.NodeType == System.Xml.XmlNodeType.Text && string.IsNullOrWhiteSpace(((System.Xml.Linq.XText)n).Value)))))
                .ToList();

            if (!containerCandidates.Any())
            {
                FileLogger.LogError("No container candidates found");
                return FindRepeatingElementsDeep(root);
            }

            FileLogger.LogProgress($"📋 Container candidates found: {containerCandidates.Count}");

            List<XElement> allRecordsFromContainers = new List<XElement>();

            foreach (var container in containerCandidates)
            {
                FileLogger.LogProgress($"🔍 Checking container: '{container.Name.LocalName}' with {container.Elements().Count()} children");

                var recordsInThisContainer = FindRepeatingElementsOriginal(container);
                if (recordsInThisContainer.Any())
                {
                    FileLogger.LogProgress($"🎯 Found records in container: '{container.Name.LocalName}' → '{recordsInThisContainer.First().Name.LocalName}' ({recordsInThisContainer.Count} instances)");
                    allRecordsFromContainers.AddRange(recordsInThisContainer);
                }
            }

            if (allRecordsFromContainers.Any())
            {
                FileLogger.LogProgress($"🎯 Combined records from multiple containers: {allRecordsFromContainers.Count} total records");
                return allRecordsFromContainers;
            }

            // STEP 3: Fallback to deep search
            FileLogger.LogProgress("🔍 Container search failed, falling back to deep search...");
            return FindRepeatingElementsDeep(root);
        }

        public static List<XElement> FindRepeatingElementsOriginal(XElement root)
        {
            var elementCounts = new Dictionary<string, List<XElement>>();

            foreach (var child in root.Elements())
            {
                string elementName = child.Name.LocalName;
                if (!elementCounts.ContainsKey(elementName))
                    elementCounts[elementName] = new List<XElement>();
                elementCounts[elementName].Add(child);
            }

            var candidates = elementCounts
                .Where(kv => kv.Value.Count > 1)
                .Where(kv => kv.Value.First().Elements().Any())
                .OrderByDescending(kv => kv.Value.Count);

            if (candidates.Any())
            {
                var bestCandidate = candidates.First();
                return bestCandidate.Value;
            }

            return new List<XElement>();
        }

        public static List<XElement> FindRepeatingElementsDeep(XElement root)
        {
            var allElements = root.Descendants().ToList();
            var elementCounts = new Dictionary<string, List<XElement>>();

            foreach (var element in allElements)
            {
                if (element.HasElements)
                {
                    string elementName = element.Name.LocalName;
                    if (!elementCounts.ContainsKey(elementName))
                        elementCounts[elementName] = new List<XElement>();
                    elementCounts[elementName].Add(element);
                }
            }

            var repeatingElements = elementCounts
                .Where(kv => kv.Value.Count > 1)
                .OrderByDescending(kv => kv.Value.Count)
                .FirstOrDefault();

            return repeatingElements.Value ?? new List<XElement>();
        }

        public static Dictionary<string, object> ExtractAllFieldsFromElement(XElement element)
        {
            var fields = new Dictionary<string, object>();

            foreach (var child in element.Elements())
            {
                string fieldName = child.Name.LocalName;

                if (child.HasElements)
                {
                    var nestedFields = ExtractAllFieldsFromElement(child);
                    foreach (var nestedField in nestedFields)
                    {
                        string flattenedName = $"{fieldName}_{nestedField.Key}";
                        fields[flattenedName] = nestedField.Value;
                    }
                }
                else
                {
                    string fieldValue = child.Value?.Trim() ?? "";
                    fields[fieldName] = fieldValue;
                }

                // Extract attributes as well
                foreach (var attr in child.Attributes())
                {
                    string attrName = $"{fieldName}_{attr.Name.LocalName}";
                    fields[attrName] = attr.Value?.Trim() ?? "";
                }
            }

            return fields;
        }
    }
}