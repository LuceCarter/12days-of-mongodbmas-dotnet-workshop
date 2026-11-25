using System.Text.Json;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// Part 2's README and tests tell a story that depends on specific facts about the seed data: a
/// stale open order from last season, a child on exactly zero points, a toy nobody makes. Each
/// of these is a trap that a step exists to catch. If someone tidies the data and one of the
/// traps quietly disappears, a step stops teaching what it claims to — and nothing else would fail.
///
/// These read the raw JSON (no models, no database), so they run anywhere in a second.
/// </summary>
public sealed class SeedDataContractTests
{
    private sealed record Line(string Toy, int Quantity);
    private sealed record Order(string Number, string Child, string Region, string Season, string Status, List<Line> Lines);
    private sealed record Deed(string Child, string Season, string Text, int Points);

    private static readonly List<Order> Orders = JsonDocument.Parse(RepoFiles.Read("src/Shared/SeedData/orders.json"))
        .RootElement.EnumerateArray()
        .Select(o => new Order(
            o.GetProperty("orderNumber").GetString()!, o.GetProperty("child").GetString()!, o.GetProperty("region").GetString()!,
            o.GetProperty("season").GetString()!, o.GetProperty("status").GetString()!,
            o.GetProperty("lines").EnumerateArray().Select(l => new Line(l.GetProperty("toy").GetString()!, l.GetProperty("quantity").GetInt32())).ToList()))
        .ToList();

    private static readonly List<Deed> Ledger = JsonDocument.Parse(RepoFiles.Read("src/Shared/SeedData/ledger.json"))
        .RootElement.EnumerateArray()
        .Select(d => new Deed(d.GetProperty("child").GetString()!, d.GetProperty("season").GetString()!,
                              d.GetProperty("deed").GetString()!, d.GetProperty("points").GetInt32()))
        .ToList();

    private static readonly HashSet<string> Catalogue = JsonDocument.Parse(RepoFiles.Read("src/Shared/SeedData/toys.json"))
        .RootElement.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToHashSet();

    private const string Dinosaur = "Tyrannosaurus Floor Puzzle";
    private static IEnumerable<Order> OpenThisSeason => Orders.Where(o => o.Season == "current" && o.Status == "open");

    private static Dictionary<string, int> Scores(bool currentOnly) =>
        Ledger.Where(d => !currentOnly || d.Season == "current").GroupBy(d => d.Child).ToDictionary(g => g.Key, g => g.Sum(d => d.Points));

    // ---- sizes (the README and the explain steps quote these) ---------------------------------

    [Fact]
    public void The_data_has_the_sizes_the_readme_quotes()
    {
        Assert.Equal(46, Orders.Count);
        Assert.Equal(79, Ledger.Count);
        Assert.Equal(34, Catalogue.Count);
        Assert.Equal((22, 9, 5), (OpenThisSeason.Count(),
                                  Orders.Count(o => o.Season == "current" && o.Status == "packed"),
                                  Orders.Count(o => o.Season == "current" && o.Status == "delivered")));
        Assert.Equal(67, Ledger.Count(d => d.Season == "current"));
        Assert.Equal(12, Ledger.Count(d => d.Season == "previous"));
    }

    // ---- Steps 1, 3, 4: forgetting the season or the status must give a visibly wrong answer ----

    [Fact]
    public void Last_season_has_open_orders_the_elves_forgot_so_the_season_filter_matters()
    {
        Assert.Equal(2, Orders.Count(o => o.Season == "previous" && o.Status == "open"));
        Assert.Equal(24, Orders.Count(o => o.Status == "open"));   // 22 + 2: the wrong answer to Step 1
    }

    [Fact]
    public void The_dinosaur_puzzle_demand_is_inflated_by_forgetting_the_season_or_the_status()
    {
        int Quantity(IEnumerable<Order> orders) => orders.SelectMany(o => o.Lines).Where(l => l.Toy == Dinosaur).Sum(l => l.Quantity);

        Assert.Equal(11, Quantity(OpenThisSeason));
        Assert.Equal(14, Quantity(Orders.Where(o => o.Status == "open")));                  // forgot the season
        Assert.True(Quantity(Orders.Where(o => o.Season == "current")) > 11);               // forgot the status
    }

    [Fact]
    public void Several_regions_want_the_dinosaur_puzzle_inside_multi_line_orders()
    {
        var wanting = OpenThisSeason.Where(o => o.Lines.Any(l => l.Toy == Dinosaur)).ToList();

        Assert.Equal(5, wanting.Select(o => o.Region).Distinct().Count());
        Assert.Contains(wanting, o => o.Lines.Count > 1);

        // The README's Europe example: Ingrid asked for one puzzle and a train set.
        var europe = wanting.Single(o => o.Region == "Europe");
        Assert.Equal("Ingrid", europe.Child);
        Assert.Equal(1, europe.Lines.Single(l => l.Toy == Dinosaur).Quantity);
        Assert.Contains(europe.Lines, l => l.Toy == "Elf Express Train Set");

        // Without the second $match, Europe's total would include the train set.
        Assert.Equal(2, europe.Lines.Sum(l => l.Quantity));
    }

    [Fact]
    public void A_toy_with_no_open_demand_exists_for_the_empty_result_test()
    {
        Assert.DoesNotContain(OpenThisSeason.SelectMany(o => o.Lines), l => l.Toy == "Lead Soldier Set");
        Assert.Contains(Orders.SelectMany(o => o.Lines), l => l.Toy == "Lead Soldier Set");   // it IS ordered — just not open
    }

    [Fact]
    public void The_README_example_order_is_what_the_README_says_it_is()
    {
        var sofia = Orders.Single(o => o.Number == "ORD-1009");
        Assert.Equal(("Sofia", "North America", "current", "open"), (sofia.Child, sofia.Region, sofia.Season, sofia.Status));
        Assert.Equal([(Dinosaur, 3), ("Triceratops Night Light", 1)], sofia.Lines.Select(l => (l.Toy, l.Quantity)).ToList());
    }

    // ---- Step 7: the join ---------------------------------------------------------------------

    [Fact]
    public void Two_open_lines_ask_for_a_toy_that_is_not_in_the_catalogue()
    {
        var lines = OpenThisSeason.SelectMany(o => o.Lines.Select(l => (Order: o, Line: l))).ToList();
        var unknown = lines.Where(x => !Catalogue.Contains(x.Line.Toy)).ToList();

        Assert.Equal(50, lines.Count);
        Assert.Equal(2, unknown.Count);
        Assert.All(unknown, x => Assert.Equal("Unicorn Saddle", x.Line.Toy));
        Assert.Equal(["Elsa", "Koa"], unknown.Select(x => x.Order.Child).OrderBy(c => c).ToArray());
    }

    [Fact]
    public void Every_other_ordered_toy_is_in_the_catalogue_and_no_line_carries_a_price()
    {
        Assert.All(Orders.SelectMany(o => o.Lines).Where(l => l.Toy != "Unicorn Saddle"), l => Assert.Contains(l.Toy, Catalogue));

        var raw = JsonDocument.Parse(RepoFiles.Read("src/Shared/SeedData/orders.json")).RootElement;
        var fields = raw.EnumerateArray().SelectMany(o => o.GetProperty("lines").EnumerateArray())
                        .SelectMany(l => l.EnumerateObject().Select(p => p.Name)).Distinct().OrderBy(n => n).ToList();
        Assert.Equal(["quantity", "toy"], fields);   // the join supplies the price; orders must not hold a stale copy
    }

    [Fact]
    public void The_index_steps_have_something_to_show_46_examined_without_one_22_with_one()
    {
        // Step 8's test expects every order to be examined with no index; Step 9's expects only the open ones.
        Assert.Equal(46, Orders.Count);
        Assert.Equal(22, OpenThisSeason.Count());
        Assert.True(OpenThisSeason.Count() < Orders.Count);
    }

    // ---- Steps 2, 5: the ledger ---------------------------------------------------------------

    [Fact]
    public void The_verdict_mix_is_twenty_nice_six_naughty_and_one_on_exactly_zero()
    {
        var scores = Scores(currentOnly: true);

        Assert.Equal(26, scores.Count);
        Assert.Equal(20, scores.Values.Count(p => p >= 0));
        Assert.Equal(6, scores.Values.Count(p => p < 0));
        Assert.Equal(["Emma"], scores.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToArray());
    }

    [Fact]
    public void Ignoring_the_season_flips_exactly_the_verdicts_the_tests_name()
    {
        var current = Scores(currentOnly: true);
        var all = Scores(currentOnly: false);

        var flipped = current.Where(kv => (kv.Value >= 0) != (all[kv.Key] >= 0)).Select(kv => kv.Key).OrderBy(c => c).ToArray();
        Assert.Equal(["Hemi", "Oliver", "Sofia"], flipped);

        // The README and Step 2's test quote these numbers.
        Assert.Equal(14, current["Sofia"]);
        Assert.Equal(-20, Ledger.Single(d => d.Child == "Sofia" && d.Season == "previous").Points);
        Assert.Equal(-11, current["Hemi"]);
    }

    [Fact]
    public void One_child_has_only_last_seasons_deeds()
    {
        var current = Scores(currentOnly: true);
        var all = Scores(currentOnly: false);

        Assert.DoesNotContain("Charlotte", current.Keys);
        Assert.Contains("Charlotte", all.Keys);
    }

    [Fact]
    public void Hemi_spilled_the_cocoa_and_it_was_the_worst_deed_of_the_season()
    {
        var worst = Ledger.Where(d => d.Season == "current").MinBy(d => d.Points)!;

        Assert.Equal(("Hemi", -10), (worst.Child, worst.Points));
        Assert.Contains("cocoa", worst.Text);
        Assert.Contains(Ledger, d => d.Child == "Hemi" && d.Season == "current" && d.Points == -4 && d.Text.Contains("elves"));
    }

    // ---- Step 6: the top of the list ----------------------------------------------------------

    [Fact]
    public void The_top_five_kind_deeds_are_distinct_so_there_are_no_ties_to_break()
    {
        var points = Ledger.Where(d => d.Season == "current" && d.Points > 0).Select(d => d.Points).OrderByDescending(p => p).ToList();

        Assert.Equal([10, 9, 8, 7, 6], points.Take(5).ToArray());
        Assert.True(points[5] < 6, "a sixth deed worth 6 or more would make fifth place a tie");
    }

    [Fact]
    public void A_deed_from_last_season_outscores_every_current_one()
    {
        Assert.Equal(12, Ledger.Where(d => d.Season == "previous").Max(d => d.Points));
        Assert.True(12 > Ledger.Where(d => d.Season == "current").Max(d => d.Points));
    }

    [Fact]
    public void The_ledger_file_is_in_no_useful_order_so_a_limit_before_a_sort_gives_the_wrong_five()
    {
        var points = Ledger.Select(d => d.Points).ToList();
        Assert.NotEqual(points.OrderByDescending(p => p).ToList(), points);
        Assert.NotEqual(points.OrderBy(p => p).ToList(), points);

        var positives = Ledger.Where(d => d.Season == "current" && d.Points > 0).ToList();
        var limitFirst = positives.Take(5).Select(d => d.Points).ToList();
        var sortFirst = positives.OrderByDescending(d => d.Points).Take(5).Select(d => d.Points).ToList();
        Assert.NotEqual(sortFirst, limitFirst);
    }

    // ---- Part 3A: the catalogue, searched ---------------------------------------------------

    private sealed record Toy(string Name, string Description, string Category, List<string> Tags, int MinAge, int MaxAge,
                              bool InStock, bool Discontinued, decimal Price);

    private static readonly List<Toy> Toys = JsonDocument.Parse(RepoFiles.Read("src/Shared/SeedData/toys.json"))
        .RootElement.EnumerateArray()
        .Select(t => new Toy(
            t.GetProperty("name").GetString()!, t.GetProperty("description").GetString()!, t.GetProperty("category").GetString()!,
            t.GetProperty("tags").EnumerateArray().Select(x => x.GetString()!).ToList(),
            t.GetProperty("minAge").GetInt32(), t.GetProperty("maxAge").GetInt32(),
            t.GetProperty("inStock").GetBoolean(), t.GetProperty("discontinued").GetBoolean(), t.GetProperty("price").GetDecimal()))
        .ToList();

    private static bool HasWord(string text, string word) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static List<Toy> Mentioning(string word) =>
        Toys.Where(t => HasWord(t.Name, word) || HasWord(t.Description, word) || t.Tags.Any(g => HasWord(g, word))).ToList();

    [Fact]
    public void Search_has_three_puzzles_for_equals_to_find_on_a_token_and_miss_on_a_dynamic_index()
    {
        Assert.Equal(3, Toys.Count(t => t.Category == "puzzle"));
    }

    [Fact]
    public void Search_dinosaur_is_said_once_as_a_singular_and_once_as_a_plural_so_the_english_analyzer_matters()
    {
        var singular = Toys.Where(t => HasWord(t.Description, "dinosaur")).Select(t => t.Name).ToList();
        var plural = Toys.Where(t => HasWord(t.Description, "dinosaurs")).Select(t => t.Name).ToList();

        Assert.Equal(["Dinosaur Dig Kit"], singular);
        Assert.Equal(["Brachiosaurus Balance Game"], plural);
        Assert.Equal(8, Toys.Count(t => t.Tags.Contains("dinosaur")));
    }

    [Fact]
    public void Search_seven_toys_mention_space_and_two_of_them_cost_fifteen_pounds_or_less()
    {
        Assert.Equal(7, Mentioning("space").Count);
        Assert.Equal(["Glow Stars, Pack of 100", "Solar System Puzzle"], Mentioning("space").Where(t => t.Price <= 15m).Select(t => t.Name).Order().ToList());
    }

    [Fact]
    public void Search_four_space_toys_suit_a_six_year_old_who_can_have_one_now()
    {
        var gifts = Mentioning("space").Where(t => t.InStock && !t.Discontinued && t.MinAge <= 6 && 6 <= t.MaxAge).ToList();
        Assert.Equal(4, gifts.Count);
    }

    [Fact]
    public void Search_one_toy_is_in_stock_and_discontinued_and_suits_a_five_year_old()
    {
        // The Step 7 trap: "in stock" alone isn't "available". Only mustNot(discontinued) removes this one.
        var fishing = Toys.Single(t => t.Name == "Magnetic Fishing Game");
        Assert.True(fishing.InStock && fishing.Discontinued && fishing.MinAge <= 5 && 5 <= fishing.MaxAge);
        Assert.Contains("classic", fishing.Tags);
    }

    [Fact]
    public void Search_the_word_wooden_is_in_the_name_of_exactly_two_toys_and_the_family_tag_is_on_six_that_tie()
    {
        Assert.Equal(["Skipping Rope, Wooden Handles", "Wooden Tool Bench"], Toys.Where(t => HasWord(t.Name, "wooden")).Select(t => t.Name).Order().ToList());

        // Six toys have 'family' exactly once, in a tag and nowhere else: the same score, so a tie.
        var family = Mentioning("family");
        Assert.Equal(6, family.Count);
        Assert.All(family, t => Assert.True(t.Tags.Contains("family") && !HasWord(t.Name, "family") && !HasWord(t.Description, "family")));
    }

    [Fact]
    public void Search_autocomplete_wo_completes_four_names_one_on_its_last_word()
    {
        var names = Toys.Where(t => t.Name.Split([' ', ',', ':'], StringSplitOptions.RemoveEmptyEntries)
                                         .Any(w => w.StartsWith("wo", StringComparison.OrdinalIgnoreCase))).Select(t => t.Name).ToList();

        Assert.Equal(4, names.Count);
        Assert.Contains("Jigsaw: Santa's Workshop", names);
    }

    // ---- Part 3B: the vectors ---------------------------------------------------------------

    private const string ToyVectorsPath = "src/Shared/SeedData/toy-embeddings.json";
    private const string QuestionVectorsPath = "src/Shared/SeedData/query-embeddings.json";

    private sealed record VectorFile(string Model, int Dimensions, string InputType, Dictionary<string, float[]> Embeddings);

    private static VectorFile ReadVectors(string path) =>
        JsonSerializer.Deserialize<VectorFile>(RepoFiles.Read(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    /// <summary>The questions Part 3B's tests and README use. If one goes missing from the vectors file, a test
    /// there throws a KeyNotFoundException, so say so here, in words.</summary>
    private static readonly string[] QuestionsPartThreeBUses =
    [
        "something calming for a restless child at bedtime",
        "a gift for a budding palaeontologist",
        "a rainy-day game for the whole family",
        "look at the night sky",
    ];

    [Fact]
    public void Part_3B_vectors_are_complete_and_consistent_once_generated()
    {
        // Generated by tools/embed from a real Voyage key and committed. Until then there is nothing to check,
        // so this SKIPS (visibly) rather than passes; the release guard below is what stops an empty release.
        if (!RepoFiles.Exists(ToyVectorsPath) || !RepoFiles.Exists(QuestionVectorsPath))
            Assert.Skip("Part 3B's vectors haven't been generated yet: run `dotnet run --project tools/embed` with VOYAGE_API_KEY set, and commit the two files.");

        var toys = ReadVectors(ToyVectorsPath);
        var questions = ReadVectors(QuestionVectorsPath);

        Assert.Equal("voyage-4", toys.Model);
        Assert.Equal("voyage-4", questions.Model);
        Assert.Equal(1024, toys.Dimensions);
        Assert.Equal(1024, questions.Dimensions);
        Assert.Equal("document", toys.InputType);
        Assert.Equal("query", questions.InputType);

        // One vector per toy, exactly, of exactly the declared length: a vector of another length would be
        // skipped by the index, silently (see the dimensions trap).
        Assert.Equal(Catalogue.Order(), toys.Embeddings.Keys.Order());
        Assert.All(toys.Embeddings, kv => Assert.True(kv.Value.Length == 1024, $"'{kv.Key}' has {kv.Value.Length} dimensions, not 1024."));
        Assert.All(questions.Embeddings, kv => Assert.True(kv.Value.Length == 1024, $"'{kv.Key}' has {kv.Value.Length} dimensions, not 1024."));

        foreach (var question in QuestionsPartThreeBUses)
            Assert.True(questions.Embeddings.ContainsKey(question), $"query-embeddings.json has no vector for \"{question}\", which Part 3B uses.");

        // A vector of all zeros has no direction, and cosine similarity is undefined for it.
        Assert.All(toys.Embeddings.Concat(questions.Embeddings), kv => Assert.Contains(kv.Value, x => x != 0f));
    }

    [Fact]
    public void Part_3B_cannot_be_released_without_its_vectors()
    {
        var released = RepoFiles.Parts().Single(p => p.Id == "3B").Available;
        var generated = RepoFiles.Exists(ToyVectorsPath) && RepoFiles.Exists(QuestionVectorsPath);

        Assert.True(!released || generated,
            "Part 3B is marked Available, but its vectors (src/Shared/SeedData/toy-embeddings.json and query-embeddings.json) " +
            "aren't in the repository, so its tests can't run. Generate them with tools/embed and commit them first.");
    }

    // ---- Part 4A: the handbook ----------------------------------------------------------------

    private const string HandbookPath = "src/Shared/SeedData/elf-handbook.md";
    private const string HandbookChunksPath = "src/Shared/SeedData/handbook-chunks.json";
    private const string HandbookQuestionsPath = "src/Shared/SeedData/handbook-questions.json";

    private sealed record ChunkRow(string Id, string Section, string Audience, string Source, int ChunkIndex, string Text, float[] Embedding);

    private sealed record ChunkFile(string Model, int Dimensions, int ChunkWords, int OverlapWords, List<ChunkRow> Chunks);

    /// <summary>The questions Part 4A's tests use. A missing one would be a KeyNotFoundException deep in a test; say so here, in words.</summary>
    private static readonly string[] QuestionsPartFourAUses =
    [
        "How do I know if a toy is safe for a baby?",
        "What should I do when a reindeer won't eat?",
        "How do I wrap something fragile?",
        "In what order do presents go onto the sleigh?",
        "A child wrote to say they are sorry for being naughty. How is that counted?",
        "I'm worn out and it's the busiest week of the year.",
        "Someone knocked over a pot of glitter by the machines.",
        "Can I show my family around the toy floor?",
        "What should I do if a machine catches fire?",
        "What is the capital of France?",
    ];

    /// <summary>The handbook cut into chunks by this test project's own few lines (it is deliberately not the learner's code).</summary>
    private static List<(string Id, string Section, string Audience, int Index, string Text)> ExpectedHandbookChunks(int size, int overlap)
    {
        var result = new List<(string, string, string, int, string)>();

        foreach (var block in RepoFiles.Read(HandbookPath).Replace("\r\n", "\n").Split("\n## ").Skip(1))
        {
            var lines = block.Split('\n');
            var section = lines[0].Trim();
            var audience = lines.First(l => l.StartsWith("Audience:", StringComparison.Ordinal))["Audience:".Length..].Trim();
            var words = string.Join(' ', lines.Skip(1).Where(l => !l.StartsWith("Audience:", StringComparison.Ordinal)))
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var index = 0;
            for (var start = 0; start < words.Length; start += size - overlap)
            {
                var id = $"{string.Join('-', section.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))}-{index}";
                result.Add((id, section, audience, index++, string.Join(' ', words.Skip(start).Take(size))));
                if (start + size >= words.Length) break;
            }
        }

        return result;
    }

    [Fact]
    public void The_elf_handbook_has_ten_sections_each_with_an_audience()
    {
        var chunks = ExpectedHandbookChunks(80, 20);

        Assert.Equal(10, chunks.Select(c => c.Section).Distinct().Count());
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Audience), $"'{c.Section}' has no audience."));
        Assert.Contains(chunks, c => c.Audience == "all-elves");
        Assert.Contains(chunks, c => c.Audience == "toy-makers");
    }

    [Fact]
    public void Part_4A_vectors_are_complete_and_match_the_handbook_text_once_generated()
    {
        if (!RepoFiles.Exists(HandbookChunksPath) || !RepoFiles.Exists(HandbookQuestionsPath))
            Assert.Skip("Part 4A's vectors haven't been generated yet: run `dotnet run --project tools/embed -- --handbook` with VOYAGE_API_KEY set, and commit the two files.");

        var file = JsonSerializer.Deserialize<ChunkFile>(RepoFiles.Read(HandbookChunksPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var questions = ReadVectors(HandbookQuestionsPath);

        Assert.Equal("voyage-4", file.Model);
        Assert.Equal("voyage-4", questions.Model);
        Assert.Equal(1024, file.Dimensions);
        Assert.Equal("query", questions.InputType);

        // The vectors describe the TEXT THEY WERE MADE FROM. If the handbook has been edited since, they describe
        // something that no longer exists, and nothing else would notice. So the shipped chunks must be exactly what
        // the handbook produces today.
        var expected = ExpectedHandbookChunks(file.ChunkWords, file.OverlapWords);
        Assert.True(expected.Select(e => (e.Id, e.Text)).SequenceEqual(file.Chunks.Select(c => (c.Id, c.Text))),
            "handbook-chunks.json doesn't match src/Shared/SeedData/elf-handbook.md. The handbook was edited after the vectors were made: " +
            "regenerate them with `dotnet run --project tools/embed -- --handbook`.");

        Assert.Equal(expected.Select(e => (e.Section, e.Audience, e.Index)), file.Chunks.Select(c => (c.Section, c.Audience, c.ChunkIndex)));
        Assert.All(file.Chunks, c => Assert.Equal($"The Elf Handbook › {c.Section}", c.Source));

        // A vector of another length would be skipped by the index, silently (the dimensions trap), and a chunk with no
        // direction can't be compared.
        Assert.All(file.Chunks, c => Assert.True(c.Embedding.Length == 1024, $"'{c.Id}' has {c.Embedding.Length} dimensions, not 1024."));
        Assert.All(file.Chunks, c => Assert.Equal(1.0, Math.Sqrt(c.Embedding.Sum(x => (double)x * x)), precision: 2));
        Assert.All(questions.Embeddings, kv => Assert.True(kv.Value.Length == 1024, $"'{kv.Key}' has {kv.Value.Length} dimensions, not 1024."));

        foreach (var question in QuestionsPartFourAUses)
            Assert.True(questions.Embeddings.ContainsKey(question), $"handbook-questions.json has no vector for \"{question}\", which Part 4A uses.");
    }

    [Fact]
    public void Part_4A_cannot_be_released_without_its_vectors()
    {
        var released = RepoFiles.Parts().Single(p => p.Id == "4A").Available;
        var generated = RepoFiles.Exists(HandbookChunksPath) && RepoFiles.Exists(HandbookQuestionsPath);

        Assert.True(!released || generated,
            "Part 4A is marked Available, but its vectors (src/Shared/SeedData/handbook-chunks.json and handbook-questions.json) " +
            "aren't in the repository, so its tests can't run. Generate them with tools/embed --handbook and commit them first.");
    }
}
