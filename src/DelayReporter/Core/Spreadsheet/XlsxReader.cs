using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>
    /// Reads the first worksheet of an XLSX into a <see cref="CellGrid"/>.
    ///
    /// Deliberately small: the report only needs cells as text. Both storage forms are
    /// handled, because they occur in real exports — the sample movement sheet ships a
    /// sharedStrings part but writes its data rows as inline strings.
    ///
    /// The package is opened as a plain zip rather than through System.IO.Packaging.
    /// An exporter that streams its output cannot seek back to fill in an entry's size, so
    /// it writes the size in a trailing Zip64 data descriptor and marks the local header as
    /// needing version 4.5. Packaging on .NET Framework refuses such a file outright, as
    /// corrupted data; Excel and a plain zip reader take the sizes from the central
    /// directory and read it. Movement sheets exported from a system arrive written this way.
    /// </summary>
    public static class XlsxReader
    {
        private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string OfficeDocumentRel = RelNs + "/officeDocument";
        private const string SharedStringsRel = RelNs + "/sharedStrings";

        public static CellGrid ReadFile(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                return Read(archive, Path.GetFileName(path));
        }

        private static CellGrid Read(ZipArchive archive, string sourceName)
        {
            Dictionary<string, ZipArchiveEntry> parts = IndexParts(archive);

            string? workbookPath = Relationships(parts, string.Empty)
                .FirstOrDefault(r => r.Type == OfficeDocumentRel)?.Target;
            if (workbookPath == null || !parts.TryGetValue(workbookPath, out ZipArchiveEntry? workbook))
                throw new SpreadsheetFormatException("The file has no workbook part; it may not be an XLSX.");

            List<Relationship> workbookRelationships = Relationships(parts, workbookPath);

            string? sheetRelId = FirstSheetRelationshipId(workbook);
            if (sheetRelId == null)
                throw new SpreadsheetFormatException("The workbook contains no worksheets.");

            string? sheetPath = workbookRelationships.FirstOrDefault(r => r.Id == sheetRelId)?.Target;
            if (sheetPath == null || !parts.TryGetValue(sheetPath, out ZipArchiveEntry? sheet))
                throw new SpreadsheetFormatException("The workbook's first worksheet is missing.");

            string[] shared = ReadSharedStrings(parts, workbookRelationships);
            return new CellGrid(ReadSheet(sheet, shared), sourceName);
        }

        /// <summary>Part names are matched without regard to case, as the package model treats them.</summary>
        private static Dictionary<string, ZipArchiveEntry> IndexParts(ZipArchive archive)
        {
            var parts = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
                parts[entry.FullName.TrimStart('/')] = entry;
            return parts;
        }

        private sealed class Relationship
        {
            public Relationship(string id, string type, string target)
            {
                Id = id;
                Type = type;
                Target = target;
            }

            public string Id { get; }
            public string Type { get; }
            public string Target { get; }
        }

        private static List<Relationship> Relationships(
            Dictionary<string, ZipArchiveEntry> parts, string partPath)
        {
            var relationships = new List<Relationship>();
            if (!parts.TryGetValue(RelationshipPartFor(partPath), out ZipArchiveEntry? entry))
                return relationships;

            using (Stream stream = entry.Open())
            using (var reader = XmlReader.Create(stream, SafeSettings()))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element ||
                        reader.LocalName != "Relationship" ||
                        reader.NamespaceURI != PackageRelNs) continue;

                    string? id = reader.GetAttribute("Id");
                    string? type = reader.GetAttribute("Type");
                    string? target = reader.GetAttribute("Target");
                    if (id == null || type == null || target == null) continue;

                    // An external target names something outside the package, so there is no part to read.
                    if (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                        continue;

                    relationships.Add(new Relationship(id, type, ResolveTarget(partPath, target)));
                }
            }
            return relationships;
        }

        /// <summary>"xl/workbook.xml" becomes "xl/_rels/workbook.xml.rels"; the package root becomes "_rels/.rels".</summary>
        private static string RelationshipPartFor(string partPath)
        {
            int slash = partPath.LastIndexOf('/');
            string folder = slash < 0 ? string.Empty : partPath.Substring(0, slash + 1);
            string name = slash < 0 ? partPath : partPath.Substring(slash + 1);
            return folder + "_rels/" + name + ".rels";
        }

        /// <summary>A relationship target is a URI reference, relative to the folder holding its own part.</summary>
        private static string ResolveTarget(string sourcePartPath, string target)
        {
            string decoded = Uri.UnescapeDataString(target);
            if (decoded.StartsWith("/", StringComparison.Ordinal)) return decoded.Substring(1);

            int slash = sourcePartPath.LastIndexOf('/');
            string folder = slash < 0 ? string.Empty : sourcePartPath.Substring(0, slash + 1);

            var segments = new List<string>();
            foreach (string segment in (folder + decoded).Split('/'))
            {
                if (segment.Length == 0 || segment == ".") continue;
                if (segment == "..")
                {
                    if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            return string.Join("/", segments);
        }

        private static string? FirstSheetRelationshipId(ZipArchiveEntry workbook)
        {
            using (Stream stream = workbook.Open())
            using (var reader = XmlReader.Create(stream, SafeSettings()))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet" && reader.NamespaceURI == Ns)
                        return reader.GetAttribute("id", RelNs);
                }
            }
            return null;
        }

        private static string[] ReadSharedStrings(
            Dictionary<string, ZipArchiveEntry> parts, List<Relationship> workbookRelationships)
        {
            string? path = workbookRelationships.FirstOrDefault(r => r.Type == SharedStringsRel)?.Target;
            if (path == null || !parts.TryGetValue(path, out ZipArchiveEntry? part)) return Array.Empty<string>();

            var values = new List<string>();
            using (Stream stream = part.Open())
            using (var reader = XmlReader.Create(stream, SafeSettings()))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si" && reader.NamespaceURI == Ns)
                        values.Add(ReadSharedItem(reader));
                }
            }
            return values.ToArray();
        }

        /// <summary>
        /// Joins the runs of one shared string. A subtree reader is used because reading a
        /// run's text leaves the reader on the item's closing tag, which the enclosing loop
        /// would then step over: Excel writes the part without indentation, so there is no
        /// whitespace node in between to absorb that step.
        /// </summary>
        private static string ReadSharedItem(XmlReader reader)
        {
            if (reader.IsEmptyElement) return string.Empty;

            var text = new StringBuilder();
            using (XmlReader item = reader.ReadSubtree())
            {
                item.Read();                                   // position on <si> itself
                while (item.Read())
                {
                    if (item.NodeType == XmlNodeType.Element && item.LocalName == "t" &&
                        item.NamespaceURI == Ns && !item.IsEmptyElement)
                        text.Append(item.ReadElementContentAsString());
                }
            }
            return text.ToString();
        }

        private static IEnumerable<string[]> ReadSheet(ZipArchiveEntry sheet, string[] shared)
        {
            var rows = new List<string[]>();
            using (Stream stream = sheet.Open())
            using (var reader = XmlReader.Create(stream, SafeSettings()))
            {
                var cells = new List<string>();
                bool inRow = false;
                int nextRowNumber = 1;

                while (reader.Read())
                {
                    if (reader.NamespaceURI != Ns) continue;

                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                    {
                        // Honour the declared row number so blank rows keep their position.
                        if (int.TryParse(reader.GetAttribute("r"), out int declared))
                        {
                            while (nextRowNumber < declared) { rows.Add(Array.Empty<string>()); nextRowNumber++; }
                        }
                        cells.Clear();
                        if (reader.IsEmptyElement)
                        {
                            rows.Add(Array.Empty<string>());
                            nextRowNumber++;
                        }
                        else inRow = true;
                        continue;
                    }

                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row")
                    {
                        rows.Add(cells.ToArray());
                        nextRowNumber++;
                        inRow = false;
                        continue;
                    }

                    if (!inRow || reader.NodeType != XmlNodeType.Element || reader.LocalName != "c") continue;

                    string? reference = reader.GetAttribute("r");
                    string? type = reader.GetAttribute("t");
                    int column = reference != null ? ColumnIndex(reference) : cells.Count;
                    if (column < 0) column = cells.Count;

                    string value = ReadCellValue(reader, type, shared);

                    while (cells.Count < column) cells.Add(string.Empty);
                    if (cells.Count == column) cells.Add(value); else cells[column] = value;
                }

                if (inRow) rows.Add(cells.ToArray());
            }
            return rows;
        }

        /// <summary>
        /// Consumes exactly one &lt;c&gt; element. A subtree reader is used so the cell's
        /// content cannot over-consume the row's closing tag.
        /// </summary>
        private static string ReadCellValue(XmlReader reader, string? type, string[] shared)
        {
            if (reader.IsEmptyElement) return string.Empty;

            string raw = string.Empty;
            bool inline = false;
            var inlineText = new StringBuilder();

            using (XmlReader cell = reader.ReadSubtree())
            {
                cell.Read();                                   // position on <c> itself
                while (cell.Read())
                {
                    if (cell.NodeType != XmlNodeType.Element || cell.NamespaceURI != Ns) continue;

                    if (cell.LocalName == "v")
                    {
                        if (!cell.IsEmptyElement) raw = cell.ReadElementContentAsString();
                    }
                    else if (cell.LocalName == "is")
                    {
                        inline = true;
                    }
                    else if (cell.LocalName == "t")
                    {
                        if (!cell.IsEmptyElement) inlineText.Append(cell.ReadElementContentAsString());
                    }
                }
            }

            if (inline || inlineText.Length > 0) return inlineText.ToString();

            if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                return index >= 0 && index < shared.Length ? shared[index] : string.Empty;

            return raw;
        }

        /// <summary>"AK12" -> 36 (0-based column index).</summary>
        public static int ColumnIndex(string cellReference)
        {
            int index = 0;
            foreach (char ch in cellReference)
            {
                if (ch >= 'A' && ch <= 'Z') index = index * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') index = index * 26 + (ch - 'a' + 1);
                else break;
            }
            return index - 1;
        }

        private static XmlReaderSettings SafeSettings() => new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
    }

    public sealed class SpreadsheetFormatException : Exception
    {
        public SpreadsheetFormatException(string message) : base(message) { }
    }
}
