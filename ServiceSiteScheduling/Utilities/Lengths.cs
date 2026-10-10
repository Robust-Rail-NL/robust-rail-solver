using Microsoft.Extensions.Logging;

namespace ServiceSiteScheduling.Utilities
{
    /// <summary>
    /// Track and unit lengths are stored as whole millimetres (<c>int</c>), so that sums and
    /// comparisons are exact. Input and output stay in metres.
    /// </summary>
    public static class Lengths
    {
        public const int MillimetresPerMetre = 1000;

        // Below this, a length counts as being a whole number of millimetres: it only
        // has to absorb the error of the metres-to-millimetres multiplication.
        private const double MillimetreTolerance = 1e-6;

        // Away from zero, to match the evaluator's llround (C#'s default is to-even).
        // `what` names the length in the warning for one that does not fit in whole
        // millimetres and so is rounded.
        public static int FromMetres(double metres, string what)
        {
            int millimetres = checked(
                (int)Math.Round(metres * MillimetresPerMetre, MidpointRounding.AwayFromZero)
            );
            if (!IsWholeMillimetres(metres))
                Logging
                    .GetLogger()
                    .LogWarning(
                        "{What}: length {Metres} m has more than 3 decimals; rounded to {Rounded} m",
                        what,
                        metres,
                        ToMetres(millimetres)
                    );
            return millimetres;
        }

        public static bool IsWholeMillimetres(double metres)
        {
            double millimetres = metres * MillimetresPerMetre;
            return Math.Abs(millimetres - Math.Round(millimetres)) <= MillimetreTolerance;
        }

        public static double ToMetres(int millimetres) => (double)millimetres / MillimetresPerMetre;
    }
}
