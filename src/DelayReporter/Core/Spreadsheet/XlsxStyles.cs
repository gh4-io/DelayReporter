namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>
    /// The workbook's fixed style table.
    ///
    /// The report has one layout, so the styles are a fixed list rather than a builder.
    /// The order of &lt;cellXfs&gt; is the order of <see cref="CellStyle"/>; changing one
    /// without the other silently restyles the report.
    /// </summary>
    internal static class XlsxStyles
    {
        public const string Xml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""9"">
    <font><sz val=""11""/><name val=""Calibri""/></font>
    <font><b/><sz val=""16""/><color rgb=""FF1F3864""/><name val=""Calibri""/></font>
    <font><sz val=""10""/><color rgb=""FF595959""/><name val=""Calibri""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FF1F3864""/><name val=""Calibri""/></font>
    <font><sz val=""10""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font>
    <font><i/><sz val=""9""/><color rgb=""FF808080""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FFC00000""/><name val=""Calibri""/></font>
  </fonts>
  <fills count=""4"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FF1F3864""/><bgColor indexed=""64""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FFD9E1F2""/><bgColor indexed=""64""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style=""thin""><color rgb=""FFBFBFBF""/></left>
      <right style=""thin""><color rgb=""FFBFBFBF""/></right>
      <top style=""thin""><color rgb=""FFBFBFBF""/></top>
      <bottom style=""thin""><color rgb=""FFBFBFBF""/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""13"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""2"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""3"" fillId=""2"" borderId=""0"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyAlignment=""1""><alignment vertical=""center"" indent=""1""/></xf>
    <xf numFmtId=""0"" fontId=""4"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""6"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""top""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""7"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""8"" fillId=""0"" borderId=""1"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""top""/></xf>
  </cellXfs>
  <cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>
  <dxfs count=""0""/>
</styleSheet>";
    }
}
