namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>
    /// The workbook's fixed style table.
    ///
    /// The report has two fixed layouts, so the styles are a fixed list rather than a builder.
    /// The order of &lt;cellXfs&gt; is the order of <see cref="CellStyle"/>, and the order of
    /// &lt;dxfs&gt; is the order of <see cref="DifferentialStyle"/>; changing one without the
    /// other silently restyles the report. Entries 1 to 12 belong to the classic layout, 13
    /// onwards to the grouped one.
    /// </summary>
    internal static class XlsxStyles
    {
        public const string Xml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""12"">
    <font><sz val=""11""/><name val=""Calibri""/></font>
    <font><b/><sz val=""16""/><color rgb=""FF1F3864""/><name val=""Calibri""/></font>
    <font><sz val=""10""/><color rgb=""FF595959""/><name val=""Calibri""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FF1F3864""/><name val=""Calibri""/></font>
    <font><sz val=""10""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font>
    <font><i/><sz val=""9""/><color rgb=""FF808080""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FFC00000""/><name val=""Calibri""/></font>
    <font><b/><sz val=""16""/><color rgb=""FF000000""/><name val=""Calibri""/></font>
    <font><b/><sz val=""10""/><color rgb=""FF000000""/><name val=""Calibri""/></font>
    <font><sz val=""11""/><color rgb=""FF262626""/><name val=""Calibri""/></font>
  </fonts>
  <fills count=""4"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FF1F3864""/><bgColor indexed=""64""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FFD9E1F2""/><bgColor indexed=""64""/></patternFill></fill>
  </fills>
  <borders count=""3"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style=""thin""><color rgb=""FFBFBFBF""/></left>
      <right style=""thin""><color rgb=""FFBFBFBF""/></right>
      <top style=""thin""><color rgb=""FFBFBFBF""/></top>
      <bottom style=""thin""><color rgb=""FFBFBFBF""/></bottom>
      <diagonal/>
    </border>
    <border><left/><right/><top/><bottom style=""thin""><color rgb=""FF404040""/></bottom><diagonal/></border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""30"">
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
    <xf numFmtId=""0"" fontId=""9"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""11"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""2"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""10"" fillId=""0"" borderId=""2"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""bottom""/></xf>
    <xf numFmtId=""0"" fontId=""2"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""10"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""10"" fillId=""0"" borderId=""2"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""bottom"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""10"" fillId=""0"" borderId=""2"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""bottom"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""10"" fillId=""0"" borderId=""2"" xfId=""0"" applyFont=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""right"" vertical=""bottom"" indent=""1"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""right"" vertical=""center"" indent=""1"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""5"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""8"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center""/></xf>
  </cellXfs>
  <cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>
  <dxfs count=""1"">
    <dxf><border><bottom style=""thin""><color rgb=""FF808080""/></bottom></border></dxf>
  </dxfs>
</styleSheet>";
    }
}
