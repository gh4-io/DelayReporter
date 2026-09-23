using DelayReporter.Core.Spreadsheet;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// Lays a <see cref="ReportModel"/> out as a workbook in the layout its options ask for.
    /// Both layouts write the same flights, in the same order, with the same summary; only
    /// the look differs.
    /// </summary>
    public static class ReportWriter
    {
        public static SheetSpec BuildSheet(ReportModel model) =>
            model.Options.Layout == ReportLayout.Classic
                ? ClassicReportWriter.BuildSheet(model)
                : GroupedReportWriter.BuildSheet(model);

        public static void Write(string path, ReportModel model) =>
            XlsxWriter.Write(path, BuildSheet(model));

        /// <summary>The same workbook in memory, for attaching to an email draft.</summary>
        public static byte[] ToBytes(ReportModel model)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                XlsxWriter.WriteTo(stream, BuildSheet(model));
                return stream.ToArray();
            }
        }
    }
}
