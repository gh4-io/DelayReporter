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
    }
}
