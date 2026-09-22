using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
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
    /// </summary>
    public static class XlsxReader
    {
        private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string OfficeDocumentRel = RelNs + "/officeDocument";
        private const string SharedStringsRel = RelNs + "/sharedStrings";

        public static CellGrid ReadFile(string path)
        {
            using (var package = Package.Open(path, FileMode.Open, FileAccess.Read))
                return Read(package, Path.GetFileName(path));
        }

        private static CellGrid Read(Package package, string sourceName)
        {
            PackagePart workbook = Resolve(package, package.GetRelationshipsByType(OfficeDocumentRel).FirstOrDefault())
                ?? throw new SpreadsheetFormatException("The file has no workbook part; it may not be an XLSX.");

            string? sheetRelId = FirstSheetRelationshipId(workbook);
            if (sheetRelId == null)
                throw new SpreadsheetFormatException("The workbook contains no worksheets.");

            PackagePart sheet = Resolve(package, workbook.GetRelationship(sheetRelId))
                ?? throw new SpreadsheetFormatException("The workbook's first worksheet is missing.");

            string[] shared = ReadSharedStrings(package, workbook);
            return new CellGrid(ReadSheet(sheet, shared), sourceName);
        }

        private static PackagePart? Resolve(Package package, PackageRelationship? relationship)
        {
            if (relationship == null) return null;
            Uri target = PackUriHelper.ResolvePartUri(relationship.SourceUri, relationship.TargetUri);
            return package.PartExists(target) ? package.GetPart(target) : null;
        }

        private static string? FirstSheetRelationshipId(PackagePart workbook)
        {
            using (var stream = workbook.GetStream(FileMode.Open, FileAccess.Read))
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

        private static string[] ReadSharedStrings(Package package, PackagePart workbook)
        {
            PackagePart? part = Resolve(package, workbook.GetRelationshipsByType(SharedStringsRel).FirstOrDefault());
            if (part == null) return Array.Empty<string>();

            var values = new List<string>();
            using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(stream, SafeSettings()))
            {
                var text = new StringBuilder();
                bool inItem = false;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns)
                    {
                        if (reader.LocalName == "si") { inItem = true; text.Clear(); }
                        else if (reader.LocalName == "t" && inItem && !reader.IsEmptyElement)
                            text.Append(reader.ReadElementContentAsString());
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "si" && reader.NamespaceURI == Ns)
                    {
                        values.Add(text.ToString());
                        inItem = false;
                    }
                }
            }
            return values.ToArray();
        }

        private static IEnumerable<string[]> ReadSheet(PackagePart sheet, string[] shared)
        {
            var rows = new List<string[]>();
            using (var stream = sheet.GetStream(FileMode.Open, FileAccess.Read))
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
