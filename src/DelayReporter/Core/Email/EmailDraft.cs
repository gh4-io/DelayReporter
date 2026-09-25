using System;
using System.Collections.Generic;
using System.Linq;

namespace DelayReporter.Core.Email
{
    /// <summary>A file carried with the draft, such as the report workbook.</summary>
    public sealed class EmailAttachment
    {
        public EmailAttachment(string fileName, string mimeType, byte[] data)
        {
            FileName = fileName;
            MimeType = mimeType;
            Data = data;
        }

        public string FileName { get; }
        public string MimeType { get; }
        public byte[] Data { get; }
    }

    /// <summary>
    /// A message ready to be written as a draft. Nothing here sends mail: the draft opens in
    /// the user's mail app, where they review it and press Send themselves.
    /// </summary>
    public sealed class EmailDraft
    {
        public List<string> To { get; } = new List<string>();
        public List<string> Cc { get; } = new List<string>();
        public string Subject { get; set; } = string.Empty;
        public string Html { get; set; } = string.Empty;
        public string PlainText { get; set; } = string.Empty;
        public List<EmailAttachment> Attachments { get; } = new List<EmailAttachment>();

        /// <summary>Splits a typed address list on semicolons or commas, as Outlook accepts it.</summary>
        public static IEnumerable<string> SplitAddresses(string? text) =>
            (text ?? string.Empty)
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => a.Length > 0);

        /// <summary>
        /// The bare addresses, with anything that is not a plain address left out and named in
        /// <paramref name="notes"/> so the user knows to add it by hand. "Name &lt;address&gt;"
        /// is accepted and keeps only the address.
        /// </summary>
        public static List<string> PlainAddresses(IEnumerable<string> addresses, string header, List<string> notes)
        {
            var plain = new List<string>();
            foreach (string raw in addresses)
            {
                string address = raw.Trim();
                int open = address.LastIndexOf('<');
                int close = address.LastIndexOf('>');
                if (open >= 0 && close > open) address = address.Substring(open + 1, close - open - 1).Trim();

                if (address.IndexOf('@') < 1 || !address.All(c => c > 0x20 && c < 0x7F))
                {
                    notes.Add($"{header} address \"{raw.Trim()}\" is not a plain email address and was left out; add it in the mail app.");
                    continue;
                }
                plain.Add(address);
            }
            return plain;
        }
    }
}
