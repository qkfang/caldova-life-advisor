using System.Collections.Concurrent;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace CaldovaLife.App.Services;

/// <summary>
/// One generated application and disclosure summary PDF, kept in memory so
/// the <c>/underwriting/application/{id}.pdf</c> endpoint can stream it back.
/// </summary>
public record ApplicationPdfRecord(
    string Id,
    string FileName,
    byte[] Content,
    DateTimeOffset CreatedAt);

/// <summary>One cover printed on the summary.</summary>
public record ApplicationPdfCover(
    string LifeInsured,
    string Cover,
    string SumInsured,
    string Ownership,
    string Premium,
    string Terms,
    string? PolicyNumber);

/// <summary>One underwriting requirement printed on the summary.</summary>
public record ApplicationPdfRequirement(
    string LifeInsured,
    string Requirement,
    string Status,
    string Detail);

/// <summary>Input model for <see cref="ApplicationPdfService.Generate"/>.</summary>
public record ApplicationPdfInput(
    string CaseNumber,
    string Clients,
    string ClientNumbers,
    string RequestType,
    string Adviser,
    string ReferredBy,
    string Insurer,
    string InsurerContact,
    string LodgedOn,
    string AsAt,
    string ApplicationStatus,
    IReadOnlyList<ApplicationPdfCover> Covers,
    string PremiumSummary,
    IReadOnlyList<string> Ownership,
    IReadOnlyList<string> Disclosures,
    IReadOnlyList<ApplicationPdfRequirement> Requirements,
    string DutyOfCareText,
    string DutyOfCareAcknowledgement,
    string? DecisionSummary,
    string? CoolingOff,
    IReadOnlyList<string> Notes);

/// <summary>
/// Generates the "Caldova Life Advice — Application and disclosure summary"
/// PDF for a Client Case using PDFsharp + MigraDoc (pure managed,
/// cross-platform), and caches it in memory so the Underwriting &amp; New
/// Business page and the <c>underwriting_generateApplicationSummaryPdf</c>
/// MCP tool can hand out a download link. The summary records the covers,
/// ownership, requirements and the duty-of-care acknowledgement — it leaves
/// out health detail, which stays with the insurer.
/// </summary>
public class ApplicationPdfService
{
    private static readonly Color BrandNavy = Color.FromRgb(0x0f, 0x17, 0x2a);
    private static readonly Color BrandTeal = Color.FromRgb(0x0f, 0x76, 0x6e);
    private static readonly Color BrandGold = Color.FromRgb(0xc9, 0xa1, 0x4a);
    private static readonly Color TextDark = Color.FromRgb(0x1f, 0x29, 0x37);
    private static readonly Color TextMuted = Color.FromRgb(0x47, 0x55, 0x69);
    private static readonly Color TextSubtle = Color.FromRgb(0x64, 0x74, 0x8b);
    private static readonly Color PanelBg = Color.FromRgb(0xf8, 0xfa, 0xfc);
    private static readonly Color PanelBorder = Color.FromRgb(0xcb, 0xd5, 0xe1);
    private static readonly Color WarnBg = Color.FromRgb(0xfe, 0xf3, 0xc7);
    private static readonly Color WarnText = Color.FromRgb(0x92, 0x40, 0x0e);
    private static readonly Color OkBg = Color.FromRgb(0xd1, 0xfa, 0xe5);
    private static readonly Color OkText = Color.FromRgb(0x06, 0x5f, 0x46);
    private static readonly Color InfoBg = Color.FromRgb(0xe0, 0xf2, 0xfe);
    private static readonly Color InfoText = Color.FromRgb(0x07, 0x59, 0x85);

    private readonly ConcurrentDictionary<string, ApplicationPdfRecord> _store = new();

    // PDFsharp 6.x ships no default font resolver. Windows hosts use the
    // installed fonts; Linux / macOS hosts get a resolver that maps Arial to
    // whichever libre font is installed. Done once per process.
    static ApplicationPdfService()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        else if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new LinuxFontResolver();
        }
    }

    /// <summary>
    /// Generate an application and disclosure summary PDF and store it in
    /// memory. Returns the record so the caller can surface its <c>Id</c> in a
    /// download URL (<c>/underwriting/application/{id}.pdf</c>).
    /// </summary>
    public ApplicationPdfRecord Generate(ApplicationPdfInput input)
    {
        var id = Guid.NewGuid().ToString("N").Substring(0, 12);
        var safeCase = new string((input.CaseNumber ?? "case").Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        if (string.IsNullOrEmpty(safeCase)) safeCase = "case";
        var fileName = $"Caldova-ApplicationSummary-{safeCase}-{id}.pdf";

        var doc = BuildDocument(input, safeCase, id);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);

        var record = new ApplicationPdfRecord(id, fileName, ms.ToArray(), DateTimeOffset.UtcNow);
        _store[id] = record;

        // Bound the in-memory cache so a long-running session can't grow unbounded.
        if (_store.Count > 200)
        {
            foreach (var stale in _store.Values.OrderBy(r => r.CreatedAt).Take(_store.Count - 200))
                _store.TryRemove(stale.Id, out _);
        }

        return record;
    }

    public ApplicationPdfRecord? Get(string id)
        => _store.TryGetValue(id ?? string.Empty, out var record) ? record : null;

    private static Document BuildDocument(ApplicationPdfInput input, string safeCase, string id)
    {
        var doc = new Document();
        doc.Info.Title = $"Caldova Life Advice — Application and disclosure summary {safeCase}";
        doc.Info.Author = "Caldova Life Advice — Underwriting & New Business";
        doc.Info.Subject = "Application and disclosure summary";

        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9.5;
        normal.Font.Color = TextDark;

        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromPoint(36);
        section.PageSetup.RightMargin = Unit.FromPoint(36);
        section.PageSetup.TopMargin = Unit.FromPoint(110);
        section.PageSetup.BottomMargin = Unit.FromPoint(56);
        section.PageSetup.HeaderDistance = Unit.FromPoint(28);
        section.PageSetup.FooterDistance = Unit.FromPoint(20);

        BuildHeader(section, safeCase, id);
        BuildFooter(section);
        BuildBody(section, input);
        return doc;
    }

    private static void BuildHeader(Section section, string safeCase, string id)
    {
        var header = section.Headers.Primary;
        var table = header.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(9.6));
        table.AddColumn(Unit.FromCentimeter(8.8));
        var row = table.AddRow();

        var brand = row.Cells[0].AddParagraph("CALDOVA LIFE ADVICE");
        brand.Format.Font.Size = 16;
        brand.Format.Font.Bold = true;
        brand.Format.Font.Color = BrandNavy;
        var sub = row.Cells[0].AddParagraph("Underwriting & New Business");
        sub.Format.Font.Size = 9;
        sub.Format.Font.Color = TextMuted;

        var title = row.Cells[1].AddParagraph("APPLICATION AND DISCLOSURE SUMMARY");
        title.Format.Alignment = ParagraphAlignment.Right;
        title.Format.Font.Size = 11;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = BrandTeal;
        var issued = row.Cells[1].AddParagraph($"Issued: {DateTimeOffset.Now:ddd d MMM yyyy}");
        issued.Format.Alignment = ParagraphAlignment.Right;
        issued.Format.Font.Size = 9;
        issued.Format.Font.Color = TextMuted;
        var reference = row.Cells[1].AddParagraph($"Reference: APP-{safeCase}-{id}");
        reference.Format.Alignment = ParagraphAlignment.Right;
        reference.Format.Font.Size = 9;
        reference.Format.Font.Color = TextMuted;

        var rule = header.AddParagraph();
        rule.Format.Borders.Bottom.Color = BrandGold;
        rule.Format.Borders.Bottom.Width = 1;
        rule.Format.SpaceBefore = Unit.FromPoint(6);
        rule.Format.SpaceAfter = Unit.FromPoint(6);
    }

    private static void BuildFooter(Section section)
    {
        var footer = section.Footers.Primary;
        var p = footer.AddParagraph("Caldova Life Advice · Underwriting & New Business · Caldova House, Melbourne VIC · Contains personal information — keep it confidential");
        p.Format.Alignment = ParagraphAlignment.Center;
        p.Format.Font.Size = 7.5;
        p.Format.Font.Color = TextSubtle;
        var p2 = footer.AddParagraph();
        p2.Format.Alignment = ParagraphAlignment.Center;
        p2.Format.Font.Size = 7.5;
        p2.Format.Font.Color = TextSubtle;
        p2.AddText("Advice Team in a Day — fictional people, insurers and figures · Page ");
        p2.AddPageField();
        p2.AddText(" of ");
        p2.AddNumPagesField();
    }

    private static void BuildBody(Section section, ApplicationPdfInput input)
    {
        AddStatusBanner(section, input.ApplicationStatus);

        AddSectionHeading(section, "Client and application");
        var details = section.AddTable();
        details.Borders.Visible = false;
        details.AddColumn(Unit.FromPoint(130));
        details.AddColumn(Unit.FromPoint(393));
        AddKeyValueRow(details, "Case number", input.CaseNumber);
        AddKeyValueRow(details, "Client", input.Clients);
        AddKeyValueRow(details, "Client number", input.ClientNumbers);
        AddKeyValueRow(details, "Request", input.RequestType);
        AddKeyValueRow(details, "Adviser", input.Adviser);
        AddKeyValueRow(details, "Referred by", input.ReferredBy);
        AddKeyValueRow(details, "Insurer", input.Insurer);
        AddKeyValueRow(details, "Insurer contact", input.InsurerContact);
        AddKeyValueRow(details, "Application lodged", input.LodgedOn);
        AddKeyValueRow(details, "As at", input.AsAt);

        AddSectionHeading(section, "Covers and sums insured");
        if (input.Covers.Count == 0)
        {
            AddPanelParagraph(section, "No covers have been applied for yet — they come from the signed Statement of Advice.");
        }
        else
        {
            var covers = section.AddTable();
            covers.Borders.Width = 0.5;
            covers.Borders.Color = PanelBorder;
            covers.Format.Font.Size = 8.3;
            covers.AddColumn(Unit.FromPoint(70));
            covers.AddColumn(Unit.FromPoint(118));
            covers.AddColumn(Unit.FromPoint(66));
            covers.AddColumn(Unit.FromPoint(110));
            covers.AddColumn(Unit.FromPoint(52));
            covers.AddColumn(Unit.FromPoint(107));

            var head = covers.AddRow();
            head.HeadingFormat = true;
            head.Shading.Color = PanelBg;
            var headings = new[] { "Life insured", "Cover", "Sum insured", "Ownership", "Premium (yr 1)", "Terms" };
            for (var i = 0; i < headings.Length; i++)
            {
                var hp = head.Cells[i].AddParagraph(headings[i]);
                hp.Format.Font.Bold = true;
                if (i is 2 or 4) hp.Format.Alignment = ParagraphAlignment.Right;
            }

            foreach (var cover in input.Covers)
            {
                var row = covers.AddRow();
                row.TopPadding = Unit.FromPoint(3);
                row.BottomPadding = Unit.FromPoint(3);
                row.Cells[0].AddParagraph(cover.LifeInsured).Format.Font.Bold = true;
                row.Cells[1].AddParagraph(cover.Cover);
                if (!string.IsNullOrWhiteSpace(cover.PolicyNumber))
                    row.Cells[1].AddParagraph($"Policy {cover.PolicyNumber}").Format.Font.Color = TextMuted;
                row.Cells[2].AddParagraph(cover.SumInsured).Format.Alignment = ParagraphAlignment.Right;
                row.Cells[3].AddParagraph(cover.Ownership);
                row.Cells[4].AddParagraph(cover.Premium).Format.Alignment = ParagraphAlignment.Right;
                var (bg, fg) = TermsTone(cover.Terms);
                row.Cells[5].Shading.Color = bg;
                var terms = row.Cells[5].AddParagraph(cover.Terms);
                terms.Format.Font.Color = fg;
                terms.Format.Font.Bold = true;
            }
        }
        if (!string.IsNullOrWhiteSpace(input.PremiumSummary))
            AddPanelParagraph(section, input.PremiumSummary);

        if (input.Ownership.Count > 0)
        {
            AddSectionHeading(section, "Ownership");
            foreach (var line in input.Ownership)
                AddBullet(section, line);
        }

        AddSectionHeading(section, "Disclosures (summary)");
        foreach (var line in input.Disclosures)
            AddBullet(section, line);
        var privacy = section.AddParagraph("This summary records what was disclosed, not the health detail. The full answers are held by the insurer and shared only with the people who need them.");
        privacy.Format.Font.Size = 8.5;
        privacy.Format.Font.Italic = true;
        privacy.Format.Font.Color = TextMuted;
        privacy.Format.SpaceBefore = Unit.FromPoint(3);

        AddSectionHeading(section, "Underwriting requirements and status");
        if (input.Requirements.Count == 0)
        {
            AddPanelParagraph(section, "Requirements are confirmed with the insurer once the application is prepared.");
        }
        else
        {
            var reqs = section.AddTable();
            reqs.Borders.Width = 0.5;
            reqs.Borders.Color = PanelBorder;
            reqs.Format.Font.Size = 8.3;
            reqs.AddColumn(Unit.FromPoint(85));
            reqs.AddColumn(Unit.FromPoint(150));
            reqs.AddColumn(Unit.FromPoint(70));
            reqs.AddColumn(Unit.FromPoint(218));
            var head = reqs.AddRow();
            head.HeadingFormat = true;
            head.Shading.Color = PanelBg;
            foreach (var (text, i) in new[] { "Life insured", "Requirement", "Status", "Detail" }.Select((t, i) => (t, i)))
                head.Cells[i].AddParagraph(text).Format.Font.Bold = true;
            foreach (var req in input.Requirements)
            {
                var row = reqs.AddRow();
                row.TopPadding = Unit.FromPoint(2);
                row.BottomPadding = Unit.FromPoint(2);
                row.Cells[0].AddParagraph(req.LifeInsured);
                row.Cells[1].AddParagraph(req.Requirement);
                var (bg, fg) = StatusTone(req.Status);
                row.Cells[2].Shading.Color = bg;
                var st = row.Cells[2].AddParagraph(req.Status.ToUpperInvariant());
                st.Format.Font.Bold = true;
                st.Format.Font.Color = fg;
                row.Cells[3].AddParagraph(req.Detail);
            }
        }

        AddSectionHeading(section, "Duty to take reasonable care — acknowledgement");
        AddCalloutParagraph(section, input.DutyOfCareText, InfoBg, InfoText);
        var ack = section.AddParagraph(input.DutyOfCareAcknowledgement);
        ack.Format.Font.Bold = true;
        ack.Format.SpaceBefore = Unit.FromPoint(2);

        if (!string.IsNullOrWhiteSpace(input.DecisionSummary) || !string.IsNullOrWhiteSpace(input.CoolingOff))
        {
            AddSectionHeading(section, "Decision and cooling-off");
            if (!string.IsNullOrWhiteSpace(input.DecisionSummary))
                AddBullet(section, input.DecisionSummary!);
            if (!string.IsNullOrWhiteSpace(input.CoolingOff))
                AddBullet(section, input.CoolingOff!);
        }

        AddSectionHeading(section, "Important");
        AddBullet(section, "Existing cover is never cancelled or reduced before the new cover is accepted and in force — that decision is the client's, with the adviser.");
        AddBullet(section, "Accepting a loading, an exclusion or a decline is the client's decision with the adviser. Caldova's agents never accept terms for a client.");
        foreach (var note in input.Notes)
            AddBullet(section, note);
    }

    private static (Color Background, Color Text) TermsTone(string terms)
    {
        var t = terms.ToUpperInvariant();
        if (t.StartsWith("STANDARD", StringComparison.Ordinal)) return (OkBg, OkText);
        if (t.Contains("LOADING", StringComparison.Ordinal) || t.Contains("EXCLUSION", StringComparison.Ordinal) || t.Contains("DECLINE", StringComparison.Ordinal))
            return (WarnBg, WarnText);
        return (PanelBg, TextMuted);
    }

    private static (Color Background, Color Text) StatusTone(string status)
    {
        var s = status.ToUpperInvariant();
        if (s.StartsWith("COMPLETE", StringComparison.Ordinal) || s.StartsWith("ACCEPTED", StringComparison.Ordinal) || s.StartsWith("DECISION", StringComparison.Ordinal))
            return (OkBg, OkText);
        if (s.StartsWith("NOT REQUIRED", StringComparison.Ordinal)) return (PanelBg, TextMuted);
        if (s.StartsWith("REQUESTED", StringComparison.Ordinal) || s.StartsWith("AWAITING", StringComparison.Ordinal) || s.StartsWith("PENDING", StringComparison.Ordinal))
            return (WarnBg, WarnText);
        return (InfoBg, InfoText);
    }

    private static void AddStatusBanner(Section section, string status)
    {
        var s = (status ?? string.Empty).ToUpperInvariant();
        var (bg, fg) = s.StartsWith("ACCEPTED", StringComparison.Ordinal) || s.Contains("IN FORCE", StringComparison.Ordinal)
            ? (OkBg, OkText)
            : s.StartsWith("NOT", StringComparison.Ordinal)
                ? (PanelBg, TextMuted)
                : (WarnBg, WarnText);

        var banner = section.AddTable();
        banner.Borders.Visible = false;
        banner.AddColumn(Unit.FromPoint(523));
        var row = banner.AddRow();
        row.Shading.Color = bg;
        row.TopPadding = Unit.FromPoint(6);
        row.BottomPadding = Unit.FromPoint(6);
        var p = row.Cells[0].AddParagraph();
        p.Format.LeftIndent = Unit.FromPoint(6);
        var label = p.AddFormattedText("APPLICATION STATUS:  ");
        label.Font.Bold = true;
        label.Font.Color = fg;
        var value = p.AddFormattedText(string.IsNullOrWhiteSpace(status) ? "—" : status);
        value.Font.Bold = true;
        value.Font.Size = 10.5;
        value.Font.Color = fg;
    }

    private static void AddSectionHeading(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Size = 11.5;
        p.Format.Font.Bold = true;
        p.Format.Font.Color = BrandTeal;
        p.Format.SpaceBefore = Unit.FromPoint(10);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        p.Format.KeepWithNext = true;
    }

    private static void AddKeyValueRow(Table table, string key, string? value)
    {
        var row = table.AddRow();
        var k = row.Cells[0].AddParagraph(key);
        k.Format.Font.Bold = true;
        k.Format.Font.Color = TextMuted;
        row.Cells[1].AddParagraph(string.IsNullOrWhiteSpace(value) ? "—" : value!);
        row.TopPadding = Unit.FromPoint(2);
        row.BottomPadding = Unit.FromPoint(2);
    }

    private static void AddBullet(Section section, string text)
    {
        var p = section.AddParagraph($"•  {text}");
        p.Format.LeftIndent = Unit.FromPoint(10);
        p.Format.FirstLineIndent = Unit.FromPoint(-8);
        p.Format.SpaceAfter = Unit.FromPoint(2);
    }

    private static void AddPanelParagraph(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Shading.Color = PanelBg;
        p.Format.LeftIndent = Unit.FromPoint(8);
        p.Format.RightIndent = Unit.FromPoint(8);
        p.Format.SpaceBefore = Unit.FromPoint(3);
        p.Format.SpaceAfter = Unit.FromPoint(3);
        p.Format.Borders.Distance = Unit.FromPoint(4);
    }

    private static void AddCalloutParagraph(Section section, string text, Color background, Color foreground)
    {
        var p = section.AddParagraph(text);
        p.Format.Shading.Color = background;
        p.Format.Font.Color = foreground;
        p.Format.LeftIndent = Unit.FromPoint(8);
        p.Format.RightIndent = Unit.FromPoint(8);
        p.Format.SpaceBefore = Unit.FromPoint(2);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        p.Format.Borders.Distance = Unit.FromPoint(4);
    }
}
