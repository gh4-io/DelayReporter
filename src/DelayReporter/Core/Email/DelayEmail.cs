using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using DelayReporter.Core.Report;

namespace DelayReporter.Core.Email
{
    /// <summary>
    /// The compact email version of a report: one line per flight with its codes, for the
    /// people who have to act on the delays rather than file them.
    ///
    /// It reads the same <see cref="ReportModel"/> as the workbook, so it lists exactly the
    /// flights the workbook does, in the same order, and carries the same summary line: a
    /// flight hidden by hand is counted here too, never quietly missing.
    /// </summary>
    public static class DelayEmail
    {
        // Ink Minimal: monochrome, hairline rules, no fills. Colour is spent only where it means
        // something (a reconciliation mismatch, an MX-coloured delay), never for decoration.
        private const string Ink = "#111111";
        private const string RowRule = "#E3E3E3";
        private const string Muted = "#595959";
        private const string Alert = "#C00000";
        private const string Font = "font-family:'Segoe UI',Calibri,Arial,sans-serif;";

        public static EmailDraft Compose(ReportModel model, string to, string cc, EmailAttachment? workbook)
        {
            var draft = new EmailDraft
            {
                Subject = Subject(model),
                Html = Html(model, workbook),
                PlainText = PlainText(model, workbook),
            };
            draft.To.AddRange(EmailDraft.SplitAddresses(to));
            draft.Cc.AddRange(EmailDraft.SplitAddresses(cc));
            if (workbook != null) draft.Attachments.Add(workbook);
            return draft;
        }

        public static string Subject(ReportModel model)
        {
            string period = Period(model);
            string flights = Plural(model.ReportedFlights, "flight");
            string kind = model.Options.MxFilter == MxFilter.MxOnly ? " MX" : string.Empty;
            return period.Length > 0
                ? $"{model.Station}{kind} departure delays, {period}: {flights}"
                : $"{model.Station}{kind} departure delays: {flights}";
        }

        /// <summary>The dates the report covers, from the options, falling back to the file's period.</summary>
        private static string Period(ReportModel model)
        {
            DateTime? from = model.Options.DateFrom;
            DateTime? to = model.Options.DateTo;
            if (from.HasValue && to.HasValue)
            {
                return from.Value.Date == to.Value.Date
                    ? model.Options.FormatDate(from.Value)
                    : model.Options.FormatDate(from.Value) + " to " + model.Options.FormatDate(to.Value);
            }
            if (from.HasValue) return "from " + model.Options.FormatDate(from.Value);
            if (to.HasValue) return "to " + model.Options.FormatDate(to.Value);
            return model.PeriodText;
        }

        /// <summary>One sentence a reader can take in before the table.</summary>
        private static string Headline(ReportModel model)
        {
            var parts = new List<string>
            {
                Plural(model.ReportedFlights, "flight") + " reported",
                Plural(model.ReportedEvents, "delay event"),
                model.TotalCodedDelayText + " coded delay",
            };
            if (model.FlightsWithMxDelay > 0) parts.Add(model.FlightsWithMxDelay + " with MX coded delay");
            string mx = ReportSummary.MxFilterText(model.Options);
            return string.Join(", ", parts) + "." + (mx.Length > 0 ? " " + mx + "." : string.Empty);
        }

        /// <summary>Flights a person left out of this report, said plainly so nobody assumes they were never there.</summary>
        private static string? LeftOutLine(ReportModel model)
        {
            var parts = new List<string>();
            if (model.ExcludedBySearch > 0)
                parts.Add(Plural(model.ExcludedBySearch, "flight") + " not matching \"" + model.Options.SearchText.Trim() + "\"");
            if (model.ExcludedHidden > 0) parts.Add(Plural(model.ExcludedHidden, "flight") + " hidden by hand");
            if (model.ExcludedNotSelected > 0) parts.Add(Plural(model.ExcludedNotSelected, "flight") + " not selected");
            if (parts.Count == 0) return null;
            bool one = model.ExcludedBySearch + model.ExcludedHidden + model.ExcludedNotSelected == 1;
            return string.Join(" and ", parts) + (one ? " is" : " are") + " not listed.";
        }

        private static string Footer(ReportModel model, EmailAttachment? workbook)
        {
            var parts = new List<string>();
            if (workbook != null) parts.Add("The full report is attached as " + workbook.FileName + ".");
            parts.Add("Minimum delay " + model.Options.MinimumDelayMinutes.ToString(CultureInfo.InvariantCulture) + " min (" +
                      (model.Options.ThresholdBasis == DelayThresholdBasis.ActualDelay ? "actual" : "included codes") + ").");
            if (model.SourceName.Length > 0) parts.Add("Source " + model.SourceName + ".");
            return string.Join(" ", parts);
        }

        // ---- HTML ----------------------------------------------------------

        private static string Html(ReportModel model, EmailAttachment? workbook)
        {
            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head>");
            html.Append("<body style=\"").Append(Font).Append("font-size:10pt;color:#1A1A1A;\">");

            html.Append("<p style=\"margin:0 0 4px 0;font-size:16px;font-weight:300;letter-spacing:.02em;color:")
                .Append(Ink).Append(";\">").Append(Encode(model.Station + " departure delays"));
            string period = Period(model);
            if (period.Length > 0) html.Append(" <span style=\"font-weight:300;color:").Append(Muted).Append(";font-size:16px;\">")
                .Append(Encode(period)).Append("</span>");
            html.Append("</p>");

            html.Append("<p style=\"margin:0 0 2px 0;\">").Append(Encode(Headline(model))).Append("</p>");
            string? leftOut = LeftOutLine(model);
            if (leftOut != null)
                html.Append("<p style=\"margin:0 0 2px 0;color:").Append(Muted).Append(";\">").Append(Encode(leftOut)).Append("</p>");

            if (model.ReportedFlights > 0) AppendTable(html, model);

            html.Append("<p style=\"margin:10px 0 0 0;font-size:8.5pt;color:").Append(Muted).Append(";\">")
                .Append(Encode(Footer(model, workbook))).Append("<br>")
                .Append(Encode("Summary: " + ReportSummary.Line(ReportSummary.Metrics(model)))).Append("</p>");

            html.Append("</body></html>");
            return html.ToString();
        }

        private static void AppendTable(StringBuilder html, ReportModel model)
        {
            html.Append("<table cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;margin-top:10px;")
                .Append(Font).Append("font-size:9.5pt;\">");

            html.Append("<tr>");
            foreach (string heading in new[] { "Date", "OPR", "Reg", "Flight", "From", "To", "STD", "ATD", "Delay", "Codes" })
            {
                html.Append("<th style=\"text-align:left;padding:6px 10px;border-bottom:2px solid ").Append(Ink)
                    .Append(";font-size:8pt;letter-spacing:.05em;text-transform:uppercase;color:").Append(Ink)
                    .Append(";font-weight:600;white-space:nowrap;\">").Append(heading).Append("</th>");
            }
            html.Append("</tr>");

            foreach (ReportFlight flight in model.Flights)
            {
                html.Append("<tr>");
                Cell(html, Encode(flight.DateText), nowrap: true);
                Cell(html, Encode(flight.OperatorDisplay), nowrap: true);
                Cell(html, Encode(flight.Registration), nowrap: true);
                Cell(html, "<b>" + Encode(flight.FlightNumber) + "</b>", nowrap: true);
                Cell(html, Encode(flight.From), nowrap: true);
                Cell(html, Encode(flight.To), nowrap: true);
                Cell(html, Encode(flight.ScheduledText), nowrap: true);
                Cell(html, Encode(flight.ActualText), nowrap: true);

                string delay = Encode(flight.ActualDelayText.Length > 0 ? flight.ActualDelayText : flight.CodedDelayText);
                Cell(html, flight.Reconciles == false ? "<b style=\"color:" + Alert + ";\">" + delay + "</b>" : "<b>" + delay + "</b>",
                     nowrap: true);

                // Each code gets its own line with room under it, rather than a tight <br> stack,
                // so a flight with several codes still reads as a list, not a block of text.
                string codes = string.Join(string.Empty, flight.Events.Select((e, i) =>
                    "<div style=\"margin-top:" + (i == 0 ? "0" : "8") + "px;\">" +
                    "<b>" + Encode(e.Code) + "</b>&nbsp;" + Encode(e.Duration) + "&nbsp; " + Encode(e.Label) + "</div>"));
                Cell(html, codes);

                html.Append("</tr>");
            }

            html.Append("</table>");
        }

        private static void Cell(StringBuilder html, string content, bool nowrap = false)
        {
            html.Append("<td style=\"padding:10px;border-bottom:1px solid ").Append(RowRule).Append(";vertical-align:top;");
            if (nowrap) html.Append("white-space:nowrap;");
            html.Append("\">").Append(content).Append("</td>");
        }

        private static string Encode(string text) => WebUtility.HtmlEncode(text ?? string.Empty);

        // ---- plain text ----------------------------------------------------

        private static string PlainText(ReportModel model, EmailAttachment? workbook)
        {
            var text = new StringBuilder();
            string period = Period(model);
            text.AppendLine(model.Station + " departure delays" + (period.Length > 0 ? ", " + period : string.Empty));
            text.AppendLine(Headline(model));
            string? leftOut = LeftOutLine(model);
            if (leftOut != null) text.AppendLine(leftOut);

            foreach (ReportFlight flight in model.Flights)
            {
                text.AppendLine();
                string delay = flight.ActualDelayText.Length > 0 ? flight.ActualDelayText : flight.CodedDelayText;
                text.AppendLine(string.Join("  ", new[]
                {
                    flight.DateText, flight.OperatorDisplay, flight.Registration, flight.FlightNumber,
                    flight.From, flight.To, "STD " + flight.ScheduledText, "ATD " + flight.ActualText,
                    "delay " + delay,
                }.Where(s => s.Trim().Length > 0)));
                foreach (ReportDelayEvent e in flight.Events)
                    text.AppendLine("    " + e.Code + " " + e.Duration + "  " + e.Label);
                foreach (string item in flight.OutstandingItems)
                    text.AppendLine("    Outstanding: " + item);
            }

            text.AppendLine();
            text.AppendLine(Footer(model, workbook));
            text.AppendLine("Summary: " + ReportSummary.Line(ReportSummary.Metrics(model)));
            return text.ToString();
        }

        private static string Plural(int count, string noun) =>
            count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");
    }
}
