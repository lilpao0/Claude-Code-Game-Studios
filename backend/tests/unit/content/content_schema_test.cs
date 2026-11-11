using System.Globalization;
using System.Text.Json.Nodes;
using CurioDistrict.Tools.ContentValidator;

namespace CurioDistrict.Tests.Unit.Content;

/// <summary>Story009: exercises the public content-set boundary entirely in memory.</summary>
public sealed class ContentSchemaTests
{
    [Fact]
    public void Imported_provenance_fixture_is_valid_without_modification()
    {
        using var stream = typeof(ContentSchemaTests).Assembly.GetManifestResourceStream("dev-seed.v0.1.json");
        using var reader = new StreamReader(stream!);
        var result = Validate(reader.ReadToEnd());
        Assert.True(result.IsValid, result.Format());
        Assert.Equal(4, result.PoolCount);
        Assert.Contains("pools=4", result.Format());
    }

    [Theory]
    [InlineData(9999)]
    [InlineData(10001)]
    public void Invalid_total_names_the_pool_and_observed_total(int total)
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"]![0]!["weight"] = total;
        var result = Validate(doc);
        var finding = Assert.Single(result.Violations, v => v.Kind == "PoolWeight");
        Assert.Contains("pool-0", finding.Message);
        Assert.Contains(total.ToString(CultureInfo.InvariantCulture), finding.Message);
    }

    [Fact]
    public void Negative_weight_is_rejected_even_when_total_is_10000()
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"] = JsonNode.Parse("""
            [{"definitionId":"item-a","weight":-1},{"definitionId":"item-a","weight":10001}]
            """);
        Assert.Contains(Validate(doc).Violations, v => v.Kind == "PoolWeight" && v.Message.Contains("-1"));
    }

    [Fact]
    public void Zero_weights_and_repeated_references_are_valid()
    {
        var doc = Valid();
        ((JsonArray)doc["lootPools"]![0]!["rewards"]!).Add(JsonNode.Parse("""
            {"definitionId":"item-a","weight":0}
            """));
        Assert.True(Validate(doc).IsValid);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Missing_or_fifth_pool_is_rejected(int count)
    {
        var doc = Valid();
        var pools = (JsonArray)doc["lootPools"]!;
        if (count == 3) pools.RemoveAt(3);
        else
        {
            var fifth = pools[0]!.DeepClone();
            fifth["poolId"] = "pool-extra";
            pools.Add(fifth);
        }
        Assert.Contains(Validate(doc).Violations, v => v.Kind == "PoolCount" && v.Message.Contains(count.ToString()));
    }

    [Fact]
    public void Every_reference_family_resolves_across_files()
    {
        var doc = Valid();
        var items = doc["itemDefinitions"]!.DeepClone();
        items[0]!["references"] = new JsonArray("item-b");
        ((JsonArray)items).Add(JsonNode.Parse("""{"definitionId":"item-b"}"""));
        doc.Remove("itemDefinitions");
        var result = new ContentSetValidator().Validate([
            new("pools.json", doc.ToJsonString()),
            new("items.json", new JsonObject { ["itemDefinitions"] = items }.ToJsonString())]);
        Assert.True(result.IsValid, result.Format());
    }

    [Fact]
    public void Independent_broken_references_include_referrer_and_target()
    {
        var doc = Valid();
        doc["lootPools"]![0]!["sourceSkuId"] = "missing-sku";
        doc["lootPools"]![1]!["rewards"]![0]!["definitionId"] = "missing-reward";
        doc["itemDefinitions"]![0]!["references"] = new JsonArray("missing-item");
        doc["products"]![0]!["poolId"] = "missing-pool";
        var result = Validate(doc);
        Assert.Equal(4, result.Violations.Count(v => v.Kind == "Reference"));
        foreach (var target in new[] { "missing-sku", "missing-reward", "missing-item", "missing-pool" })
            Assert.Contains(target, result.Format());
        Assert.Contains("pool-0", result.Format());
        Assert.Contains("item-a", result.Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Missing_empty_or_whitespace_declaration_is_rejected(string? id)
    {
        var doc = Valid();
        if (id is null) ((JsonObject)doc["itemDefinitions"]![0]!).Remove("definitionId");
        else doc["itemDefinitions"]![0]!["definitionId"] = id;
        Assert.Contains(Validate(doc).Violations, v => v.Kind == "Identifier");
    }

    [Fact]
    public void Same_file_duplicate_identifies_both_declarations()
    {
        var doc = Valid();
        ((JsonArray)doc["itemDefinitions"]!).Add(doc["itemDefinitions"]![0]!.DeepClone());
        var finding = Assert.Single(Validate(doc).Violations, v => v.Kind == "DuplicateId");
        Assert.Contains("item-a", finding.Message);
        Assert.Contains("[0]", finding.Message);
        Assert.Contains("[1]", finding.Message);
    }

    [Fact]
    public void Cross_file_cross_type_duplicates_use_one_global_namespace()
    {
        var result = new ContentSetValidator().Validate([
            new("base.json", Valid().ToJsonString()),
            new("other.json", """{"itemDefinitions":[{"definitionId":"sku-a"}]}""")]);
        var finding = Assert.Single(result.Violations, v => v.Kind == "DuplicateId");
        Assert.Contains("sku-a", finding.Message);
        Assert.Contains("base.json", finding.Message);
        Assert.Contains("other.json", finding.Message);
    }

    [Fact]
    public void All_defects_are_grouped_and_order_is_source_and_culture_independent()
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"]![0]!["weight"] = 9999;
        doc["lootPools"]![1]!["sourceSkuId"] = "missing-sku";
        var sources = new[] { new ContentSource("z.json", doc.ToJsonString()),
            new ContentSource("a.json", """{"itemDefinitions":[{"definitionId":"sku-a"}]}""") };
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var first = new ContentSetValidator().Validate(sources);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = new ContentSetValidator().Validate(sources.Reverse());
            Assert.False(first.IsValid);
            Assert.Equal(first.Format(), second.Format());
            Assert.Equal(new[] { "DuplicateId", "PoolWeight", "Reference" },
                first.Violations.Select(v => v.Kind).Distinct());
            Assert.Contains("[DuplicateId]", first.Format());
            Assert.Contains("[PoolWeight]", first.Format());
            Assert.Contains("[Reference]", first.Format());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"lootPools\":null}")]
    [InlineData("{\"itemDefinitions\":[null]}")]
    [InlineData("{\"itemDefinitions\":[{\"definitionId\":5}]}")]
    [InlineData("{\"itemDefinitions\":[{\"definitionId\":\"a\",\"references\":[5]}]}")]
    [InlineData("{\"unknownReferences\":[\"missing\"]}")]
    [InlineData("{\"itemDefinitions\":[{\"definitionId\":\"a\",\"outputId\":\"missing\"}]}")]
    public void Malformed_or_unsupported_input_never_silently_passes(string json)
    {
        var result = new ContentSetValidator().Validate([new("valid.json", Valid().ToJsonString()), new("bad.json", json)]);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.FilePath == "bad.json" && v.Kind is "Parse" or "Schema" or "Identifier");
    }

    [Fact]
    public void Bad_document_does_not_hide_other_document_defects()
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"]![0]!["weight"] = 9999;
        var result = new ContentSetValidator().Validate([new("broken.json", "{"), new("semantic.json", doc.ToJsonString())]);
        Assert.Contains(result.Violations, v => v.Kind == "Parse");
        Assert.Contains(result.Violations, v => v.Kind == "PoolWeight");
    }

    [Fact]
    public void Duplicate_json_member_cannot_hide_a_reference()
    {
        var result = new ContentSetValidator().Validate([
            new("valid.json", Valid().ToJsonString()),
            new("ambiguous.json", """{"itemDefinitions":[{"definitionId":"x","references":["missing"],"references":[]}]}""")]);
        Assert.Contains(result.Violations, v => v.Kind == "Schema");
        Assert.Contains(result.Violations, v => v.Kind == "Reference" && v.Message.Contains("missing"));
    }

    [Theory]
    [InlineData("products", "skuId")]
    [InlineData("itemDefinitions", "definitionId")]
    [InlineData("lootPools", "poolId")]
    [InlineData("fixtures", "fixtureId")]
    [InlineData("npcModels", "modelId")]
    public void Every_declaration_collection_checks_its_identifier(string collection, string field)
    {
        var doc = Valid();
        if (doc[collection] is not JsonArray) doc[collection] = new JsonArray(new JsonObject());
        ((JsonObject)doc[collection]![0]!).Remove(field);
        var result = Validate(doc);
        Assert.Contains(result.Violations, v => v.Kind == "Identifier" &&
            v.FilePath == "fixture.json" && v.JsonPath == $"$.{collection}[0].{field}");
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("1.5")]
    [InlineData("\"10000\"")]
    [InlineData("null")]
    public void Invalid_integer_weight_is_controlled_and_other_defects_continue(string weight)
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"]![0]!["weight"] = JsonNode.Parse(weight);
        doc["itemDefinitions"]![0]!["references"] = new JsonArray("missing-independent");
        var result = Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Kind == "Schema" && v.JsonPath.EndsWith(".weight"));
        Assert.Contains(result.Violations, v => v.Kind == "PoolWeight" && v.Message.Contains("pool-0"));
        Assert.Contains(result.Violations, v => v.Kind == "Reference" && v.Message.Contains("missing-independent"));
    }

    [Fact]
    public void Weight_accumulation_overflow_is_controlled_and_does_not_hide_other_defects()
    {
        var doc = Valid();
        doc["lootPools"]![0]!["rewards"]![0]!["weight"] = long.MaxValue;
        ((JsonArray)doc["lootPools"]![0]!["rewards"]!).Add(JsonNode.Parse("""{"definitionId":"item-a","weight":1}"""));
        doc["itemDefinitions"]![0]!["references"] = new JsonArray("missing-after-overflow");
        var result = Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Kind == "PoolWeight" &&
            v.Message.Contains("Int64") && v.Message.Contains("pool-0") && v.Message.Contains("item-a"));
        Assert.Contains(result.Violations, v => v.Kind == "Reference" && v.Message.Contains("missing-after-overflow"));
    }

    [Fact]
    public void Duplicate_root_collection_does_not_hide_its_declared_items()
    {
        var result = new ContentSetValidator().Validate([
            new("valid.json", Valid().ToJsonString()),
            new("ambiguous.json", """{"itemDefinitions":[{"definitionId":"x","references":["hidden-target"]}],"itemDefinitions":[]}""")]);
        Assert.Contains(result.Violations, v => v.Kind == "Schema");
        Assert.Contains(result.Violations, v => v.Kind == "Reference" && v.Message.Contains("hidden-target"));
    }

    private static ContentValidationResult Validate(JsonObject doc) => Validate(doc.ToJsonString());
    private static ContentValidationResult Validate(string json) => new ContentSetValidator().Validate([new("fixture.json", json)]);

    private static JsonObject Valid()
    {
        var pools = new JsonArray();
        for (var i = 0; i < 4; i++)
            pools.Add(new JsonObject { ["poolId"] = $"pool-{i}", ["sourceSkuId"] = "sku-a",
                ["rewards"] = JsonNode.Parse("""[{"definitionId":"item-a","weight":10000}]""") });
        return new JsonObject { ["products"] = JsonNode.Parse("""[{"skuId":"sku-a","poolId":"pool-0"}]"""),
            ["itemDefinitions"] = JsonNode.Parse("""[{"definitionId":"item-a"}]"""), ["lootPools"] = pools };
    }
}
