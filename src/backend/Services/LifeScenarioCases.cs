namespace CaldovaLife.App.Services;

/// <summary>
/// Maps the five seeded scenario cases (<c>REQ-yyyyMMdd-LIFnnn</c>) to a
/// stable key — <c>priya</c>, <c>grace</c>, <c>george</c>, <c>tom</c> or
/// <c>robert</c> — whatever sample id the Onboarding catalogue gives them.
/// Used by the Client Communications and Team Leader pages, which show the
/// scenario cases at a fixed point in their journey. Cases opened live with
/// the Onboarding Agent return <c>null</c>.
/// </summary>
internal static class LifeScenarioCases
{
    public const string Priya = "priya";
    public const string Grace = "grace";
    public const string George = "george";
    public const string Tom = "tom";
    public const string Robert = "robert";

    // Scenario order — LIF001 … LIF005.
    private static readonly string[] Keys = [Priya, Grace, George, Tom, Robert];

    public static string? KeyOf(CaseRecord c)
    {
        if (!CaseStore.IsScenarioCaseNumber(c.CaseNumber)) return null;

        var sample = (c.SampleId ?? string.Empty).ToLowerInvariant();
        foreach (var key in Keys)
        {
            if (sample.Contains(key)) return key;
        }

        var number = c.CaseNumber.ToUpperInvariant();
        for (var i = 0; i < Keys.Length; i++)
        {
            if (number.EndsWith($"LIF{i + 1:000}")) return Keys[i];
        }
        return null;
    }
}

/// <summary>
/// Caldova Life Advice contact details used in client communications. All
/// fictional: the landline is in the (03) 5550 range ACMA reserves for
/// creative works, and the practice mailbox is the one in the FSG
/// (<c>requests@caldova.example</c>).
/// </summary>
internal static class PracticeContacts
{
    public const string Phone = "(03) 5550 1889";
    public const string Hours = "8:30am–5:30pm Melbourne time, Monday to Friday";
    public const string Email = "requests@caldova.example";
    public const string AmeliaEmail = "amelia@caldova.example";
    public const string Address = "Caldova House, Melbourne VIC 3000";
}

/// <summary>Australian date display for Client Case dates stored as <c>yyyy-MM-dd</c>.</summary>
internal static class PracticeDates
{
    private static readonly System.Globalization.CultureInfo AuCulture = new("en-AU");

    /// <summary>"2026-11-23" → "Mon 23 Nov 2026"; anything else is returned unchanged.</summary>
    public static string Display(string? isoDate) =>
        DateTime.TryParseExact(isoDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("ddd d MMM yyyy", AuCulture)
            : isoDate ?? string.Empty;
}

/// <summary>
/// The regulatory rules and figures quoted by Cara's drafts, Theo's dashboard
/// and the group chat, kept in one place so every page says the same thing.
/// Each one is checked against primary sources as at 23 Nov 2026 (FY2026-27).
/// </summary>
internal static class LifeRules
{
    // Cooling-off for life risk insurance: 14 days from the earlier of receiving
    // confirmation or the end of the 5th business day after issue, and not once
    // a claim has been made (Corporations Act s1019B(3), (5)).
    public const string CoolingOff = "14 days";
    public const string CoolingOffPlain =
        "You have 14 days to change your mind and cancel your new policy for a refund of your premiums. The 14 days start when you " +
        "receive confirmation of your policy, or at the end of the fifth business day after it's issued if that's earlier. The " +
        "cooling-off period doesn't apply once you've made a claim.";

    // Written response to a complaint (ASIC RG 271.56; Life Code cl 7.13).
    public const string IdrResponse = "30 calendar days";

    // Insurance Contracts Act 1984 s29(3): avoidance for non-fraudulent
    // misrepresentation only within three years of the contract.
    public const string MisrepresentationWindow = "three years";

    // Protecting Your Super (SIS Act s68AAA): no insurance on an account with
    // no amount received for 16 continuous months unless the client elects in writing.
    public const string PysInactivity = "16 months";

    // Life Insurance Code of Practice (CALI edition in force from 1 Mar 2025).
    // It binds insurers, not advisers (cl 1.10) — Caldova tracks it for clients.
    public const string CodeIncomeClaimDecision =
        "Life Code cl 5.48 — the insurer decides an income-related claim within 2 months of receiving it, or of the end of the waiting period if later; " +
        "updates at least every 20 business days (cl 5.6)";
    public const string CodeLumpSumClaimDecision =
        "Life Code cl 5.49 — the insurer decides a lump-sum claim (death, terminal illness, trauma, TPD) within 6 months; " +
        "updates at least every 20 business days (cl 5.6)";
    public const string CodeWrittenDecision =
        "a written decision within 15 business days of the insurer having all the information it needs (Life Code cl 5.50)";

    // External dispute resolution and free support services.
    public const string AfcaContact = "1800 931 678 · www.afca.org.au · info@afca.org.au";
    public const string Idcare = "IDCARE, a free national identity and cyber support service — 1800 595 160 or www.idcare.org";
    public const string DebtHelpline = "the National Debt Helpline on 1800 007 007 (ndh.org.au)";
}
