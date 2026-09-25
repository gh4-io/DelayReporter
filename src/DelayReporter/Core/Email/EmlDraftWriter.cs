using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DelayReporter.Core.Email
{
    /// <summary>
    /// Writes a draft as MIME (.eml) marked X-Unsent: 1, which Outlook (classic and new) and
    /// most mail apps open as an editable, unsent message. Ported from OFT Scrubber's writer,
    /// trimmed to what a report needs: plain text and HTML alternatives, and file attachments.
    ///
    /// Structure: multipart/mixed (attachments) around multipart/alternative (plain and HTML).
    /// The mixed level is left out when there is nothing attached.
    /// </summary>
    public static class EmlDraftWriter
    {
        private const string Crlf = "\r\n";

        public static byte[] ToBytes(EmailDraft draft, out List<string> notes)
        {
            notes = new List<string>();
            var mime = new StringBuilder();

            mime.Append("X-Unsent: 1").Append(Crlf);
            DateTimeOffset now = DateTimeOffset.Now;
            mime.Append("Date: ")
                .Append(now.ToString("ddd, dd MMM yyyy HH:mm:ss ", CultureInfo.InvariantCulture))
                .Append(now.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty))
                .Append(Crlf);
            // Some New Outlook builds refuse to save a draft without a Message-ID.
            mime.Append("Message-ID: <").Append(Guid.NewGuid().ToString("N")).Append("@delay-reporter.invalid>").Append(Crlf);
            AppendAddressHeader(mime, "To", draft.To, notes);
            AppendAddressHeader(mime, "Cc", draft.Cc, notes);
            mime.Append("Subject: ").Append(EncodeHeaderText(draft.Subject)).Append(Crlf);
            mime.Append("MIME-Version: 1.0").Append(Crlf);

            if (draft.Attachments.Count == 0)
            {
                AppendAlternative(mime, draft);
            }
            else
            {
                string boundary = NewBoundary("mixed");
                mime.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append('"').Append(Crlf).Append(Crlf);
                mime.Append("--").Append(boundary).Append(Crlf);
                AppendAlternative(mime, draft);

                foreach (EmailAttachment attachment in draft.Attachments)
                {
                    mime.Append(Crlf).Append("--").Append(boundary).Append(Crlf);
                    AppendAttachment(mime, attachment);
                }

                mime.Append(Crlf).Append("--").Append(boundary).Append("--").Append(Crlf);
            }

            return Encoding.ASCII.GetBytes(mime.ToString());
        }

        private static void AppendAlternative(StringBuilder mime, EmailDraft draft)
        {
            string boundary = NewBoundary("alt");
            mime.Append("Content-Type: multipart/alternative; boundary=\"").Append(boundary).Append('"').Append(Crlf).Append(Crlf);
            mime.Append("--").Append(boundary).Append(Crlf);
            AppendText(mime, "text/plain", draft.PlainText);
            mime.Append(Crlf).Append("--").Append(boundary).Append(Crlf);
            AppendText(mime, "text/html", draft.Html);
            mime.Append(Crlf).Append("--").Append(boundary).Append("--").Append(Crlf);
        }

        private static void AppendText(StringBuilder mime, string type, string text)
        {
            mime.Append("Content-Type: ").Append(type).Append("; charset=\"utf-8\"").Append(Crlf);
            mime.Append("Content-Transfer-Encoding: base64").Append(Crlf).Append(Crlf);
            AppendBase64(mime, Encoding.UTF8.GetBytes(text));
        }

        private static void AppendAttachment(StringBuilder mime, EmailAttachment attachment)
        {
            mime.Append("Content-Type: ").Append(attachment.MimeType).Append(';')
                .Append(FileNameParameter("name", attachment.FileName)).Append(Crlf);
            mime.Append("Content-Disposition: attachment;")
                .Append(FileNameParameter("filename", attachment.FileName)).Append(Crlf);
            mime.Append("Content-Transfer-Encoding: base64").Append(Crlf).Append(Crlf);
            AppendBase64(mime, attachment.Data);
        }

        /// <summary>
        /// ASCII names as a quoted string; anything else as an RFC 2231 extended parameter
        /// (name*=utf-8''...), never as encoded words inside quotes.
        /// </summary>
        private static string FileNameParameter(string parameter, string fileName)
        {
            string name = SafeHeaderValue(fileName);

            if (IsPlainAscii(name))
                return " " + parameter + "=\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            var encoded = new StringBuilder();
            foreach (byte b in Encoding.UTF8.GetBytes(name))
            {
                char c = (char)b;
                bool unreserved = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                                  (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
                if (unreserved) encoded.Append(c);
                else encoded.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return Crlf + " " + parameter + "*=utf-8''" + encoded;
        }

        private static void AppendAddressHeader(StringBuilder mime, string header, IEnumerable<string> addresses, List<string> notes)
        {
            List<string> parts = EmailDraft.PlainAddresses(addresses, header, notes).Select(a => "<" + a + ">").ToList();
            if (parts.Count > 0)
                mime.Append(header).Append(": ").Append(string.Join("," + Crlf + " ", parts)).Append(Crlf);
        }

        /// <summary>RFC 2047 encoded words for non-ASCII text, split so no line grows too long.</summary>
        private static string EncodeHeaderText(string text)
        {
            text = SafeHeaderValue(text);
            if (IsPlainAscii(text)) return text;

            var words = new List<string>();
            var chunk = new StringBuilder();
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string element = (string)elements.Current;
                if (Encoding.UTF8.GetByteCount(chunk + element) > 45)
                {
                    words.Add(EncodedWord(chunk.ToString()));
                    chunk.Clear();
                }
                chunk.Append(element);
            }

            if (chunk.Length > 0) words.Add(EncodedWord(chunk.ToString()));
            return string.Join(Crlf + " ", words);
        }

        private static string EncodedWord(string text) =>
            "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) + "?=";

        private static bool IsPlainAscii(string text) => text.All(c => c >= 0x20 && c < 0x7F);

        /// <summary>Header values must never carry line breaks: that would inject headers.</summary>
        private static string SafeHeaderValue(string? text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

        private static void AppendBase64(StringBuilder mime, byte[] data)
        {
            string encoded = Convert.ToBase64String(data);
            for (int i = 0; i < encoded.Length; i += 76)
                mime.Append(encoded, i, Math.Min(76, encoded.Length - i)).Append(Crlf);
        }

        private static string NewBoundary(string kind) => "=_delay_" + kind + "_" + Guid.NewGuid().ToString("N");
    }
}
