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
    /// Writes a <see cref="SheetSpec"/> as an XLSX.
    ///
    /// Hand written over ZipArchive and XmlWriter, both supplied by Windows, so the
    /// application stays a single executable with no runtime dependency. The scope is
    /// deliberately just what this one report needs: text cells, a fixed style table,
    /// column widths, row heights, merges, a freeze pane, an autofilter and print setup.
    ///
    /// Element order inside &lt;worksheet&gt; is fixed by the OOXML schema. In particular
    /// autoFilter must precede mergeCells, and printOptions, pageMargins, pageSetup and
    /// headerFooter come last, in that order. Excel and other readers reject the file
    /// outright when this is wrong.
    /// </summary>
    public static class XlsxWriter
    {
        private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private const string WorkbookContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
        private const string WorksheetContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        private const string StylesContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";

        private const string ContentTypesXml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""" + WorkbookContentType + @"""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""" + WorksheetContentType + @"""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""" + StylesContentType + @"""/>
</Types>";

        private const string RootRelsXml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""" + RelNs + @"/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

        private const string WorkbookRelsXml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""" + RelNs + @"/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""" + RelNs + @"/styles"" Target=""styles.xml""/>
</Relationships>";

        public static void Write(string path, SheetSpec sheet)
        {
            // Build in memory first so a failure cannot leave a half written report behind.
            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                WriteTo(buffer, sheet);
                bytes = buffer.ToArray();
            }
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// The package is written as a plain zip with its content types and relationships
        /// spelled out, rather than through System.IO.Packaging. Packaging rewrites part
        /// names and maintains its own relationship store, which makes the output harder
        /// to predict and to compare against a reference file; a zip written here is
        /// exactly the bytes intended.
        /// </summary>
        public static void WriteTo(Stream output, SheetSpec sheet)
        {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
                WriteEntry(archive, "_rels/.rels", RootRelsXml);
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
                WriteEntry(archive, "xl/styles.xml", XlsxStyles.Xml);

                using (Stream stream = archive.CreateEntry("xl/workbook.xml", CompressionLevel.Optimal).Open())
                    WriteWorkbook(stream, sheet);

                using (Stream stream = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open())
                    WriteWorksheet(stream, sheet);
            }
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using (Stream stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static XmlWriterSettings WriterSettings() => new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            // Line breaks inside a cell are meaningful; leave them exactly as built.
            NewLineHandling = NewLineHandling.None,
        };

        private static void WriteWorkbook(Stream stream, SheetSpec sheet)
        {
            string quotedName = "'" + sheet.Name.Replace("'", "''") + "'";
            string lastColumn = SheetSpec.ColumnName(sheet.LastColumn);

            using (XmlWriter w = XmlWriter.Create(stream, WriterSettings()))
            {
                w.WriteStartDocument(true);
                w.WriteStartElement("workbook", Ns);
                w.WriteAttributeString("xmlns", "r", null, RelNs);

                w.WriteStartElement("sheets", Ns);
                w.WriteStartElement("sheet", Ns);
                w.WriteAttributeString("name", sheet.Name);
                w.WriteAttributeString("sheetId", "1");
                w.WriteAttributeString("id", RelNs, "rId1");
                w.WriteEndElement();
                w.WriteEndElement();

                w.WriteStartElement("definedNames", Ns);

                // Repeat the summary block and the table header on every printed page.
                w.WriteStartElement("definedName", Ns);
                w.WriteAttributeString("name", "_xlnm.Print_Titles");
                w.WriteAttributeString("localSheetId", "0");
                w.WriteString(quotedName + "!$1:$" + sheet.HeaderRow.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();

                w.WriteStartElement("definedName", Ns);
                w.WriteAttributeString("name", "_xlnm._FilterDatabase");
                w.WriteAttributeString("localSheetId", "0");
                w.WriteAttributeString("hidden", "1");
                w.WriteString(quotedName + "!$A$" + sheet.HeaderRow.ToString(CultureInfo.InvariantCulture) +
                              ":$" + lastColumn + "$" + sheet.LastRow.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();

                w.WriteEndElement();    // definedNames
                w.WriteEndElement();    // workbook
                w.WriteEndDocument();
            }
        }

        private static void WriteWorksheet(Stream stream, SheetSpec sheet)
        {
            string lastColumn = SheetSpec.ColumnName(sheet.LastColumn);
            string range = "A" + sheet.HeaderRow.ToString(CultureInfo.InvariantCulture) +
                           ":" + lastColumn + sheet.LastRow.ToString(CultureInfo.InvariantCulture);
            int freezeRow = sheet.HeaderRow + 1;

            using (XmlWriter w = XmlWriter.Create(stream, WriterSettings()))
            {
                w.WriteStartDocument(true);
                w.WriteStartElement("worksheet", Ns);
                w.WriteAttributeString("xmlns", "r", null, RelNs);

                w.WriteStartElement("sheetPr", Ns);
                w.WriteStartElement("pageSetUpPr", Ns);
                w.WriteAttributeString("fitToPage", "1");
                w.WriteEndElement();
                w.WriteEndElement();

                w.WriteStartElement("dimension", Ns);
                w.WriteAttributeString("ref", "A1:" + lastColumn + sheet.LastRow.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();

                WriteSheetViews(w, freezeRow);
                WriteColumns(w, sheet);
                WriteSheetData(w, sheet);

                // autoFilter must come before mergeCells.
                w.WriteStartElement("autoFilter", Ns);
                w.WriteAttributeString("ref", range);
                w.WriteEndElement();

                WriteMerges(w, sheet);
                WritePrintSetup(w, sheet);

                w.WriteEndElement();    // worksheet
                w.WriteEndDocument();
            }
        }

        private static void WriteSheetViews(XmlWriter w, int freezeRow)
        {
            string topLeft = "A" + freezeRow.ToString(CultureInfo.InvariantCulture);

            w.WriteStartElement("sheetViews", Ns);
            w.WriteStartElement("sheetView", Ns);
            w.WriteAttributeString("showGridLines", "0");
            w.WriteAttributeString("tabSelected", "1");
            w.WriteAttributeString("workbookViewId", "0");

            w.WriteStartElement("pane", Ns);
            w.WriteAttributeString("ySplit", (freezeRow - 1).ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("topLeftCell", topLeft);
            w.WriteAttributeString("activePane", "bottomLeft");
            w.WriteAttributeString("state", "frozen");
            w.WriteEndElement();

            w.WriteStartElement("selection", Ns);
            w.WriteAttributeString("pane", "bottomLeft");
            w.WriteAttributeString("activeCell", topLeft);
            w.WriteAttributeString("sqref", topLeft);
            w.WriteEndElement();

            w.WriteEndElement();
            w.WriteEndElement();
        }

        private static void WriteColumns(XmlWriter w, SheetSpec sheet)
        {
            if (sheet.Columns.Count == 0) return;

            w.WriteStartElement("sheetFormatPr", Ns);
            w.WriteAttributeString("defaultRowHeight", "14.5");
            w.WriteEndElement();

            w.WriteStartElement("cols", Ns);
            for (int i = 0; i < sheet.Columns.Count; i++)
            {
                string index = (i + 1).ToString(CultureInfo.InvariantCulture);
                w.WriteStartElement("col", Ns);
                w.WriteAttributeString("min", index);
                w.WriteAttributeString("max", index);
                w.WriteAttributeString("width", sheet.Columns[i].Width.ToString("0.##", CultureInfo.InvariantCulture));
                w.WriteAttributeString("customWidth", "1");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        private static void WriteSheetData(XmlWriter w, SheetSpec sheet)
        {
            w.WriteStartElement("sheetData", Ns);

            foreach (int rowNumber in sheet.Rows.Keys.OrderBy(k => k))
            {
                Dictionary<int, Cell> cells = sheet.Rows[rowNumber];

                w.WriteStartElement("row", Ns);
                w.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
                if (sheet.RowHeights.TryGetValue(rowNumber, out double height))
                {
                    w.WriteAttributeString("ht", height.ToString("0.##", CultureInfo.InvariantCulture));
                    w.WriteAttributeString("customHeight", "1");
                }

                foreach (int column in cells.Keys.OrderBy(k => k))
                {
                    Cell cell = cells[column];
                    string reference = SheetSpec.ColumnName(column) + rowNumber.ToString(CultureInfo.InvariantCulture);

                    w.WriteStartElement("c", Ns);
                    w.WriteAttributeString("r", reference);
                    w.WriteAttributeString("s", ((int)cell.Style).ToString(CultureInfo.InvariantCulture));

                    if (cell.Text.Length > 0)
                    {
                        // Inline strings keep the file to one worksheet part with no
                        // shared string table to keep in step.
                        w.WriteAttributeString("t", "inlineStr");
                        w.WriteStartElement("is", Ns);
                        w.WriteStartElement("t", Ns);
                        w.WriteAttributeString("xml", "space", null, "preserve");
                        w.WriteString(cell.Text);
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }

                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        private static void WriteMerges(XmlWriter w, SheetSpec sheet)
        {
            if (sheet.Merges.Count == 0) return;

            w.WriteStartElement("mergeCells", Ns);
            w.WriteAttributeString("count", sheet.Merges.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string merge in sheet.Merges)
            {
                w.WriteStartElement("mergeCell", Ns);
                w.WriteAttributeString("ref", merge);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        private static void WritePrintSetup(XmlWriter w, SheetSpec sheet)
        {
            w.WriteStartElement("printOptions", Ns);
            w.WriteAttributeString("horizontalCentered", "1");
            w.WriteEndElement();

            w.WriteStartElement("pageMargins", Ns);
            w.WriteAttributeString("left", "0.3");
            w.WriteAttributeString("right", "0.3");
            w.WriteAttributeString("top", "0.4");
            w.WriteAttributeString("bottom", "0.4");
            w.WriteAttributeString("header", "0.2");
            w.WriteAttributeString("footer", "0.2");
            w.WriteEndElement();

            w.WriteStartElement("pageSetup", Ns);
            w.WriteAttributeString("paperSize", "1");       // Letter
            w.WriteAttributeString("orientation", "landscape");
            w.WriteAttributeString("fitToWidth", "1");
            w.WriteAttributeString("fitToHeight", "0");
            w.WriteEndElement();

            if (!string.IsNullOrEmpty(sheet.FooterText))
            {
                w.WriteStartElement("headerFooter", Ns);
                w.WriteStartElement("oddFooter", Ns);
                w.WriteString(sheet.FooterText);
                w.WriteEndElement();
                w.WriteEndElement();
            }
        }
    }
}
