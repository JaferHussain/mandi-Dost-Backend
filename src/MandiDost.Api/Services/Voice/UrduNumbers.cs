namespace MandiDost.Api.Services.Voice;

/// <summary>A number found in a sentence: tokens [Start, End) spell the value, e.g. "ڈیڑھ لاکھ" = 150000.</summary>
public readonly record struct NumberSpan(int Start, int End, double Value);

/// <summary>Reads numbers the way people say them in Urdu: digits, words, ہزار / لاکھ / کروڑ, ڈیڑھ, ساڑھے تین, پونے دو.</summary>
public static class UrduNumbers
{
    private static readonly string[] Words =
    [
        "صفر", "ایک", "دو", "تین", "چار", "پانچ", "چھ", "سات", "آٹھ", "نو",
        "دس", "گیارہ", "بارہ", "تیرہ", "چودہ", "پندرہ", "سولہ", "سترہ", "اٹھارہ", "انیس",
        "بیس", "اکیس", "بائیس", "تئیس", "چوبیس", "پچیس", "چھبیس", "ستائیس", "اٹھائیس", "انتیس",
        "تیس", "اکتیس", "بتیس", "تینتیس", "چونتیس", "پینتیس", "چھتیس", "سینتیس", "اڑتیس", "انتالیس",
        "چالیس", "اکتالیس", "بیالیس", "تینتالیس", "چوالیس", "پینتالیس", "چھیالیس", "سینتالیس", "اڑتالیس", "انچاس",
        "پچاس", "اکیاون", "باون", "ترپن", "چون", "پچپن", "چھپن", "ستاون", "اٹھاون", "انسٹھ",
        "ساٹھ", "اکسٹھ", "باسٹھ", "تریسٹھ", "چوسٹھ", "پینسٹھ", "چھیاسٹھ", "سڑسٹھ", "اڑسٹھ", "انہتر",
        "ستر", "اکہتر", "بہتر", "تہتر", "چوہتر", "پچھتر", "چھہتر", "ستتر", "اٹھہتر", "اناسی",
        "اسی", "اکیاسی", "بیاسی", "تراسی", "چوراسی", "پچاسی", "چھیاسی", "ستاسی", "اٹھاسی", "نواسی",
        "نوے", "اکانوے", "بانوے", "ترانوے", "چورانوے", "پچانوے", "چھیانوے", "ستانوے", "اٹھانوے", "ننانوے",
    ];

    private static readonly Dictionary<string, double> WordValues = BuildWordValues();
    private static readonly Dictionary<string, double> Multipliers = new()
    {
        [UrduText.Normalize("ہزار")] = 1_000,
        [UrduText.Normalize("لاکھ")] = 100_000,
        [UrduText.Normalize("کروڑ")] = 10_000_000,
        [UrduText.Normalize("ارب")] = 1_000_000_000,
    };

    private static readonly string Hundred = UrduText.Normalize("سو");
    private static readonly string OneAndHalf = UrduText.Normalize("ڈیڑھ");
    private static readonly string TwoAndHalf = UrduText.Normalize("ڈھائی");
    private static readonly string AndHalf = UrduText.Normalize("ساڑھے");
    private static readonly string AndQuarter = UrduText.Normalize("سوا");
    private static readonly string MinusQuarter = UrduText.Normalize("پونے");

    private static Dictionary<string, double> BuildWordValues()
    {
        var d = new Dictionary<string, double>();
        for (var i = 0; i < Words.Length; i++) d[UrduText.Normalize(Words[i])] = i;
        // common spelling variants
        d[UrduText.Normalize("چوّن")] = 54;
        d[UrduText.Normalize("نوّے")] = 90;
        return d;
    }

    private static bool TryBase(string token, out double value)
    {
        if (UrduText.IsNumber(token, out value)) return true;
        return WordValues.TryGetValue(token, out value);
    }

    public static List<NumberSpan> FindSpans(string[] t)
    {
        var spans = new List<NumberSpan>();
        var i = 0;
        while (i < t.Length)
        {
            if (TryParsePhrase(t, i, out var end, out var value)) { spans.Add(new NumberSpan(i, end, value)); i = end; }
            else i++;
        }
        return spans;
    }

    private static bool TryParsePhrase(string[] t, int start, out int end, out double value)
    {
        double total = 0, cur = 0;
        var i = start;
        var any = false;
        var lastWasBase = false;

        while (i < t.Length)
        {
            var w = t[i];

            if (TryBase(w, out var v))
            {
                if (lastWasBase) break; // "دو تین" is two numbers, not 5
                cur += v; lastWasBase = true; any = true; i++;
            }
            else if (w == Hundred)
            {
                cur = (cur == 0 ? 1 : cur) * 100; lastWasBase = false; any = true; i++;
            }
            else if (Multipliers.TryGetValue(w, out var mult))
            {
                total += (cur == 0 ? 1 : cur) * mult; cur = 0; lastWasBase = false; any = true; i++;
            }
            else if (w == OneAndHalf || w == TwoAndHalf)
            {
                if (lastWasBase) break;
                cur += w == OneAndHalf ? 1.5 : 2.5; lastWasBase = true; any = true; i++;
            }
            else if ((w == AndHalf || w == MinusQuarter || w == AndQuarter) && !lastWasBase)
            {
                // "ساڑھے تین" = 3.5, "پونے دو" = 1.75, "سوا تین" = 3.25, "سوا لاکھ" = 1.25 lakh
                if (i + 1 < t.Length && TryBase(t[i + 1], out var n))
                {
                    cur += w == AndHalf ? n + 0.5 : w == AndQuarter ? n + 0.25 : n - 0.25;
                    lastWasBase = true; any = true; i += 2;
                }
                else if (w == AndQuarter && i + 1 < t.Length && (Multipliers.ContainsKey(t[i + 1]) || t[i + 1] == Hundred))
                {
                    cur += 1.25; lastWasBase = true; any = true; i++;
                }
                else break;
            }
            else break;
        }

        end = i;
        value = total + cur;
        return any;
    }
}
