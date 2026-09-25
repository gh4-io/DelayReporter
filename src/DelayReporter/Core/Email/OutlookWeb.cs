using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DelayReporter.Core.Email
{
    /// <summary>Where Send email puts the draft.</summary>
    public enum EmailClient
    {
        /// <summary>An .eml draft opened in the default mail app, with the workbook attached.</summary>
        MailApp,

        /// <summary>A new message in Outlook on the web, for a work or school (Microsoft 365) account.</summary>
        OutlookWeb,

        /// <summary>A new message in Outlook.com, for a personal Microsoft account.</summary>
        OutlookCom,
    }

    /// <summary>
    /// Opens a draft in Outlook in the browser. Its compose link fills in the recipients and
    /// subject, but takes the body only as plain text and cannot carry an attachment, so the
    /// formatted table travels on the clipboard (see <see cref="ClipboardHtml"/>) and the
    /// workbook is saved for the user to drag into the message. Nothing is sent from here.
    /// </summary>
    public static class OutlookWeb
    {
        public const string WorkComposeUrl = "https://outlook.office.com/mail/deeplink/compose";
        public const string PersonalComposeUrl = "https://outlook.live.com/mail/0/deeplink/compose";

        /// <summary>
        /// The compose link for a draft. Addresses that are not plain addresses are left out and
        /// named in <paramref name="notes"/>, as the .eml writer does. The body is left empty for
        /// the formatted table to be pasted into.
        /// </summary>
        public static string ComposeUrl(EmailDraft draft, EmailClient client, List<string> notes)
        {
            string baseUrl = client == EmailClient.OutlookCom ? PersonalComposeUrl : WorkComposeUrl;
            var query = new List<string>();

            List<string> to = EmailDraft.PlainAddresses(draft.To, "To", notes);
            List<string> cc = EmailDraft.PlainAddresses(draft.Cc, "Cc", notes);
            if (to.Count > 0) query.Add("to=" + Uri.EscapeDataString(string.Join(";", to)));
            if (cc.Count > 0) query.Add("cc=" + Uri.EscapeDataString(string.Join(";", cc)));
            query.Add("subject=" + Uri.EscapeDataString(draft.Subject));

            return baseUrl + "?" + string.Join("&", query);
        }
    }

    /// <summary>
    /// The Windows clipboard's "HTML Format": a short header giving the byte offsets of the
    /// document and of the fragment to paste, then the document itself, all as UTF-8. Outlook
    /// on the web, Word and the Outlook apps paste it as formatted text.
    /// </summary>
    public static class ClipboardHtml
    {
        private const string StartMarker = "<!--StartFragment-->";
        private const string EndMarker = "<!--EndFragment-->";

        // Each offset is written as ten digits, so the header is the same length whatever it holds.
        private const string HeaderTemplate =
            "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

        private static readonly Regex Body = new Regex(
            "<body(?<attributes>[^>]*)>(?<content>.*)</body>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex StyleAttribute = new Regex(
            "style\\s*=\\s*\"(?<style>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Wraps an HTML document for the clipboard. Only the body's content is pasted, so the
        /// body's own style (the email's font and size) moves onto a div around it; without that
        /// the pasted table would take whatever font the message around it has.
        /// </summary>
        public static string Format(string document)
        {
            string fragment = document ?? string.Empty;
            Match body = Body.Match(fragment);
            if (body.Success)
            {
                Match style = StyleAttribute.Match(body.Groups["attributes"].Value);
                fragment = style.Success
                    ? "<div style=\"" + style.Groups["style"].Value + "\">" + body.Groups["content"].Value + "</div>"
                    : body.Groups["content"].Value;
            }

            string before = "<html><head><meta charset=\"utf-8\"></head><body>" + StartMarker;
            string after = EndMarker + "</body></html>";

            int headerLength = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, HeaderTemplate, 0, 0, 0, 0));
            int startHtml = headerLength;
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(before);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(after);

            return string.Format(CultureInfo.InvariantCulture, HeaderTemplate, startHtml, endHtml, startFragment, endFragment) +
                   before + fragment + after;
        }
    }
}
