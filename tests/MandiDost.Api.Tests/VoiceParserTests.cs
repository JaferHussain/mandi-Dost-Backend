using MandiDost.Api.Data;
using MandiDost.Api.Entities;
using MandiDost.Api.Services.Voice;

namespace MandiDost.Api.Tests;

public class VoiceParserTests
{
    private static readonly CategoryRef[] Categories = [.. SeedData.Categories.Select(c => new CategoryRef(c.Id, c.Slug, c.Section))];
    private static readonly LocationRef[] Locations = [.. SeedData.Locations.Select(l => new LocationRef(l.Id, l.NameUr))];

    private static VoiceParseResult Parse(string text) => UrduVoiceParser.Parse(text, Categories, Locations);

    // ---- Numbers ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("80 ہزار", 80_000)]
    [InlineData("۸۰ ہزار", 80_000)]                     // Urdu digits
    [InlineData("٨٠ ہزار", 80_000)]                     // Arabic-Indic digits
    [InlineData("80,000", 80_000)]
    [InlineData("اسی ہزار", 80_000)]
    [InlineData("ایک لاکھ بیس ہزار", 120_000)]
    [InlineData("ڈیڑھ لاکھ", 150_000)]
    [InlineData("ڈھائی لاکھ", 250_000)]
    [InlineData("ساڑھے تین ہزار", 3_500)]
    [InlineData("سوا لاکھ", 125_000)]
    [InlineData("پونے دو لاکھ", 175_000)]
    [InlineData("دو سو پچاس", 250)]
    [InlineData("پچیس ہزار پانچ سو", 25_500)]
    [InlineData("ایک کروڑ", 10_000_000)]
    [InlineData("ہزار", 1_000)]
    [InlineData("1.5 لاکھ", 150_000)]
    public void Reads_numbers_the_way_people_say_them(string spoken, double expected)
    {
        var spans = UrduNumbers.FindSpans(UrduText.Tokenize(spoken));
        var one = Assert.Single(spans);
        Assert.Equal(expected, one.Value, 3);
    }

    [Fact]
    public void Two_separate_numbers_are_not_added_together()
    {
        var spans = UrduNumbers.FindSpans(UrduText.Tokenize("دو تین"));
        Assert.Equal([2.0, 3.0], spans.Select(s => s.Value));
    }

    [Theory]
    [InlineData("80ہزار")]
    [InlineData("  ۸۰   ہزار  ")]
    public void Digits_stuck_to_words_are_separated(string spoken) =>
        Assert.Equal(80_000, Assert.Single(UrduNumbers.FindSpans(UrduText.Tokenize(spoken))).Value);

    // ---- Selling -------------------------------------------------------------------------------------

    [Fact]
    public void A_full_sale_sentence_fills_the_form()
    {
        var r = Parse("میرے پاس دو ساہیوال گائے ہیں، دو دانت، 80 ہزار روپے فی جانور، لودھراں");
        Assert.Equal(VoiceIntent.Sell, r.Intent);
        Assert.Equal("cow", r.CategorySlug);
        Assert.Equal(1, r.CategoryId);
        Assert.Equal(ProductSection.Livestock, r.Section);
        Assert.Equal(2m, r.Quantity);
        Assert.Equal(QuantityUnit.Head, r.QuantityUnit);
        Assert.Equal("ساہیوال", r.Breed);
        Assert.Equal(TeethStage.Two, r.Teeth);
        Assert.Equal(80_000, r.Price);
        Assert.Equal(PriceUnit.PerItem, r.PriceUnit);
        Assert.Equal(1, r.LocationId);
        Assert.Equal(AnimalGender.Female, r.Gender);
        Assert.Equal(["purpose"], r.Missing); // we never guess what the animal is for
    }

    [Fact]
    public void A_goat_for_sale_with_only_a_price_says_what_is_missing()
    {
        var r = Parse("بکرا بیچنا ہے پچاس ہزار روپے");
        Assert.Equal(VoiceIntent.Sell, r.Intent);
        Assert.Equal("goat", r.CategorySlug);
        Assert.Equal(AnimalGender.Male, r.Gender);
        Assert.Equal(50_000, r.Price);
        Assert.Contains("purpose", r.Missing);
        Assert.DoesNotContain("price", r.Missing);
    }

    [Fact]
    public void Qurbani_cow_in_seven_shares()
    {
        var r = Parse("قربانی کے لیے ساہیوال گائے بیچنی ہے سات حصے ایک حصے کی قیمت 45 ہزار روپے");
        Assert.Equal(VoiceIntent.Sell, r.Intent);
        Assert.Contains(ListingPurpose.Qurbani, r.Purposes);
        Assert.Equal(7, r.Shares);
        Assert.Equal(45_000, r.Price);
        Assert.Equal(PriceUnit.PerShare, r.PriceUnit);
        Assert.DoesNotContain("purpose", r.Missing);
    }

    [Fact]
    public void Age_in_years_and_a_half_and_a_price_in_lakhs()
    {
        var r = Parse("ڈیڑھ سال کا بچھڑا ایک لاکھ بیس ہزار روپے بیچنا ہے");
        Assert.Equal(18, r.AgeMonths);
        Assert.Equal(AnimalGender.Male, r.Gender);
        Assert.Equal(120_000, r.Price);
        Assert.Equal("cow", r.CategorySlug);
    }

    [Fact]
    public void Dairy_buffaloes_with_price_per_head_and_city()
    {
        var r = Parse("دس بھینس بیچنی ہیں دودھ دینے والی 90 ہزار فی بھینس ملتان");
        Assert.Equal("buffalo", r.CategorySlug);
        Assert.Equal(10m, r.Quantity);
        Assert.Equal(90_000, r.Price);
        Assert.Equal(PriceUnit.PerItem, r.PriceUnit);
        Assert.Contains(ListingPurpose.Dairy, r.Purposes);
        Assert.Equal(5, r.LocationId);
    }

    [Fact]
    public void Weight_in_kilos_and_in_maunds_and_glued_teeth_words()
    {
        var a = Parse("بیتل بکرا بیچنا ہے دودانت 45 کلو 95 ہزار روپے");
        Assert.Equal("goat", a.CategorySlug);
        Assert.Equal(TeethStage.Two, a.Teeth);
        Assert.Equal(45, a.WeightKg);
        Assert.Equal(95_000, a.Price);
        Assert.Equal("بیتل", a.Breed);

        var b = Parse("بیل بیچنا ہے وزن آٹھ من قیمت دو لاکھ روپے");
        Assert.Equal(320, b.WeightKg); // 8 maunds × 40 kg
        Assert.Equal(200_000, b.Price);
    }

    [Fact]
    public void A_price_without_the_word_rupees_is_still_found()
    {
        var r = Parse("ایک بکری چالیس ہزار بیچنی ہے");
        Assert.Equal(1m, r.Quantity);
        Assert.Equal(40_000, r.Price);
        Assert.Equal(AnimalGender.Female, r.Gender);
    }

    [Fact]
    public void Camel_in_another_city()
    {
        var r = Parse("اونٹ بیچنا ہے ڈیڑھ لاکھ روپے بہاولپور");
        Assert.Equal("camel", r.CategorySlug);
        Assert.Equal(150_000, r.Price);
        Assert.Equal(7, r.LocationId);
    }

    [Fact]
    public void Crops_are_counted_in_maunds_with_a_price_per_maund()
    {
        var r = Parse("گندم بیچنی ہے پچاس من چار ہزار روپے من کے حساب سے");
        Assert.Equal("wheat", r.CategorySlug);
        Assert.Equal(ProductSection.Crops, r.Section);
        Assert.Equal(50m, r.Quantity);
        Assert.Equal(QuantityUnit.Maund, r.QuantityUnit);
        Assert.Equal(4_000, r.Price);
        Assert.Equal(PriceUnit.PerMaund, r.PriceUnit);
        Assert.Null(r.WeightKg); // animal fields stay empty for crops
        Assert.Empty(r.Purposes);
    }

    [Fact]
    public void Owning_something_and_naming_a_price_counts_as_selling()
    {
        var r = Parse("میرے پاس ایک گائے ہے ستر ہزار روپے");
        Assert.Equal(VoiceIntent.Sell, r.Intent);
    }

    // ---- Wanted and search ---------------------------------------------------------------------------

    [Fact]
    public void A_hotel_needs_goats_every_week()
    {
        var r = Parse("ہوٹل کے لیے پانچ بکرے چاہییں ہر ہفتے تیس سے چالیس کلو");
        Assert.Equal(VoiceIntent.Wanted, r.Intent);
        Assert.Equal("goat", r.CategorySlug);
        Assert.Equal(5m, r.Quantity);
        Assert.Contains(ListingPurpose.MeatSupply, r.Purposes);
        Assert.Equal(SupplyFrequency.Weekly, r.Frequency);
        Assert.Equal(30, r.MinWeightKg);
        Assert.Equal(40, r.MaxWeightKg);
        Assert.Null(r.WeightKg);
        Assert.Empty(r.Missing);
    }

    [Fact]
    public void A_wanted_request_with_a_budget_and_a_monthly_supply()
    {
        var r = Parse("مجھے ماہانہ دس دنبے چاہییں بجٹ پچھتر ہزار روپے فی دنبہ ملتان");
        Assert.Equal(VoiceIntent.Wanted, r.Intent);
        Assert.Equal("sheep", r.CategorySlug);
        Assert.Equal(10m, r.Quantity);
        Assert.Equal(75_000, r.Price);
        Assert.Equal(PriceUnit.PerItem, r.PriceUnit);
        Assert.Equal(SupplyFrequency.Monthly, r.Frequency);
        Assert.Equal(5, r.LocationId);
    }

    [Fact]
    public void Asking_to_see_animals_nearby_is_a_search()
    {
        var r = Parse("میرے قریب گائے دکھاؤ");
        Assert.Equal(VoiceIntent.Search, r.Intent);
        Assert.Equal("cow", r.CategorySlug);
        Assert.Empty(r.Missing);
    }

    [Fact]
    public void Buying_wording_is_a_wanted_request()
    {
        Assert.Equal(VoiceIntent.Wanted, Parse("مجھے ایک بھینس خریدنی ہے").Intent);
        Assert.Equal(VoiceIntent.Wanted, Parse("گندم خریدنی ہے").Intent);
    }

    [Fact]
    public void Selling_wins_over_the_word_want()
    {
        // "چاہتا ہوں" is only "I would like to", not a request
        Assert.Equal(VoiceIntent.Sell, Parse("میں اپنی گائے بیچنا چاہتا ہوں").Intent);
    }

    // ---- Things it must not invent ---------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("السلام علیکم کیا حال ہے")]
    public void Chit_chat_gives_nothing(string spoken)
    {
        var r = Parse(spoken);
        Assert.Equal(VoiceIntent.Unknown, r.Intent);
        Assert.Null(r.CategoryId);
        Assert.Null(r.Price);
        Assert.Null(r.Quantity);
        Assert.Null(r.LocationId);
    }

    [Fact]
    public void A_two_is_not_taken_for_teeth_unless_the_word_teeth_follows()
    {
        var r = Parse("دو بکرے بیچنے ہیں");
        Assert.Equal(2m, r.Quantity);
        Assert.Null(r.Teeth);
    }

    [Fact]
    public void Ages_are_not_confused_with_prices_or_quantities()
    {
        var r = Parse("بھیڑ بیچنی ہے تین سال کی 30 ہزار روپے");
        Assert.Equal(36, r.AgeMonths);
        Assert.Equal(30_000, r.Price);
        Assert.Null(r.Quantity);
    }

    [Fact]
    public void Milk_teeth_and_full_mouth_phrases()
    {
        Assert.Equal(TeethStage.MilkTeeth, Parse("بکرا بیچنا ہے دودھ کے دانت").Teeth);
        Assert.Equal(TeethStage.Full, Parse("گائے بیچنی ہے پورا منہ").Teeth);
        Assert.Equal(TeethStage.Six, Parse("بیل بیچنا ہے چھ دانت").Teeth);
    }

    [Fact]
    public void Milk_in_litres_and_pregnancy_for_a_cow()
    {
        var r = Parse("گائے بیچنی ہے روزانہ بیس لیٹر دودھ گابھن ہے");
        Assert.Equal(20m, r.MilkLitresPerDay);
        Assert.True(r.IsPregnant);
    }

    [Fact]
    public void Cities_with_several_words_are_found_by_part_of_the_name()
    {
        Assert.Equal(4, Parse("بکرا بیچنا ہے جلال پور").LocationId);
        Assert.Equal(3, Parse("بکرا بیچنا ہے کہروڑ").LocationId);
        Assert.Equal(2, Parse("بکرا بیچنا ہے دنیا پور").LocationId);
        Assert.Equal(2, Parse("بکرا بیچنا ہے دنیاپور").LocationId); // written without a space
        Assert.Null(Parse("بکرا بیچنا ہے دنیا میں").LocationId);       // "دنیا" alone is just the word "world"
    }

    [Fact]
    public void Arabic_letter_variants_from_keyboards_and_speech_services_are_accepted()
    {
        var r = Parse("ميرے پاس دو گاۓ ہيں اسى ہزار روپے");  // ي ك ۓ spelled the Arabic way
        Assert.Equal("cow", r.CategorySlug);
        Assert.Equal(2m, r.Quantity);
        Assert.Equal(80_000, r.Price);
    }

    [Fact]
    public void The_transcript_is_returned_as_heard()
    {
        Assert.Equal("گائے   بیچنی ہے".Replace("   ", " "), Parse("  گائے   بیچنی ہے ").Transcript);
    }
}
