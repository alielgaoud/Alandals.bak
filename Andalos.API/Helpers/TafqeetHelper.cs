namespace Andalos.API.Helpers
{
    public static class TafqeetHelper
    {
        private static readonly string[] Ones =
        {
            "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
            "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
            "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
        };

        private static readonly string[] Tens =
        {
            "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
        };

        private static readonly string[] Hundreds =
        {
            "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة",
            "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
        };

        public static string ToArabicWords(decimal amount, string currencyName = "دينار ليبي", string fractionName = "درهم")
        {
            amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero);

            long integerPart = (long)Math.Floor(amount);
            int fractionPart = (int)Math.Round((amount - integerPart) * 1000m, 0, MidpointRounding.AwayFromZero);

            // دعم 3 منازل (درهم ليبي تقريبي)
            if (fractionPart >= 1000)
            {
                integerPart += 1;
                fractionPart = 0;
            }

            string integerWords = integerPart == 0 ? "صفر" : ConvertNumber(integerPart);
            string result = $"{integerWords} {currencyName}";

            if (fractionPart > 0)
            {
                result += $" و {ConvertNumber(fractionPart)} {fractionName}";
            }

            return result.Trim() + " فقط لا غير.";
        }

        private static string ConvertNumber(long number)
        {
            if (number == 0) return "صفر";
            if (number < 0) return "سالب " + ConvertNumber(Math.Abs(number));

            var parts = new List<string>();

            long billions = number / 1_000_000_000;
            number %= 1_000_000_000;
            long millions = number / 1_000_000;
            number %= 1_000_000;
            long thousands = number / 1000;
            number %= 1000;

            if (billions > 0)
                parts.Add(FormatGroup(billions, "مليار", "ملياران", "مليارات"));

            if (millions > 0)
                parts.Add(FormatGroup(millions, "مليون", "مليونان", "ملايين"));

            if (thousands > 0)
                parts.Add(FormatGroup(thousands, "ألف", "ألفان", "آلاف"));

            if (number > 0)
                parts.Add(ConvertBelowThousand((int)number));

            return string.Join(" و ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private static string FormatGroup(long value, string singular, string dual, string plural)
        {
            if (value == 1) return singular;
            if (value == 2) return dual;
            if (value >= 3 && value <= 10) return $"{ConvertBelowThousand((int)value)} {plural}";
            return $"{ConvertBelowThousand((int)value)} {singular}";
        }

        private static string ConvertBelowThousand(int number)
        {
            if (number == 0) return "";
            if (number < 20) return Ones[number];

            int hundreds = number / 100;
            int remainder = number % 100;

            var parts = new List<string>();

            if (hundreds > 0)
                parts.Add(Hundreds[hundreds]);

            if (remainder > 0)
            {
                if (remainder < 20)
                {
                    parts.Add(Ones[remainder]);
                }
                else
                {
                    int ten = remainder / 10;
                    int one = remainder % 10;

                    if (one > 0)
                        parts.Add($"{Ones[one]} و {Tens[ten]}");
                    else
                        parts.Add(Tens[ten]);
                }
            }

            return string.Join(" و ", parts);
        }
    }
}