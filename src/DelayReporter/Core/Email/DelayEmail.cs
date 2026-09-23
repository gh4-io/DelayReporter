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
    /// The compact email version of a report: one line per flight with its codes and whatever
    /// is still owed on it, for the people who have to act on the delays rather than file them.
    ///
    /// It reads the same <see cref="ReportModel"/> as the workbook, so it lists exactly the
    /// flights the workbook does, in the same order, and carries the same summary line: a
    /// flight hidden by hand is counted here too, never quietly missing.
    /// </summary>
    public static class DelayEmail
    {
        // The workbook's own navy and borders, so the email and the attachment look related.
        private const string Navy = "#1F3864";
        private const string Rule = "#BFBFBF";
        private const string Band = "#D9E1F2";
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

        private static string OutstandingLine(ReportModel model)
        {
            int owed = model.FlightsWithOutstandingItems;
            return owed == 0
                ? "Nothing is outstanding on any of them."
                : (owed == 1 ? "1 flight has" : owed + " flights have") + " something outstanding, listed in the last column.";
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

            html.Append("<p style=\"margin:0 0 4px 0;font-size:12pt;font-weight:bold;color:").Append(Navy).Append(";\">")
                .Append(Encode(model.Station + " departure delays"));
            string period = Period(model);
            if (period.Length > 0) html.Append(" <span style=\"font-weight:normal;\">").Append(Encode(period)).Append("</span>");
            html.Append("</p>");

            html.Append("<p style=\"margin:0 0 2px 0;\">").Append(Encode(Headline(model))).Append("</p>");
            if (model.ReportedFlights > 0)
                html.Append("<p style=\"margin:0 0 2px 0;\">").Append(Encode(OutstandingLine(model))).Append("</p>");
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
            html.Append("<table cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;margin-top:8px;")
                .Append(Font).Append("font-size:9pt;\">");

            html.Append("<tr>");
            foreach (string heading in new[] { "Date", "Flight", "Reg", "To", "STD", "ATD", "Delay", "Codes", "Outstanding" })
            {
                html.Append("<th style=\"background:").Append(Navy).Append(";color:#FFFFFF;text-align:left;")
                    .Append("padding:3px 6px;border:1px solid ").Append(Navy).Append(";white-space:nowrap;\">")
                    .Append(heading).Append("</th>");
            }
            html.Append("</tr>");

            bool band = false;
            foreach (ReportFlight flight in model.Flights)
            {
                string background = band ? Band : "#FFFFFF";
                band = !band;

                html.Append("<tr style=\"background:").Append(background).Append(";\">");
                Cell(html, Encode(flight.DateText), nowrap: true);

                string flightCell = "<b>" + Encode(flight.FlightNumber) + "</b>";
                if (flight.IsMxDelay)
                    flightCell += " <span style=\"font-size:7.5pt;font-weight:bold;color:" + Alert + ";\">MX</span>";
                Cell(html, flightCell, nowrap: true);

                Cell(html, Encode(flight.Registration), nowrap: true);
                Cell(html, Encode(flight.To), nowrap: true);
                Cell(html, Encode(flight.ScheduledText), nowrap: true);
                Cell(html, Encode(flight.ActualText), nowrap: true);

                string delay = Encode(flight.ActualDelayText.Length > 0 ? flight.ActualDelayText : flight.CodedDelayText);
                Cell(html, flight.Reconciles == false ? "<b style=\"color:" + Alert + ";\">" + delay + "</b>" : "<b>" + delay + "</b>",
                     nowrap: true);

                Cell(html, string.Join("<br>", flight.Events.Select(e =>
                    "<b>" + Encode(e.Code) + "</b>&nbsp;" + Encode(e.Duration) + "&nbsp; " + Encode(e.Label))));

                List<string> owed = flight.OutstandingItems;
                Cell(html, owed.Count == 0
                    ? string.Empty
                    : "<span style=\"color:" + Alert + ";\">" + string.Join("<br>", owed.Select(Encode)) + "</span>");

                html.Append("</tr>");
            }

            html.Append("</table>");
        }

        private static void Cell(StringBuilder html, string content, bool nowrap = false)
        {
            html.Append("<td style=\"padding:3px 6px;border:1px solid ").Append(Rule).Append(";vertical-align:top;");
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
            if (model.ReportedFlights > 0) text.AppendLine(OutstandingLine(model));
            string? leftOut = LeftOutLine(model);
            if (leftOut != null) text.AppendLine(leftOut);

            foreach (ReportFlight flight in model.Flights)
            {
                text.AppendLine();
                string delay = flight.ActualDelayText.Length > 0 ? flight.ActualDelayText : flight.CodedDelayText;
                text.AppendLine(string.Join("  ", new[]
                {
                    flight.DateText, flight.FlightNumber + (flight.IsMxDelay ? " (MX)" : string.Empty),
                    flight.Registration, flight.To, "STD " + flight.ScheduledText, "ATD " + flight.ActualText,
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
