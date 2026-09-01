namespace WrenchWorks.Api.Features.Common;

/// <summary>
/// One taxable line, reduced to what the arithmetic needs.
/// <paramref name="GrossOrNet"/> is the line amount as entered — whether that is the net or
/// the tax-inclusive figure depends on the business's PricesIncludeTax setting.
/// </summary>
public record TaxableLine(decimal GrossOrNet, decimal RatePercent);

/// <summary>What a line worked out to, once the rate and the inclusive/exclusive setting are applied.</summary>
public record TaxedLine(decimal Net, decimal Tax, decimal Gross);

public record TaxTotals(decimal Net, decimal Tax, decimal Gross);

/// <summary>
/// The tax arithmetic, in one place.
///
/// Kept separate from the endpoints so a provider (Avalara, TaxJar, Stripe Tax) can be
/// slotted in later without touching them, and — more immediately — so the rounding rule
/// is written once rather than re-derived at each call site.
///
/// See docs/tax.md for why these choices and not the equally defensible alternatives.
/// </summary>
public static class TaxCalculator
{
    /// <summary>
    /// Round per line, half away from zero, to 2dp.
    ///
    /// Banker's rounding (.NET's default) would round 0.125 to 0.12, which is not what an
    /// invoice reader expects. Rounding per line and then summing — rather than taxing the
    /// summed total — is the other half of the rule; mixing the two produces invoices off
    /// by a penny.
    /// </summary>
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Splits one line into net, tax and gross.
    ///
    /// The inclusive branch is the one that is easy to get wrong: a price quoted "including
    /// VAT" already contains the tax, so it must be divided out rather than added on.
    /// </summary>
    public static TaxedLine CalculateLine(TaxableLine line, bool pricesIncludeTax)
    {
        if (line.RatePercent <= 0)
        {
            var amount = Money(line.GrossOrNet);
            return new TaxedLine(amount, 0m, amount);
        }

        if (pricesIncludeTax)
        {
            var gross = Money(line.GrossOrNet);
            var net = Money(gross / (1 + line.RatePercent));
            // Tax as the remainder, not as its own rounded product: this guarantees
            // net + tax == gross exactly, which is what the customer is charged.
            return new TaxedLine(net, gross - net, gross);
        }

        var netAmount = Money(line.GrossOrNet);
        var tax = Money(netAmount * line.RatePercent);
        return new TaxedLine(netAmount, tax, netAmount + tax);
    }

    public static TaxTotals Total(IEnumerable<TaxableLine> lines, bool pricesIncludeTax)
    {
        decimal net = 0m, tax = 0m;

        foreach (var line in lines)
        {
            var result = CalculateLine(line, pricesIncludeTax);
            net += result.Net;
            tax += result.Tax;
        }

        return new TaxTotals(net, tax, net + tax);
    }

    // The rate to apply to a new line, given the business's configured rates.
    //
    // Returns 0 when nothing defaults for the category — which is exactly how a US shop
    // expresses "labour is not taxable here", and how every business behaves before it has
    // configured anything.
    //
    // Plain // rather than ///: the .NET 10 preview OpenAPI XML-comment source generator
    // emits `IEnumerable` with no type argument for a tuple-typed generic parameter and
    // fails with CS0305 in generated code. Same family as the CS0673 trap in CLAUDE.md.
    public static decimal DefaultRateFor(
        IEnumerable<(bool IsDefaultForLabour, bool IsDefaultForParts, decimal Rate)> rates,
        bool forLabour)
    {
        var match = rates.FirstOrDefault(r => forLabour ? r.IsDefaultForLabour : r.IsDefaultForParts);
        return match.Rate;
    }
}
