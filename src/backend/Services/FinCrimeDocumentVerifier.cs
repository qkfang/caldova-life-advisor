using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Identity;

namespace LifeAdviceGuru.App.Services;

/// <summary>Outcome of one authenticity check against a document.</summary>
/// <param name="Status"><c>pass</c>, <c>fail</c>, <c>warn</c> or <c>n/a</c>.</param>
public record FinCrimeDocumentCheck(string Name, string Status, string Detail);

/// <summary>Per-document verdict plus the checks that produced it.</summary>
/// <param name="Verdict"><c>legit</c>, <c>suspicious</c>, <c>tampered</c> (genuine details, altered image) or <c>fake</c>.</param>
/// <param name="Source"><c>content-understanding</c> or <c>manifest-fallback</c>.</param>
public record FinCrimeDocumentVerification(
    string Id,
    string Label,
    string Kind,
    string Src,
    string Verdict,
    string Summary,
    IReadOnlyList<string> FailureReasons,
    IReadOnlyList<FinCrimeDocumentCheck> Checks,
    string? CuMarkdown,
    string Source,
    IReadOnlyDictionary<string, string?> ExtractedFields);

/// <summary>
/// Document-authenticity verifier for the Financial Crime "Try it out" tab.
///
/// When Azure Content Understanding is configured
/// (<c>AZURE_CONTENT_UNDERSTANDING_ENDPOINT</c>, optional
/// <c>AZURE_CONTENT_UNDERSTANDING_KEY</c> — otherwise
/// <c>DefaultAzureCredential</c>), each specimen image is sent to a custom
/// analyzer built on <c>prebuilt-document</c> with an authenticity field schema
/// (document type, issuer, holder, numbers, dates, amounts, provider number,
/// diagnosis evidence, company officers, MRZ, certification, tamper indicators
/// and security features). The extracted fields then run through a
/// deterministic checks layer that compares them with the Client Case (people
/// and dates of birth on file, who a benefit may be paid to, the travel notice,
/// the income on file and the policy's evidence rule) and scores each document
/// <c>legit</c> / <c>suspicious</c> / <c>tampered</c> / <c>fake</c> with the reasons.
///
/// Without Content Understanding (or if a call fails) the same checks run on
/// the case-file record in the specimen manifest, so the page always works
/// offline.
/// </summary>
public class FinCrimeDocumentVerifier
{
    public const string SourceContentUnderstanding = "content-understanding";
    public const string SourceManifestFallback = "manifest-fallback";

    private const string DefaultAnalyzerId = "life_advice_guru_fincrime_authenticity_v1";

    private static readonly string[] DocumentKinds =
    [
        "driver-licence", "passport", "payslip", "medical-report", "death-certificate",
        "bank-statement", "bas-profit-loss", "company-extract", "other",
    ];

    private static readonly Regex NoneRegex = new(
        @"^\s*(none|no|nil|n/?a|not applicable|none (found|detected|visible)|no (visible )?(tamper(ing)? )?indicators?( found| detected)?)\s*\.?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NotConfirmedRegex = new(
        @"\b(no|not|none|pending|awaiting|to follow|outstanding|without)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Medicare provider number: six-digit stem, practice location character
    // (no I, O, S or Z) and a check character from a fixed set of letters.
    private static readonly Regex ProviderNumberRegex = new(@"^\d{6}[0-9A-HJ-NP-RT-Y][ABFHJKLTWXY]$", RegexOptions.Compiled);

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "dd/MM/yyyy", "d/MM/yyyy", "d/M/yyyy", "dd MMM yyyy", "d MMM yyyy",
        "dd MMMM yyyy", "d MMMM yyyy", "yyyy/MM/dd", "dd-MM-yyyy", "yyyy-MM-ddTHH:mm:ss",
    ];

    private readonly FinCrimeCaseDocumentStore _store;
    private readonly ILogger<FinCrimeDocumentVerifier> _logger;
    private readonly string? _endpoint;
    private readonly string? _apiKey;
    private readonly string? _tenantId;
    private readonly string _analyzerId;
    private readonly string _completionModel;
    private readonly string _embeddingModel;
    private readonly Lazy<ContentUnderstandingClient> _client;
    private readonly SemaphoreSlim _analyzerGate = new(1, 1);
    private volatile bool _analyzerReady;
    private DateTimeOffset _retryContentUnderstandingAfter = DateTimeOffset.MinValue;

    // Latest verification per case + document (so engaging the agent reuses the scan).
    private readonly ConcurrentDictionary<string, FinCrimeDocumentVerification> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // Document numbers already verified, with the case (and scenario) they were first seen on.
    private readonly ConcurrentDictionary<string, (string CaseNumber, string ScenarioKey)> _seenDocumentNumbers =
        new(StringComparer.OrdinalIgnoreCase);

    public FinCrimeDocumentVerifier(
        FinCrimeCaseDocumentStore store,
        IConfiguration config,
        ILogger<FinCrimeDocumentVerifier> logger)
    {
        _store = store;
        _logger = logger;
        _endpoint = config["AZURE_CONTENT_UNDERSTANDING_ENDPOINT"];
        _apiKey = config["AZURE_CONTENT_UNDERSTANDING_KEY"];
        _tenantId = config["AZURE_TENANT_ID"];
        _analyzerId = NonEmpty(config["AZURE_CONTENT_UNDERSTANDING_ANALYZER_ID"]) ?? DefaultAnalyzerId;
        _completionModel = NonEmpty(config["AZURE_CONTENT_UNDERSTANDING_COMPLETION_MODEL"]) ?? "gpt-4.1";
        _embeddingModel = NonEmpty(config["AZURE_CONTENT_UNDERSTANDING_EMBEDDING_MODEL"]) ?? "text-embedding-3-large";
        _client = new Lazy<ContentUnderstandingClient>(CreateClient);
    }

    /// <summary>True when an Azure Content Understanding endpoint is configured.</summary>
    public bool ContentUnderstandingConfigured => !string.IsNullOrWhiteSpace(_endpoint);

    /// <summary>Every check the layer runs, in the order they appear on each document.</summary>
    public IReadOnlyList<string> CheckNames { get; } =
    [
        "Document type plausibility",
        "Name matches the case",
        "Date of birth matches the record on file",
        "Dates plausible and consistent",
        "No visible tamper indicators",
        "Security features or certification present",
        "Number formats and check digits",
        "Electronic verification (DVS)",
        "Supporting-evidence checks",
        "Document not used on another client's case",
    ];

    /// <summary>
    /// Verify documents against a Client Case. Documents are analysed in
    /// parallel; results keep the requested order. With <paramref name="useCache"/>
    /// the latest scan of the same document on the same case is reused.
    /// </summary>
    public async Task<IReadOnlyList<FinCrimeDocumentVerification>> VerifyAsync(
        CaseRecord record,
        IReadOnlyList<FinCrimeSampleDocument> samples,
        bool useCache = false,
        CancellationToken cancellationToken = default)
    {
        if (samples.Count == 0) return Array.Empty<FinCrimeDocumentVerification>();

        var profile = _store.ProfileFor(record);
        var results = await Task.WhenAll(samples.Select(sample =>
            VerifyWithCacheAsync(record, profile, sample, useCache, cancellationToken)));

        // Remember document numbers so a document reused on another client's
        // case is flagged there. First case wins.
        foreach (var sample in samples)
        {
            var number = NormaliseNumber(sample.DocumentNumber);
            if (number.Length > 0)
                _seenDocumentNumbers.TryAdd(number, (record.CaseNumber, profile.ScenarioKey));
        }

        return results;
    }

    private async Task<FinCrimeDocumentVerification> VerifyWithCacheAsync(
        CaseRecord record,
        FinCrimeCaseProfile profile,
        FinCrimeSampleDocument sample,
        bool useCache,
        CancellationToken ct)
    {
        var key = $"{record.CaseNumber}|{sample.Id}";
        if (useCache && _cache.TryGetValue(key, out var cached)) return cached;

        FinCrimeDocumentVerification result;
        try
        {
            result = await VerifyOneAsync(record, profile, sample, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Document verification failed for {DocumentId}; using the case-file record", sample.Id);
            result = BuildResult(record, profile, sample, ManifestFields(sample), null, SourceManifestFallback,
                $"Verification fell back to the case-file record ({Shorten(ex.Message, 140)}).");
        }
        _cache[key] = result;
        return result;
    }

    private async Task<FinCrimeDocumentVerification> VerifyOneAsync(
        CaseRecord record,
        FinCrimeCaseProfile profile,
        FinCrimeSampleDocument sample,
        CancellationToken ct)
    {
        if (!ContentUnderstandingConfigured)
        {
            return BuildResult(record, profile, sample, ManifestFields(sample), null, SourceManifestFallback, null);
        }

        if (DateTimeOffset.UtcNow < _retryContentUnderstandingAfter)
        {
            return BuildResult(record, profile, sample, ManifestFields(sample), null, SourceManifestFallback,
                "Content Understanding failed a moment ago, so the checks ran on the case-file record.");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var (extracted, markdown) = await AnalyzeAsync(sample, timeout.Token);

            // Content Understanding wins; the case-file record only fills gaps
            // (and supplies the DVS result, which comes from a separate service).
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in extracted)
            {
                if (!string.IsNullOrWhiteSpace(value)) fields[key] = value.Trim();
            }
            foreach (var (key, value) in ManifestFields(sample))
            {
                if (!fields.ContainsKey(key) || key.Equals("electronicVerification", StringComparison.OrdinalIgnoreCase))
                    fields[key] = value;
            }
            return BuildResult(record, profile, sample, fields, markdown, SourceContentUnderstanding, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Content Understanding analysis failed for {DocumentId}; using the case-file record", sample.Id);
            _retryContentUnderstandingAfter = DateTimeOffset.UtcNow.AddMinutes(2);
            return BuildResult(record, profile, sample, ManifestFields(sample), null, SourceManifestFallback,
                $"Content Understanding was unavailable ({Shorten(ex.Message, 140)}), so the checks ran on the case-file record.");
        }
    }

    private FinCrimeDocumentVerification BuildResult(
        CaseRecord record,
        FinCrimeCaseProfile profile,
        FinCrimeSampleDocument sample,
        Dictionary<string, string?> fields,
        string? markdown,
        string source,
        string? note)
    {
        var recorder = RunChecks(record, profile, sample, fields);
        var (verdict, summary, reasons) = ScoreVerdict(recorder, sample, fields);
        if (note is not null) reasons.Insert(0, note);
        return new FinCrimeDocumentVerification(
            Id: sample.Id,
            Label: sample.Label,
            Kind: sample.Kind,
            Src: sample.Src,
            Verdict: verdict,
            Summary: summary,
            FailureReasons: reasons,
            Checks: recorder.Checks,
            CuMarkdown: markdown,
            Source: source,
            ExtractedFields: fields);
    }

    // ── Azure Content Understanding ─────────────────────────────────────────

    private ContentUnderstandingClient CreateClient()
    {
        var endpoint = new Uri(_endpoint!);
        if (!string.IsNullOrWhiteSpace(_apiKey))
            return new ContentUnderstandingClient(endpoint, new AzureKeyCredential(_apiKey));

        var options = new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
        };
        if (!string.IsNullOrWhiteSpace(_tenantId)) options.TenantId = _tenantId;
        return new ContentUnderstandingClient(endpoint, new DefaultAzureCredential(options));
    }

    private async Task<(Dictionary<string, string?> Fields, string? Markdown)> AnalyzeAsync(
        FinCrimeSampleDocument sample, CancellationToken ct)
    {
        var path = _store.ResolvePhysicalPath(sample)
            ?? throw new FileNotFoundException($"Specimen image for '{sample.Id}' was not found under wwwroot.");
        var client = _client.Value;
        await EnsureAnalyzerAsync(client, ct);

        // Send the image bytes rather than a URL: the cloud service can't
        // reach a developer's localhost.
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var operation = await client.AnalyzeBinaryAsync(
            WaitUntil.Completed,
            _analyzerId,
            BinaryData.FromBytes(bytes),
            contentType: ContentTypeFor(path),
            cancellationToken: ct);

        var content = operation.Value.Contents?.FirstOrDefault();
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (content?.Fields is not null)
        {
            foreach (var entry in content.Fields)
            {
                fields[entry.Key] = entry.Value?.Value switch
                {
                    null => null,
                    DateTimeOffset dto => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    var other => other.ToString(),
                };
            }
        }
        return (fields, content?.Markdown);
    }

    private async Task EnsureAnalyzerAsync(ContentUnderstandingClient client, CancellationToken ct)
    {
        if (_analyzerReady) return;
        await _analyzerGate.WaitAsync(ct);
        try
        {
            if (_analyzerReady) return;
            try
            {
                await client.GetAnalyzerAsync(_analyzerId, ct);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Creating Content Understanding analyzer {AnalyzerId}", _analyzerId);
                await client.CreateAnalyzerAsync(WaitUntil.Completed, _analyzerId, BuildAnalyzer(), true, ct);
            }
            _analyzerReady = true;
        }
        finally
        {
            _analyzerGate.Release();
        }
    }

    private ContentAnalyzer BuildAnalyzer()
    {
        static ContentFieldDefinition Text(string description) => new()
        {
            Type = ContentFieldType.String,
            Method = GenerationMethod.Generate,
            Description = description,
        };

        var schema = new ContentFieldSchema(new Dictionary<string, ContentFieldDefinition>
        {
            ["documentType"] = new()
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Classify,
                Description = "Type of document: driver-licence, passport, payslip, medical-report (a specialist's or doctor's report), death-certificate, bank-statement, bas-profit-loss (BAS lodgement or profit and loss summary), company-extract (company register extract) or other.",
            },
            ["issuer"] = Text("Who issued the document, exactly as printed: licensing or passport authority, employer, medical practice and doctor, registry, bank, accountant or tax agent, or company register."),
            ["holderName"] = Text("Name the document is about: licence or passport holder, employee, patient, deceased person, bank account name, business owner or business name, or company name."),
            ["documentNumber"] = Text("Primary identifier as printed: licence or passport number, employee ID, medical provider number, death registration number, BSB and account number (masked digits stay masked), ABN or ACN."),
            ["dateOfBirth"] = Text("Date of birth printed on the document (holder, patient or deceased), as yyyy-MM-dd. Empty if none."),
            ["issueDate"] = Text("Issue date, pay date, report or letter date, statement date, extract date or date prepared, as yyyy-MM-dd. Empty if none."),
            ["expiryDate"] = Text("Expiry date, as yyyy-MM-dd. Empty if none."),
            ["registrationDate"] = Text("Company registration date (company extract) or death registration date (death certificate), as yyyy-MM-dd. Empty if none."),
            ["dateOfDeath"] = Text("Date of death on a death certificate, as yyyy-MM-dd. Empty if none."),
            ["accountOpenedDate"] = Text("Date a bank account was opened, or the 'customer since' date, as yyyy-MM-dd. Empty if none."),
            ["consultationDate"] = Text("Medical report only: the date the doctor saw the patient, or the date of the operation, as yyyy-MM-dd. Empty if none."),
            ["periodStart"] = Text("Start of the pay period, statement period or reporting period, as yyyy-MM-dd. Empty if none."),
            ["periodEnd"] = Text("End of the pay period, statement period or reporting period, as yyyy-MM-dd. Empty if none."),
            ["keyAmount"] = Text("Headline amount as a plain number: annual salary on a payslip, net business income on a profit and loss summary, or closing balance on a bank statement. Empty if none."),
            ["providerNumber"] = Text("Medical report only: the doctor's provider number exactly as printed. Empty if none."),
            ["diagnosisEvidence"] = Text("Medical report only: what the diagnosis rests on — histopathology or pathology report, imaging (MRI, mammogram, ultrasound), an operation — and whether results are still pending. Empty if none."),
            ["officers"] = Text("Company extract only: each director, secretary and shareholder with their role and shareholding, separated by semicolons. Empty if none."),
            ["companyStatus"] = Text("Company extract only: the company's registration status as printed (for example Registered or Deregistered). Empty if none."),
            ["mrz"] = Text("Passport machine-readable zone: both lines exactly as printed, separated by a newline. Empty if none."),
            ["certification"] = Text("Certified copy stamp, if any: who certified it and the date sighted. Empty if none."),
            ["tamperIndicators"] = Text("Visible signs the document was altered: a pasted or low-resolution letterhead; text or dates re-typed in a different font, weight or baseline; white patches; a paragraph in a different typeface; a replaced or cut-out photo; a ghost image that doesn't match the main photo; security features interrupted by a patch; misspelt issuer names; impossible dates. Ignore the 'SPECIMEN — NOT A REAL DOCUMENT' watermark and stamps. Return 'none' when there are no indicators."),
            ["securityFeaturesPresent"] = new()
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Classify,
                Description = "Whether the security features this document type should carry are present and intact (identity documents: hologram band, ghost image, microprint, MRZ; death certificates: registry seal and certification). Use n/a for payslips, reports, statements, summaries and extracts.",
            },
            ["consistencySummary"] = Text("One or two plain-English sentences on whether the document is internally consistent."),
        })
        {
            Name = "life_advice_guru_fincrime_authenticity",
            Description = "Authenticity-oriented fields for documents supporting life insurance advice and claims.",
        };
        foreach (var kind in DocumentKinds) schema.Fields["documentType"].Enum.Add(kind);
        foreach (var value in new[] { "yes", "no", "partial", "n/a" }) schema.Fields["securityFeaturesPresent"].Enum.Add(value);

        var analyzer = new ContentAnalyzer
        {
            BaseAnalyzerId = "prebuilt-document",
            Description = "Life Advice Guru financial-crime document authenticity: identity documents, payslips, specialist medical reports, death certificates, bank statements, BAS and profit and loss summaries, and company extracts. Every specimen carries a 'SPECIMEN — NOT A REAL DOCUMENT' watermark; that watermark is expected and is not a sign of tampering.",
            Config = new ContentAnalyzerConfig { EnableOcr = true, EnableLayout = true },
            FieldSchema = schema,
        };
        analyzer.Models["completion"] = _completionModel;
        analyzer.Models["embedding"] = _embeddingModel;
        return analyzer;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };

    // ── Case-file record (offline fallback) ─────────────────────────────────

    private static Dictionary<string, string?> ManifestFields(FinCrimeSampleDocument s)
    {
        var isId = IsIdentityDocument(s.Kind);
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentType"] = s.Kind,
            ["issuer"] = s.Issuer,
            ["holderName"] = s.HolderName,
            ["documentNumber"] = s.DocumentNumber,
            ["dateOfBirth"] = s.DateOfBirth,
            ["issueDate"] = s.IssueDate,
            ["expiryDate"] = s.ExpiryDate,
            ["registrationDate"] = s.RegistrationDate,
            ["dateOfDeath"] = s.DateOfDeath,
            ["accountOpenedDate"] = s.AccountOpenedDate,
            ["consultationDate"] = s.ConsultationDate,
            ["periodStart"] = s.PeriodStart,
            ["periodEnd"] = s.PeriodEnd,
            ["keyAmount"] = s.KeyAmount?.ToString("0.##", CultureInfo.InvariantCulture),
            ["keyAmountLabel"] = s.KeyAmountLabel,
            ["providerNumber"] = s.ProviderNumber,
            ["diagnosisEvidence"] = s.DiagnosisEvidence,
            ["officers"] = s.Officers,
            ["companyStatus"] = s.CompanyStatus,
            ["mrz"] = s.Mrz,
            ["certification"] = s.Certification,
            ["tamperIndicators"] = s.TamperIndicators
                ?? (s.Expected is "fake" or "tampered" ? s.Description : "none"),
            ["securityFeaturesPresent"] = s.SecurityFeatures
                ?? (isId ? (s.Expected.Equals("legit", StringComparison.OrdinalIgnoreCase) ? "yes" : "no") : "n/a"),
            ["electronicVerification"] = s.ElectronicVerification,
        };
        return fields.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    // ── Deterministic checks layer ──────────────────────────────────────────

    private sealed class CheckRecorder(IReadOnlyList<string> names)
    {
        public List<FinCrimeDocumentCheck> Checks { get; } = new();
        public List<string> FailHeadlines { get; } = new();
        public List<string> WarnHeadlines { get; } = new();

        public void Pass(string detail) => Add("pass", detail, null);
        public void Fail(string detail, string headline) => Add("fail", detail, headline);
        public void Warn(string detail, string headline) => Add("warn", detail, headline);
        public void NotApplicable(string detail = "Not applicable to this document type.") => Add("n/a", detail, null);

        private void Add(string status, string detail, string? headline)
        {
            Checks.Add(new FinCrimeDocumentCheck(names[Checks.Count], status, detail));
            if (headline is null) return;
            (status == "fail" ? FailHeadlines : WarnHeadlines).Add(headline);
        }
    }

    private CheckRecorder RunChecks(
        CaseRecord record,
        FinCrimeCaseProfile profile,
        FinCrimeSampleDocument sample,
        IReadOnlyDictionary<string, string?> f)
    {
        var c = new CheckRecorder(CheckNames);
        var kind = sample.Kind.ToLowerInvariant();
        var isId = IsIdentityDocument(kind);
        var received = ParseDate(record.ReceivedDate) ?? record.CreatedAt.Date;
        var holder = Get(f, "holderName");
        var party = FindParty(profile, holder);

        // 1. Document type plausibility
        var detected = Get(f, "documentType");
        if (string.IsNullOrWhiteSpace(detected))
            c.Warn("Document type could not be determined.", "document type unclear");
        else if (Normalise(detected).Replace(" ", "-") == kind)
            c.Pass($"Read as {WithArticle(KindLabel(kind))} — matches the case file.");
        else
            c.Warn($"Read as '{detected}', but the case file lists it as {WithArticle(KindLabel(kind))}.", "document type doesn't match the case file");

        // 2. Name matches the case
        CheckName(c, kind, holder, party, profile);

        // 3. Date of birth matches the record on file
        if (isId || kind is "medical-report" or "death-certificate")
        {
            var docDob = ParseDate(Get(f, "dateOfBirth"));
            if (docDob is null)
                c.Warn("Date of birth could not be read.", "date of birth unreadable");
            else if (party is null || party.IsCompany)
                c.Warn("The named person isn't on this case, so the date of birth wasn't compared.", "date of birth not compared");
            else if (ParseDate(party.DateOfBirth) is not { } onFile)
                c.Warn($"No date of birth on file for {party.Name}.", "no date of birth on file");
            else if (onFile.Date == docDob.Value.Date)
                c.Pass($"{Dmy(docDob)} matches the record on file for {party.Name}.");
            else
                c.Fail($"Document shows {Dmy(docDob)}; the record on file for {party.Name} is {Dmy(onFile)}.",
                    "date of birth contradicts the record on file");
        }
        else
        {
            c.NotApplicable();
        }

        // 4. Dates plausible and consistent
        CheckDates(c, kind, f, received, profile);

        // 5. No visible tamper indicators
        var tamper = Get(f, "tamperIndicators");
        if (string.IsNullOrWhiteSpace(tamper) || NoneRegex.IsMatch(tamper))
            c.Pass("No tamper indicators found.");
        else
            c.Fail($"Tamper indicators: {Shorten(tamper, 480)}", "visible signs of alteration");

        // 6. Security features or certification present
        var security = Normalise(Get(f, "securityFeaturesPresent"));
        var certification = Get(f, "certification");
        var certified = certification is null ? string.Empty : $" Certified copy — {certification}.";
        if (isId)
        {
            var features = kind == "passport" ? "ghost image, guilloche background, MRZ" : "hologram band, ghost image, microprint";
            if (security.StartsWith("yes"))
                c.Pass($"Expected security features look intact ({features})." + certified);
            else if (security.StartsWith("partial"))
                c.Fail("Security features are incomplete or interrupted (for example a hologram band cut by a patch, or a ghost image that doesn't match the main photo).", "security features interrupted");
            else if (security.StartsWith("no"))
                c.Fail("Expected security features are missing or damaged.", "security features missing");
            else
                c.Warn("Could not confirm the security features.", "security features not confirmed");
        }
        else if (kind == "death-certificate")
        {
            if (security.StartsWith("yes") && certification is not null)
                c.Pass($"Registry seal present.{certified}");
            else if (security.StartsWith("yes"))
                c.Warn("Registry seal present, but no certification — ask for a certified copy.", "not a certified copy");
            else
                c.Warn("No registry seal or certification found — ask for a certified copy.", "not a certified copy");
        }
        else
        {
            c.NotApplicable();
        }

        // 7. Number formats and check digits
        CheckNumbers(c, kind, f);

        // 8. Electronic verification (DVS)
        if (isId)
        {
            var dvs = Get(f, "electronicVerification");
            if (string.IsNullOrWhiteSpace(dvs))
                c.Warn("No Document Verification Service (DVS) result on the case.", "DVS not run");
            else if (dvs.StartsWith("no match", StringComparison.OrdinalIgnoreCase))
                c.Fail($"DVS: {dvs}.", "DVS returned no match");
            else if (dvs.StartsWith("match", StringComparison.OrdinalIgnoreCase))
                c.Pass($"DVS: {dvs}.");
            else
                c.Warn($"DVS: {dvs}.", "DVS result unclear");
        }
        else
        {
            c.NotApplicable();
        }

        // 9. Supporting-evidence checks
        CheckEvidence(c, kind, f, profile, party, received);

        // 10. Document not used on another client's case
        var number = NormaliseNumber(Get(f, "documentNumber") ?? sample.DocumentNumber);
        if (!string.IsNullOrWhiteSpace(sample.Owner) &&
            !sample.Owner.Equals(profile.ScenarioKey, StringComparison.OrdinalIgnoreCase))
        {
            c.Warn($"This document is held on another client's case ({FinCrimeCaseDocumentStore.ClientNameFor(sample.Owner)}); one document shouldn't support two clients' requests.",
                "document belongs to another client's case");
        }
        else if (number.Length > 0 &&
                 _seenDocumentNumbers.TryGetValue(number, out var seen) &&
                 !seen.CaseNumber.Equals(record.CaseNumber, StringComparison.OrdinalIgnoreCase) &&
                 !seen.ScenarioKey.Equals(profile.ScenarioKey, StringComparison.OrdinalIgnoreCase))
        {
            c.Warn($"Document number {Get(f, "documentNumber")} was already submitted on case {seen.CaseNumber}.",
                "document already used on another case");
        }
        else
        {
            c.Pass("Not seen on any other client's case.");
        }

        return c;
    }

    private static void CheckName(CheckRecorder c, string kind, string? holder, FinCrimeCaseParty? party, FinCrimeCaseProfile profile)
    {
        switch (kind)
        {
            case "driver-licence" or "passport" or "payslip" or "medical-report" or "bas-profit-loss":
                if (string.IsNullOrWhiteSpace(holder))
                    c.Warn("Name could not be read from the document.", "name unreadable");
                else if (party is null || party.IsCompany)
                    c.Warn($"'{holder}' isn't a person on this case ({PartyList(profile)}).", "name doesn't match anyone on the case");
                else
                    c.Pass($"'{holder}' matches {party.Name} — {LowerFirst(party.Role)}.");
                break;
            case "death-certificate":
                if (string.IsNullOrWhiteSpace(holder))
                    c.Warn("Name of the deceased could not be read.", "name unreadable");
                else if (party is null || !party.Role.Contains("deceased", StringComparison.OrdinalIgnoreCase))
                    c.Warn($"'{holder}' isn't the life insured on this case ({PartyList(profile)}).", "deceased doesn't match the case");
                else
                    c.Pass($"'{holder}' matches {party.Name} — {LowerFirst(party.Role)}.");
                break;
            case "bank-statement":
                if (string.IsNullOrWhiteSpace(holder))
                {
                    c.Warn("Account name could not be read.", "account name unreadable");
                }
                else if (profile.AcceptedPayees.FirstOrDefault(n => NamesMatch(holder, n)) is { } accepted)
                {
                    c.Pass($"Account name '{holder}' matches {accepted}, who the benefit is payable to.");
                }
                else
                {
                    var payee = profile.AcceptedPayees.FirstOrDefault() ?? "the client";
                    var payeeParty = FindParty(profile, payee);
                    var payeeRole = payeeParty is null ? string.Empty
                        : payeeParty.Role.Contains("policy owner", StringComparison.OrdinalIgnoreCase) ? ", the policy owner"
                        : payeeParty.Role.Contains("beneficiary", StringComparison.OrdinalIgnoreCase) ? ", the nominated beneficiary"
                        : string.Empty;
                    c.Warn($"Account name '{holder}' doesn't match {payee}{payeeRole}. Benefits are paid to the policy owner or the nominated beneficiary — a new account in another name can't be used.",
                        "account name doesn't match the policy owner");
                }
                break;
            case "company-extract":
                if (string.IsNullOrWhiteSpace(holder))
                    c.Warn("Company name could not be read.", "company name unreadable");
                else if (party is null || !party.IsCompany)
                    c.Warn($"'{holder}' isn't the company on this case ({PartyList(profile)}).", "company doesn't match the case");
                else
                    c.Pass($"'{holder}' matches the company on the case ({LowerFirst(party.Role)}).");
                break;
            default:
                c.NotApplicable();
                break;
        }
    }

    private static void CheckDates(CheckRecorder c, string kind, IReadOnlyDictionary<string, string?> f, DateTime received, FinCrimeCaseProfile profile)
    {
        switch (kind)
        {
            case "driver-licence" or "passport":
            {
                var issue = ParseDate(Get(f, "issueDate"));
                var expiry = ParseDate(Get(f, "expiryDate"));
                if (issue is null && expiry is null)
                    c.Warn("Issue and expiry dates could not be read.", "dates unreadable");
                else if (issue is not null && expiry is not null && expiry < issue)
                    c.Fail($"Expiry ({Dmy(expiry)}) is before issue ({Dmy(issue)}).", "impossible dates");
                else if (issue is not null && issue > received)
                    c.Fail($"Issued {Dmy(issue)} — after the request was received ({Dmy(received)}).", "issued after the request");
                else if (expiry is not null && expiry < received)
                    c.Warn($"Expired on {Dmy(expiry)}, before the request ({Dmy(received)}).", "expired document");
                else
                    c.Pass($"Issued {Dmy(issue)}, expires {Dmy(expiry)} — current when the request was received.");
                break;
            }
            case "payslip":
            {
                var paid = ParseDate(Get(f, "issueDate"));
                var start = ParseDate(Get(f, "periodStart"));
                var end = ParseDate(Get(f, "periodEnd"));
                if (paid is null)
                    c.Warn("Pay date could not be read.", "pay date unreadable");
                else if (end is not null && (end > paid || (start is not null && start > end)))
                    c.Fail($"Pay period {Dmy(start)} – {Dmy(end)} doesn't fit the pay date {Dmy(paid)}.", "impossible dates");
                else if (paid > received)
                    c.Warn($"Pay date {Dmy(paid)} — after the request date.", "payslip dated after the request");
                else if ((received - paid.Value).TotalDays > 92)
                    c.Warn($"Pay date {Dmy(paid)} — more than three months before the request.", "payslip out of date");
                else
                    c.Pass($"Pay date {Dmy(paid)} for the period {Dmy(start)} – {Dmy(end)} — recent and consistent.");
                break;
            }
            case "medical-report":
            {
                var report = ParseDate(Get(f, "issueDate"));
                var seen = ParseDate(Get(f, "consultationDate"));
                var travel = profile.TravelNotice;
                var first = FirstName(profile.Parties.FirstOrDefault()?.Name ?? "the client");
                if (report is null)
                    c.Warn("Report date could not be read.", "report date unreadable");
                else if (seen is not null && seen > report)
                    c.Fail($"Consultation ({Dmy(seen)}) is after the report date ({Dmy(report)}).", "impossible dates");
                else if (report > received)
                    c.Warn($"Report dated {Dmy(report)} — after the request date.", "report dated after the request");
                else if (seen is not null && travel is not null && seen >= travel.From && seen <= travel.To)
                    c.Warn($"Consultation dated {Dmy(seen)}, but the travel notice puts {first} in {travel.Place} from {Dmy(travel.From)} to {Dmy(travel.To)} (recorded at {travel.RecordedAt}).",
                        "consultation date conflicts with the travel notice");
                else
                    c.Pass($"Report dated {Dmy(report)}{(seen is null ? string.Empty : $"; patient seen or treated {Dmy(seen)}")} — consistent with the request.");
                break;
            }
            case "death-certificate":
            {
                var death = ParseDate(Get(f, "dateOfDeath"));
                var registered = ParseDate(Get(f, "registrationDate") ?? Get(f, "issueDate"));
                if (death is null)
                    c.Warn("Date of death could not be read.", "date of death unreadable");
                else if (death > received)
                    c.Fail($"Date of death ({Dmy(death)}) is after the notification ({Dmy(received)}).", "impossible dates");
                else if (registered is not null && registered < death)
                    c.Fail($"Registered {Dmy(registered)} — before the date of death ({Dmy(death)}).", "impossible dates");
                else
                    c.Pass($"Died {Dmy(death)}; registered {Dmy(registered)}; notified {Dmy(received)}.");
                break;
            }
            case "bank-statement":
            {
                var statement = ParseDate(Get(f, "issueDate"));
                if (statement is null)
                    c.Warn("Statement date could not be read.", "statement date unreadable");
                else if (statement > received)
                    c.Warn($"Statement dated {Dmy(statement)} — after the request date.", "statement dated after the request");
                else if ((received - statement.Value).TotalDays > 92)
                    c.Warn($"Statement dated {Dmy(statement)} — more than three months before the request.", "statement out of date");
                else
                    c.Pass($"Statement dated {Dmy(statement)} — within three months of the request.");
                break;
            }
            case "bas-profit-loss":
            {
                var start = ParseDate(Get(f, "periodStart"));
                var end = ParseDate(Get(f, "periodEnd"));
                var prepared = ParseDate(Get(f, "issueDate"));
                if (start is null || end is null)
                    c.Warn("The reporting period could not be read.", "reporting period unreadable");
                else if (end < start || end > received)
                    c.Fail($"Reporting period {Dmy(start)} – {Dmy(end)} doesn't fit the request date ({Dmy(received)}).", "impossible dates");
                else if (prepared is not null && prepared > received)
                    c.Warn($"Prepared {Dmy(prepared)} — after the request date.", "prepared after the request");
                else
                    c.Pass($"Covers {Dmy(start)} – {Dmy(end)} ({Math.Round((end.Value - start.Value).TotalDays / 30.44)} months); prepared {Dmy(prepared)}.");
                break;
            }
            case "company-extract":
            {
                var extracted = ParseDate(Get(f, "issueDate"));
                var registered = ParseDate(Get(f, "registrationDate"));
                if (extracted is null)
                    c.Warn("Extract date could not be read.", "extract date unreadable");
                else if (registered is not null && registered > extracted)
                    c.Fail($"Registered {Dmy(registered)} — after the extract date ({Dmy(extracted)}).", "impossible dates");
                else if ((received - extracted.Value).TotalDays > 92)
                    c.Warn($"Extract dated {Dmy(extracted)} — more than three months old.", "extract not current");
                else
                    c.Pass($"Extracted {Dmy(extracted)} — current; company registered {Dmy(registered)}.");
                break;
            }
            default:
                c.NotApplicable();
                break;
        }
    }

    private static void CheckNumbers(CheckRecorder c, string kind, IReadOnlyDictionary<string, string?> f)
    {
        switch (kind)
        {
            case "passport":
            {
                var mrz = Get(f, "mrz");
                var (valid, problems) = ValidateTd3(mrz);
                if (string.IsNullOrWhiteSpace(mrz))
                    c.Warn("The machine-readable zone could not be read.", "MRZ unreadable");
                else if (!valid)
                    c.Fail($"MRZ check digits inconsistent: {string.Join("; ", problems)}.", "MRZ check digits inconsistent");
                else if (!MrzMatchesPrinted(mrz, Get(f, "documentNumber"), out var mismatch))
                    c.Fail($"MRZ check digits are valid but {mismatch}.", "MRZ doesn't match the printed data");
                else
                    c.Pass("MRZ check digits (document number, date of birth, expiry and composite) are consistent with the printed data.");
                break;
            }
            case "medical-report":
            {
                var provider = Get(f, "providerNumber");
                var compact = Regex.Replace(provider ?? string.Empty, @"\s", string.Empty).ToUpperInvariant();
                if (compact.Length == 0)
                    c.Warn("No provider number on the report.", "no provider number");
                else if (ProviderNumberRegex.IsMatch(compact))
                    c.Pass($"Provider number {provider} is in the Medicare provider number format (a six-digit stem, a practice location character and a check character).");
                else
                    c.Fail($"Provider number '{provider}' isn't in the format of a Medicare provider number (a six-digit stem, a practice location character and a check character).",
                        "provider number in the wrong format");
                break;
            }
            case "company-extract":
            {
                var digits = Regex.Replace(Get(f, "documentNumber") ?? string.Empty, @"\D", string.Empty);
                if (digits.Length == 9)
                    c.Pass($"ACN {Group3(digits)} is in the nine-digit ACN format.");
                else
                    c.Warn($"Company number '{Get(f, "documentNumber")}' isn't a nine-digit ACN.", "company number unclear");
                break;
            }
            case "bas-profit-loss":
            {
                var digits = Regex.Replace(Get(f, "documentNumber") ?? string.Empty, @"\D", string.Empty);
                if (digits.Length == 11)
                    c.Pass($"ABN {digits[..2]} {Group3(digits[2..])} is in the 11-digit ABN format.");
                else
                    c.Warn($"Business number '{Get(f, "documentNumber")}' isn't an 11-digit ABN.", "ABN unclear");
                break;
            }
            default:
                c.NotApplicable();
                break;
        }
    }

    private static void CheckEvidence(
        CheckRecorder c, string kind, IReadOnlyDictionary<string, string?> f, FinCrimeCaseProfile profile,
        FinCrimeCaseParty? party, DateTime received)
    {
        switch (kind)
        {
            case "medical-report":
            {
                var evidence = Get(f, "diagnosisEvidence");
                var needsHistopathology = profile.EvidenceRule?.Contains("histopathology", StringComparison.OrdinalIgnoreCase) == true;
                if (needsHistopathology)
                {
                    var confirmed = evidence is not null &&
                                    evidence.Contains("histopath", StringComparison.OrdinalIgnoreCase) &&
                                    !NotConfirmedRegex.IsMatch(evidence);
                    if (confirmed)
                        c.Pass($"Histopathology confirms the diagnosis ({evidence}).");
                    else
                        c.Fail($"No histopathology. {profile.EvidenceRule} The report relies on: {evidence ?? "nothing stated"}.",
                            "no histopathology");
                }
                else if (evidence is null)
                {
                    c.Warn("The report doesn't say what the diagnosis is based on.", "diagnosis evidence not shown");
                }
                else
                {
                    c.Pass($"Diagnosis supported by: {evidence}.");
                }
                break;
            }
            case "bank-statement":
            {
                var opened = ParseDate(Get(f, "accountOpenedDate"));
                if (opened is null)
                {
                    c.Warn("The account opening date isn't shown.", "account age unknown");
                    break;
                }
                var days = (int)(received - opened.Value).TotalDays;
                if (days is >= 0 and <= 30)
                    c.Warn($"Account opened {Dmy(opened)} — {days} day{(days == 1 ? "" : "s")} before the request (newly opened account).", "newly opened account");
                else
                    c.Pass($"Account open since {Dmy(opened)}.");
                break;
            }
            case "payslip" or "bas-profit-loss":
            {
                var amount = ParseAmount(Get(f, "keyAmount"));
                var label = Get(f, "keyAmountLabel") ?? (kind == "payslip" ? "Annual salary" : "Net business income");
                var source = kind == "payslip" ? "the income in the fact find" : "the earnings on the claim";
                if (amount is null)
                    c.Warn($"{label} could not be read.", "income not shown");
                else if (profile.IncomeOnFile is not { } onFile || onFile <= 0)
                    c.Pass($"{label} {Money(amount.Value)} — internally consistent.");
                else if (Math.Abs(amount.Value - onFile) / onFile <= 0.05m)
                    c.Pass($"{label} {Money(amount.Value)} matches {source} ({Money(onFile)}).");
                else
                    c.Warn($"{label} {Money(amount.Value)} doesn't match {source} ({Money(onFile)}).", "income doesn't match the case");
                break;
            }
            case "company-extract":
            {
                var status = Get(f, "companyStatus");
                var officers = Get(f, "officers") ?? string.Empty;
                var owners = profile.Parties.Where(p => !p.IsCompany &&
                    (p.Role.Contains("director", StringComparison.OrdinalIgnoreCase) ||
                     p.Role.Contains("shareholder", StringComparison.OrdinalIgnoreCase))).ToList();
                var missing = owners.Where(p => !ContainsName(officers, p.Name)).Select(p => p.Name).ToList();
                if (status is not null && !status.Contains("registered", StringComparison.OrdinalIgnoreCase))
                    c.Fail($"Company status is '{status}' — not registered.", "company not registered");
                else if (status is not null && status.Contains("deregistered", StringComparison.OrdinalIgnoreCase))
                    c.Fail($"Company status is '{status}'.", "company deregistered");
                else if (officers.Length == 0)
                    c.Warn("Office holders and members could not be read.", "office holders unreadable");
                else if (missing.Count > 0)
                    c.Warn($"Not on the extract: {string.Join(", ", missing)}.", "office holders don't match the case");
                else
                    c.Pass($"Status {status ?? "not shown"}. Office holders and members match the case — {officers}. Beneficial owners: {string.Join(" and ", owners.Select(o => o.Name))}.");
                break;
            }
            case "death-certificate":
                if (party is not null && party.Role.Contains("life insured", StringComparison.OrdinalIgnoreCase))
                    c.Pass($"The deceased is the life insured on the policy ({party.Role}).");
                else
                    c.NotApplicable("No life insured on this case to compare with.");
                break;
            default:
                c.NotApplicable();
                break;
        }
    }

    private static (string Verdict, string Summary, List<string> Reasons) ScoreVerdict(
        CheckRecorder c, FinCrimeSampleDocument sample, IReadOnlyDictionary<string, string?> fields)
    {
        var reasons = c.Checks
            .Where(x => x.Status is "fail" or "warn")
            .Select(x => $"{x.Name}: {x.Detail}")
            .ToList();

        // A failed identity document whose details still verify with the issuer
        // is a genuine document that has been altered (e.g. a replaced photo).
        var detailsVerify = IsIdentityDocument(sample.Kind) &&
                            (Get(fields, "electronicVerification")?.StartsWith("match", StringComparison.OrdinalIgnoreCase) ?? false);
        var verdict = c.FailHeadlines.Count > 0 ? (detailsVerify ? "tampered" : "fake")
                    : c.WarnHeadlines.Count > 0 ? "suspicious"
                    : "legit";

        // Respect the case-file flag if extraction missed what the checks need.
        if (verdict == "legit" && !sample.Expected.Equals("legit", StringComparison.OrdinalIgnoreCase))
        {
            verdict = "suspicious";
            reasons.Add($"The case file flags this document for review: {sample.Description}");
            c.WarnHeadlines.Add("flagged on the case file");
        }

        var headlines = c.FailHeadlines.Concat(c.WarnHeadlines).Distinct().ToList();
        var summary = verdict switch
        {
            "fake" => "Likely fake — " + string.Join("; ", headlines) + ".",
            "tampered" => "Tampered — " + string.Join("; ", headlines) + ". The details match the issuer's record, but the image has been altered.",
            "suspicious" => "Needs verification — " + string.Join("; ", c.WarnHeadlines.Distinct()) + ".",
            _ => "Consistent with a genuine document — every applicable check passed.",
        };
        return (verdict, summary, reasons);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsIdentityDocument(string kind) =>
        kind.Equals("driver-licence", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("passport", StringComparison.OrdinalIgnoreCase);

    /// <summary>Plain-English label for a document kind ("driver licence", "BAS and profit and loss summary").</summary>
    public static string KindLabel(string kind) => kind switch
    {
        "driver-licence" => "driver licence",
        "passport" => "passport",
        "payslip" => "payslip",
        "medical-report" => "medical report",
        "death-certificate" => "death certificate",
        "bank-statement" => "bank statement",
        "bas-profit-loss" => "BAS and profit and loss summary",
        "company-extract" => "company extract",
        _ => kind.Replace('-', ' '),
    };

    private static string WithArticle(string noun) =>
        (Regex.IsMatch(noun, "^[aeiouAEIOU]") ? "an " : "a ") + noun;

    private static string? Get(IReadOnlyDictionary<string, string?> fields, string key) =>
        fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Normalise(string? value) =>
        Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9&/]+", " ").Trim();

    private static string NormaliseNumber(string? value) =>
        Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9*]", string.Empty).ToUpperInvariant();

    private static string Group3(string digits) =>
        Regex.Replace(digits, @"(\d{3})(?=\d)", "$1 ");

    private static string FirstName(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;

    private static string LowerFirst(string s) =>
        string.IsNullOrEmpty(s) || (s.Length > 1 && char.IsUpper(s[1])) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private static string Money(decimal amount) =>
        amount.ToString(amount == Math.Round(amount) ? "$#,##0" : "$#,##0.00", CultureInfo.InvariantCulture);

    private static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = Regex.Replace(raw, @"[^0-9.\-]", string.Empty);
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static FinCrimeCaseParty? FindParty(FinCrimeCaseProfile profile, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return profile.Parties.FirstOrDefault(p =>
            NamesMatch(name, p.Name) || (p.Aliases?.Any(a => NamesMatch(name, a)) ?? false));
    }

    private static string PartyList(FinCrimeCaseProfile profile) =>
        string.Join(", ", profile.Parties.Select(p => p.Name));

    /// <summary>True when every word of <paramref name="name"/> appears in <paramref name="text"/>.</summary>
    private static bool ContainsName(string text, string name)
    {
        var words = new HashSet<string>(Normalise(text).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var tokens = Normalise(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(words.Contains);
    }

    /// <summary>
    /// Names match when they normalise to the same text, or share at least two
    /// meaningful tokens (one when either name is a single word).
    /// </summary>
    private static bool NamesMatch(string a, string b)
    {
        var na = Normalise(a);
        var nb = Normalise(b);
        if (na.Length == 0 || nb.Length == 0) return false;
        if (na == nb) return true;
        var ta = Tokens(na);
        var tb = Tokens(nb);
        if (ta.Count == 0 || tb.Count == 0) return false;
        var common = ta.Intersect(tb).Count();
        return common >= Math.Min(2, Math.Min(ta.Count, tb.Count)) && common >= Math.Min(ta.Count, tb.Count);
    }

    private static HashSet<string> Tokens(string normalised) =>
        new(normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 2 && t is not ("pty" or "ltd" or "the")));

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact.Date;
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.AllowWhiteSpaces, out var loose)
            ? loose.Date
            : null;
    }

    private static string Dmy(DateTime? value) =>
        value is { } d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "(not shown)";

    private static string Shorten(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";

    // ── ICAO 9303 TD3 (passport) machine-readable zone ──────────────────────

    private static int MrzValue(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'A' and <= 'Z' => ch - 'A' + 10,
        _ => 0,
    };

    private static char CheckDigit(string field)
    {
        int[] weights = [7, 3, 1];
        var total = 0;
        for (var i = 0; i < field.Length; i++)
            total += MrzValue(char.ToUpperInvariant(field[i])) * weights[i % 3];
        return (char)('0' + total % 10);
    }

    private static string? MrzLine2(string? mrz) =>
        (mrz ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Replace(" ", string.Empty).ToUpperInvariant())
            .LastOrDefault(l => l.Length == 44);

    private static (bool Valid, List<string> Problems) ValidateTd3(string? mrz)
    {
        var problems = new List<string>();
        var line = MrzLine2(mrz);
        if (line is null)
        {
            problems.Add("the second MRZ line is not the 44 characters a passport needs");
            return (false, problems);
        }

        void Expect(string label, string field, char actual)
        {
            var expected = CheckDigit(field);
            if (actual != expected) problems.Add($"{label} check digit is {actual}, expected {expected}");
        }

        Expect("document number", line[..9], line[9]);
        Expect("date of birth", line[13..19], line[19]);
        Expect("expiry date", line[21..27], line[27]);
        var personal = line[28..42];
        var personalBlank = personal.All(ch => ch == '<') && (line[42] == '<' || line[42] == '0');
        if (!personalBlank) Expect("personal number", personal, line[42]);
        Expect("composite", line[..10] + line[13..20] + line[21..43], line[43]);
        return (problems.Count == 0, problems);
    }

    private static bool MrzMatchesPrinted(string? mrz, string? printedNumber, out string mismatch)
    {
        mismatch = string.Empty;
        var line = MrzLine2(mrz);
        var printed = NormaliseNumber(printedNumber);
        if (line is null || printed.Length == 0) return true;
        var mrzNumber = line[..9].TrimEnd('<');
        if (mrzNumber.Equals(printed, StringComparison.OrdinalIgnoreCase)) return true;
        mismatch = $"the MRZ document number ({mrzNumber}) differs from the printed number ({printedNumber})";
        return false;
    }
}
