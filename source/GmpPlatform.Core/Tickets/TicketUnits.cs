namespace GmpPlatform.Core.Tickets;

/// <summary>
/// Unit handling for the ticket contract. Small, but it earns its own type: both sides of the
/// hand-off need the same answers, and one of these rules has already cost a wrong part number
/// on a drawing.
/// </summary>
public static class TicketUnits
{
    public const double FeetToInches = 12.0;

    /// <summary>
    /// Feet to inches, rounded to a thousandth.
    /// <para>
    /// Revit stores feet, and 46'-3" comes back as 554.9999999999432. Left unrounded that
    /// noise crosses the contract: the rebar planner floored it to 554", named the bar
    /// <c>RSX05096</c> instead of <c>RSX05097</c>, and the wrong number reached the bill of
    /// material. Clean the numbers at the boundary; keep the arithmetic behind it tolerant.
    /// </para>
    /// </summary>
    public static double Inches(double feet) => Math.Round(feet * FeetToInches, 3);

    /// <summary>
    /// Inches as the ticket writes them — <c>46'-3"</c>, <c>1'-0"</c>.
    /// <para>
    /// The tolerance matters: a dimension that is a hair under a whole foot should read
    /// <c>46'-0"</c>, not <c>45'-12"</c> or <c>46'-0.01"</c>.
    /// </para>
    /// </summary>
    public static string FeetInches(double inches)
    {
        var feet = (int)(inches / 12);
        var rest = inches - feet * 12;

        // Round up into the next foot when the remainder is within tolerance of 12".
        if (Math.Abs(rest - 12.0) < 0.02)
        {
            feet += 1;
            rest = 0;
        }

        return Math.Abs(rest) < 0.02 ? $"{feet}'-0\"" : $"{feet}'-{rest:0.##}\"";
    }

    /// <summary>
    /// A length the way a drafter writes it: fractions rather than decimals, and inches alone
    /// under a foot — <c>47'-10"</c>, <c>12'-4 1/4"</c>, <c>9"</c>.
    /// <para>
    /// The dialogs used to show <c>L=47.833 H=12.354 T=0.75</c> — decimal feet, which is how
    /// Revit stores a length and how nobody reads one. A 9" wythe displayed as <c>0.75</c> is
    /// a number a drafter has to convert before they can check it.
    /// </para>
    /// </summary>
    /// <param name="inches">The length in inches.</param>
    /// <param name="denominator">Precision; 8 gives eighths, which covers the approved ticket.</param>
    public static string Architectural(double inches, int denominator = 8)
    {
        var sign = inches < 0 ? "-" : "";
        var steps = (long)Math.Round(Math.Abs(inches) * denominator, MidpointRounding.AwayFromZero);

        var feet = steps / (12L * denominator);
        var remainder = steps - feet * 12L * denominator;
        var wholeInches = remainder / denominator;
        var numerator = remainder % denominator;

        var inchText = wholeInches.ToString();
        if (numerator != 0)
        {
            var gcd = Gcd(numerator, denominator);
            var fraction = $"{numerator / gcd}/{denominator / gcd}";
            inchText = wholeInches == 0 ? fraction : $"{wholeInches} {fraction}";
        }

        return feet == 0 ? $"{sign}{inchText}\"" : $"{sign}{feet}'-{inchText}\"";
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
