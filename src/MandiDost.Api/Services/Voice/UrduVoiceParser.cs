using MandiDost.Api.Entities;

namespace MandiDost.Api.Services.Voice;

public enum VoiceIntent { Unknown, Sell, Wanted, Search }

public record CategoryRef(int Id, string Slug, ProductSection Section);
public record LocationRef(int Id, string NameUr);

/// <summary>
/// What the parser understood from the sentence. Everything is a suggestion for the app to put into a form:
/// the user always sees and can correct it, and nothing is published without a separate confirmation.
/// </summary>
public record VoiceParseResult(
    VoiceIntent Intent,
    string Transcript,
    int? CategoryId,
    string? CategorySlug,
    ProductSection? Section,
    IReadOnlyList<ListingPurpose> Purposes,
    AnimalGender? Gender,
    TeethStage? Teeth,
    string? Breed,
    int? AgeMonths,
    int? WeightKg,
    int? MinWeightKg,
    int? MaxWeightKg,
    decimal? Quantity,
    QuantityUnit? QuantityUnit,
    long? Price,
    PriceUnit? PriceUnit,
    int? Shares,
    SupplyFrequency? Frequency,
    int? LocationId,
    bool? IsPregnant,
    bool? IsVaccinated,
    decimal? MilkLitresPerDay,
    IReadOnlyList<string> Missing);

/// <summary>
/// Turns a spoken or typed Urdu sentence into form fields, for example
/// "میرے پاس دو ساہیوال گائے ہیں، دو دانت، 80 ہزار روپے فی جانور، لودھراں" →
/// cow, 2 animals, Sahiwal, two teeth, Rs 80,000 each, Lodhran.
/// It uses word lists and number reading, not guessing: what it can't find is reported as missing.
/// </summary>
public static class UrduVoiceParser
{
    // ---- Word lists (normalised once, compared as whole words) ----------------------------

    private static HashSet<string> Set(params string[] words) => words.Select(UrduText.Normalize).ToHashSet();

    private record Noun(string Slug, AnimalGender Gender);

    private static readonly Dictionary<string, Noun> LivestockNouns = BuildNouns();

    private static Dictionary<string, Noun> BuildNouns()
    {
        var d = new Dictionary<string, Noun>();
        void Add(string slug, AnimalGender g, params string[] words) { foreach (var w in words) d[UrduText.Normalize(w)] = new Noun(slug, g); }
        Add("cow", AnimalGender.Female, "گائے", "گاے", "گائیں", "گایوں", "بچھیا", "بچھڑی", "گاؤ");
        Add("cow", AnimalGender.Male, "بیل", "بیلوں", "بچھڑا", "بچھڑے");
        Add("buffalo", AnimalGender.Female, "بھینس", "بھینسیں", "بھینسوں", "کٹی", "کٹیاں");
        Add("buffalo", AnimalGender.Male, "بھینسا", "کٹا", "کٹے");
        Add("goat", AnimalGender.Female, "بکری", "بکریاں", "بکریوں");
        Add("goat", AnimalGender.Male, "بکرا", "بکرے", "بکروں");
        Add("sheep", AnimalGender.Female, "بھیڑ", "بھیڑیں", "بھیڑوں");
        Add("sheep", AnimalGender.Male, "دنبہ", "دنبے", "دنبوں", "مینڈھا", "مینڈھے");
        Add("camel", AnimalGender.Unknown, "اونٹ", "اونٹوں", "اونٹنی", "اونٹنیاں");
        Add("camel", AnimalGender.Female, "اونٹنی", "اونٹنیاں");
        Add("other", AnimalGender.Male, "گھوڑا", "گھوڑے", "گدھا", "گدھے", "خچر");
        Add("other", AnimalGender.Female, "گھوڑی", "گدھی");
        return d;
    }

    private static readonly Dictionary<string, string> OtherNouns = BuildOtherNouns();

    private static Dictionary<string, string> BuildOtherNouns()
    {
        var d = new Dictionary<string, string>();
        void Add(string slug, params string[] words) { foreach (var w in words) d[UrduText.Normalize(w)] = slug; }
        Add("wheat", "گندم");
        Add("cotton", "کپاس", "روئی");
        Add("rice", "چاول", "دھان", "باسمتی");
        Add("maize", "مکئی", "مکی");
        Add("sugarcane", "گنا", "گنے");
        Add("vegetables", "سبزی", "سبزیاں", "آلو", "پیاز", "ٹماٹر", "گوبھی", "بھنڈی", "کدو");
        Add("fruits", "پھل", "آم", "کینو", "کیلا", "سیب", "انار", "امرود", "کھجور");
        Add("seeds", "بیج");
        Add("fertilizer", "کھاد", "یوریا");
        Add("pesticides", "سپرے", "ادویات");
        Add("feed", "چارہ", "ونڈا", "توڑی", "بھوسہ");
        Add("machinery", "ٹریکٹر", "مشین", "ٹرالی", "تھریشر");
        Add("tools", "اوزار", "درانتی");
        Add("dairy-equipment", "ڈیری");
        return d;
    }

    private static readonly HashSet<string> Kilo = Set("کلو", "کلوگرام", "kg");
    private static readonly HashSet<string> Maund = Set("من", "mann");
    private static readonly HashSet<string> Ton = Set("ٹن");
    private static readonly HashSet<string> Bag = Set("بوری", "بوریاں", "بیگ");
    private static readonly HashSet<string> Litre = Set("لیٹر", "لٹر");
    private static readonly HashSet<string> Rupee = Set("روپے", "روپیہ", "روپیے", "روپئے", "روپیوں", "rs", "rupees", "rupee");
    private static readonly HashSet<string> Year = Set("سال", "سالہ", "برس");
    private static readonly HashSet<string> Month = Set("ماہ", "مہینے", "مہینہ", "ماہہ");
    private static readonly HashSet<string> Teeth = Set("دانت");
    private static readonly HashSet<string> Shares = Set("حصے", "حصہ", "حصوں");
    private static readonly HashSet<string> HeadWords = Set("جانور", "جانوروں", "عدد", "نگ", "سر", "ہیڈ");
    private static readonly string Per = UrduText.Normalize("فی");
    private static readonly string From = UrduText.Normalize("سے");

    private static readonly HashSet<string> SellWords = Set("بیچ", "بیچنا", "بیچنی", "بیچنے", "بیچوں", "بیچتا", "بیچتی", "بیچنی", "فروخت", "بکاؤ", "اشتہار");
    private static readonly HashSet<string> WantedWords = Set("چاہیے", "چاہئے", "چاہیئے", "چاہییں", "چاہیں", "ضرورت", "درکار", "مطلوب",
        "خریدنا", "خریدنی", "خریدنے", "خریدوں", "خرید", "خریداری", "لینا", "لینی", "لینے");
    private static readonly HashSet<string> SearchWords = Set("دکھاؤ", "دکھائیں", "دکھا", "ڈھونڈ", "ڈھونڈو", "ڈھونڈیں", "ڈھونڈنا", "تلاش", "ملتا", "ملتی", "ملتے", "کہاں");

    private static readonly HashSet<string> MeatWords = Set("گوشت", "ہوٹل", "ریسٹورنٹ", "ریستوران", "قصائی", "قصاب");
    private static readonly HashSet<string> DairyWords = Set("دودھیل", "ڈیری");
    private static readonly HashSet<string> BreedingWords = Set("بریڈنگ", "افزائش");
    private static readonly HashSet<string> Male = Set("نر");
    private static readonly HashSet<string> Female = Set("مادہ");
    private static readonly HashSet<string> Pregnant = Set("گابھن", "حاملہ", "گابن");
    private static readonly HashSet<string> Vaccine = Set("ویکسین", "ویکسینیشن");
    private static readonly HashSet<string> No = Set("نہیں", "نہی");

    private static readonly (string[] Phrase, string Display)[] Breeds = BuildBreeds();

    private static (string[], string)[] BuildBreeds()
    {
        var names = new[]
        {
            "ساہیوال", "چولستانی", "ریڈ سندھی", "تھرپارکر", "دھنی", "فریزین کراس", "فریزین", "جرسی کراس", "جرسی",
            "نیلی راوی", "کنڈی", "بیتل", "ٹیڈی", "ڈیرہ دین پناہ", "کاموری", "مکھی چینی", "بربری",
            "لوہی", "کاجلی", "تھلی", "بلوچی دنبہ", "مریچہ", "بریلا", "براہوی", "دیسی",
        };
        return [.. names.Select(n => (UrduText.Tokenize(n), n)).OrderByDescending(x => x.Item1.Length)];
    }

    private static readonly Dictionary<string, string> BreedImpliesSlug = new()
    {
        [UrduText.Normalize("بیتل")] = "goat", [UrduText.Normalize("ٹیڈی")] = "goat", [UrduText.Normalize("کاموری")] = "goat", [UrduText.Normalize("بربری")] = "goat",
        [UrduText.Normalize("لوہی")] = "sheep", [UrduText.Normalize("کاجلی")] = "sheep", [UrduText.Normalize("ساہیوال")] = "cow",
        [UrduText.Normalize("نیلی")] = "buffalo",
    };

    private static readonly HashSet<string> CommonFirstWords = Set("دنیا", "نیا", "بڑا", "چھوٹا");

    // ---- Parsing ------------------------------------------------------------------------------

    public static VoiceParseResult Parse(string? text, IReadOnlyList<CategoryRef> categories, IReadOnlyList<LocationRef> locations)
    {
        var tokens = UrduText.Tokenize(text);
        var transcript = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var spans = UrduNumbers.FindSpans(tokens);
        var spanStart = spans.ToDictionary(s => s.Start);
        var spanEnds = spans.Select(s => s.End).ToHashSet();

        // 1. What is it about?
        var (slug, nounGender, nounIndex) = FindCategory(tokens);
        var breed = FindBreed(tokens);
        if (slug is null && breed is not null && BreedImpliesSlug.TryGetValue(UrduText.Normalize(breed.Split(' ')[0]), out var bs)) slug = bs;
        var category = slug is null ? null : categories.FirstOrDefault(c => c.Slug == slug);
        var isLivestock = category?.Section == ProductSection.Livestock;
        var nounIndexes = tokens.Select((w, i) => (w, i)).Where(x => LivestockNouns.ContainsKey(x.w) || OtherNouns.ContainsKey(x.w) || HeadWords.Contains(x.w)).Select(x => x.i).ToHashSet();

        // 2. Give every number a meaning from the word after it.
        int? ageMonths = null, weightKg = null, minWeight = null, maxWeight = null, shares = null;
        TeethStage? teeth = null;
        decimal? quantity = null, milk = null;
        QuantityUnit? quantityUnit = null;
        long? price = null;
        NumberSpan? priceSpan = null;
        var used = new HashSet<int>();

        // "تیس سے چالیس کلو": a range
        for (var k = 0; k + 1 < spans.Count; k++)
        {
            var a = spans[k]; var b = spans[k + 1];
            if (b.Start == a.End + 1 && tokens[a.End] == From && b.End < tokens.Length && Kilo.Contains(tokens[b.End]))
            {
                minWeight = (int)Math.Round(a.Value); maxWeight = (int)Math.Round(b.Value);
                used.Add(a.Start); used.Add(b.Start);
            }
        }

        foreach (var s in spans)
        {
            if (used.Contains(s.Start)) continue;
            var next = s.End < tokens.Length ? tokens[s.End] : "";
            var nearNoun = Enumerable.Range(s.End, Math.Min(4, Math.Max(0, tokens.Length - s.End))).Any(nounIndexes.Contains);

            if (Teeth.Contains(next))
            {
                teeth = s.Value switch { 2 => TeethStage.Two, 4 => TeethStage.Four, 6 => TeethStage.Six, _ => teeth };
            }
            else if (Shares.Contains(next)) shares = Math.Max(shares ?? 0, (int)Math.Round(s.Value)); // "سات حصے ... ایک حصے کی قیمت" is still 7
            else if (Year.Contains(next)) ageMonths = (int)Math.Round(s.Value * 12);
            else if (Month.Contains(next) && !(s.Start > 0 && tokens[s.Start - 1] == UrduText.Normalize("ہر"))) ageMonths = (int)Math.Round(s.Value);
            else if (Kilo.Contains(next))
            {
                if (isLivestock || category is null) weightKg = (int)Math.Round(s.Value);
                else { quantity = (decimal)s.Value; quantityUnit = Entities.QuantityUnit.Kg; }
            }
            else if (Maund.Contains(next))
            {
                if (isLivestock || category is null) weightKg = (int)Math.Round(s.Value * 40);
                else { quantity = (decimal)s.Value; quantityUnit = Entities.QuantityUnit.Maund; }
            }
            else if (Ton.Contains(next)) { quantity = (decimal)s.Value; quantityUnit = Entities.QuantityUnit.Ton; }
            else if (Bag.Contains(next)) { quantity = (decimal)s.Value; quantityUnit = Entities.QuantityUnit.Bag; }
            else if (Litre.Contains(next))
            {
                if (isLivestock) milk = (decimal)s.Value;
                else { quantity = (decimal)s.Value; quantityUnit = Entities.QuantityUnit.Litre; }
            }
            else if (Rupee.Contains(next) || next == Per)
            {
                if (price is null) { price = (long)Math.Round(s.Value); priceSpan = s; }
            }
            else if (nearNoun && s.Value is >= 1 and <= 999 && quantity is null)
            {
                quantity = (decimal)s.Value;
                quantityUnit = isLivestock ? Entities.QuantityUnit.Head : category is null ? null : quantityUnit;
            }
            else if (s.Value >= 1000 && price is null)
            {
                price = (long)Math.Round(s.Value); priceSpan = s;
            }
        }

        if (isLivestock && quantity is not null) quantityUnit = Entities.QuantityUnit.Head;
        if (category is not null && !isLivestock && quantity is not null && quantityUnit is null)
            quantityUnit = category.Section == ProductSection.Supplies ? Entities.QuantityUnit.Piece : Entities.QuantityUnit.Maund;

        // 3. Price per what?
        PriceUnit? priceUnit = null;
        if (priceSpan is { } ps) priceUnit = FindPriceUnit(tokens, ps, spans);
        if (shares == 7 && priceUnit is null && price is not null && tokens.Any(Shares.Contains)) priceUnit = Entities.PriceUnit.PerShare;

        // 4. Gender, health, purpose
        var gender = tokens.Any(Male.Contains) ? AnimalGender.Male : tokens.Any(Female.Contains) ? AnimalGender.Female : nounGender;
        bool? pregnant = tokens.Any(Pregnant.Contains) ? true : null;
        bool? vaccinated = null;
        for (var i = 0; i < tokens.Length; i++)
            if (Vaccine.Contains(tokens[i])) vaccinated = !tokens.Skip(i + 1).Take(3).Any(No.Contains);

        var phrase = " " + string.Join(' ', tokens) + " ";
        if (teeth is null)
        {
            if (phrase.Contains(" دودھ کے دانت ") || phrase.Contains(" کھیرا ") || phrase.Contains(" کھیڑا ") || phrase.Contains(" کھیرے ")) teeth = TeethStage.MilkTeeth;
            else if (phrase.Contains(" پورا منہ ") || phrase.Contains(" پورے منہ ") || phrase.Contains(" پورے دانت ") || phrase.Contains(" پورا دانت ")) teeth = TeethStage.Full;
        }

        var purposes = new List<ListingPurpose>();
        if (isLivestock)
        {
            if (phrase.Contains(" قربانی ") || shares == 7) purposes.Add(ListingPurpose.Qurbani);
            if (tokens.Any(MeatWords.Contains)) purposes.Add(ListingPurpose.MeatSupply);
            if (tokens.Any(DairyWords.Contains) || (phrase.Contains(" دودھ ") && !phrase.Contains(" دودھ کے دانت "))) purposes.Add(ListingPurpose.Dairy);
            if (tokens.Any(BreedingWords.Contains)) purposes.Add(ListingPurpose.Breeding);
        }
        if (isLivestock && shares is null && purposes.Contains(ListingPurpose.Qurbani) && phrase.Contains(" سات حصے ")) shares = 7;

        // 5. How often (for regular supply)
        SupplyFrequency? frequency = null;
        if (phrase.Contains(" روزانہ ") || phrase.Contains(" ہر دن ") || phrase.Contains(" ہر روز ")) frequency = SupplyFrequency.Daily;
        else if (phrase.Contains(" ماہانہ ") || phrase.Contains(" ہر مہینے ") || phrase.Contains(" ہر ماہ ") || phrase.Contains(" مہینے میں ")) frequency = SupplyFrequency.Monthly;
        else if (phrase.Contains(" ہفتہ وار ") || phrase.Contains(" ہفتہ واری ") || phrase.Contains(" ہر ہفتے ") || phrase.Contains(" ہر ہفتہ ") || phrase.Contains(" ہفتے میں ")) frequency = SupplyFrequency.Weekly;

        var locationId = FindLocation(tokens, locations);
        var intent = FindIntent(tokens, category is not null, price is not null);

        // 6. What is still needed?
        var missing = new List<string>();
        if (category is null) missing.Add("category");
        if (intent == VoiceIntent.Sell)
        {
            if (isLivestock && purposes.Count == 0) missing.Add("purpose");
            if (price is null) missing.Add("price");
        }
        if (intent == VoiceIntent.Wanted && quantity is null) missing.Add("quantity");

        return new VoiceParseResult(
            intent, transcript, category?.Id, category?.Slug, category?.Section, purposes.Distinct().ToList(),
            isLivestock ? gender : null, isLivestock ? teeth : null, isLivestock ? breed : null, isLivestock ? ageMonths : null,
            isLivestock ? weightKg : null, isLivestock ? minWeight : null, isLivestock ? maxWeight : null,
            quantity, quantityUnit, price, priceUnit, isLivestock ? shares : null, frequency, locationId,
            isLivestock ? pregnant : null, isLivestock ? vaccinated : null, isLivestock ? milk : null, missing);
    }

    // ---- Pieces ----------------------------------------------------------------------------------

    private static (string? Slug, AnimalGender Gender, int Index) FindCategory(string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (LivestockNouns.TryGetValue(tokens[i], out var n)) return (n.Slug, n.Gender, i);
            if (OtherNouns.TryGetValue(tokens[i], out var slug)) return (slug, AnimalGender.Unknown, i);
        }
        return (null, AnimalGender.Unknown, -1);
    }

    private static string? FindBreed(string[] tokens)
    {
        foreach (var (phrase, display) in Breeds)
        {
            for (var i = 0; i + phrase.Length <= tokens.Length; i++)
            {
                var ok = true;
                for (var j = 0; j < phrase.Length && ok; j++) ok = tokens[i + j] == phrase[j];
                if (ok) return display;
            }
        }
        return null;
    }

    private static PriceUnit? FindPriceUnit(string[] t, NumberSpan price, List<NumberSpan> spans)
    {
        var starts = spans.Select(s => s.Start).ToHashSet();
        PriceUnit? Unit(string w) =>
            Kilo.Contains(w) ? Entities.PriceUnit.PerKg :
            Maund.Contains(w) ? Entities.PriceUnit.PerMaund :
            Shares.Contains(w) ? Entities.PriceUnit.PerShare :
            Bag.Contains(w) ? Entities.PriceUnit.PerBag :
            Ton.Contains(w) ? Entities.PriceUnit.PerTon :
            Litre.Contains(w) ? Entities.PriceUnit.PerLitre :
            HeadWords.Contains(w) || LivestockNouns.ContainsKey(w) ? Entities.PriceUnit.PerItem : null;

        // "... روپے فی کلو" / "... روپے کلو کے حساب سے" / "... ایک جانور کا"
        for (var i = price.End; i < Math.Min(t.Length, price.End + 5); i++)
        {
            if (starts.Contains(i)) break; // another number begins: what follows belongs to it
            if (Unit(t[i]) is { } u) return u;
        }
        // "فی کلو 800 روپے"
        for (var i = price.Start - 1; i >= Math.Max(1, price.Start - 3); i--)
            if (t[i - 1] == Per && Unit(t[i]) is { } u) return u;
        return null;
    }

    private static int? FindLocation(string[] tokens, IReadOnlyList<LocationRef> locations)
    {
        var spaced = " " + string.Join(' ', tokens) + " ";
        var squashed = string.Concat(tokens);
        foreach (var l in locations.OrderByDescending(x => x.NameUr.Length))
        {
            var name = UrduText.Normalize(l.NameUr);
            if (name.Length == 0) continue;
            if (spaced.Contains(" " + name + " ")) return l.Id;
            var sq = name.Replace(" ", "");
            if (sq.Length >= 5 && squashed.Contains(sq)) return l.Id;
            var parts = name.Split(' ');
            if (parts.Length >= 3 && spaced.Contains(" " + parts[0] + " " + parts[1] + " ")) return l.Id;
            if (parts.Length >= 2 && parts[0].Length >= 4 && !CommonFirstWords.Contains(parts[0]) && spaced.Contains(" " + parts[0] + " ")) return l.Id;
        }
        return null;
    }

    private static VoiceIntent FindIntent(string[] tokens, bool hasCategory, bool hasPrice)
    {
        var sell = Array.FindIndex(tokens, SellWords.Contains);
        var wanted = Array.FindIndex(tokens, WantedWords.Contains);
        var search = Array.FindIndex(tokens, SearchWords.Contains);

        if (sell >= 0 && (wanted < 0 || sell < wanted)) return VoiceIntent.Sell;
        if (search >= 0 && sell < 0) return VoiceIntent.Search;
        if (wanted >= 0) return VoiceIntent.Wanted;
        // "میرے پاس دو گائے ہیں، 80 ہزار" – owning something and naming a price is a sale
        var phrase = " " + string.Join(' ', tokens) + " ";
        if (hasCategory && hasPrice && (phrase.Contains(" میرے پاس ") || phrase.Contains(" میری ") || phrase.Contains(" میرا ") || phrase.Contains(" میرے "))) return VoiceIntent.Sell;
        return VoiceIntent.Unknown;
    }
}
