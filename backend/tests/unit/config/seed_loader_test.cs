using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CurioDistrict.Config;

namespace CurioDistrict.Tests.Unit.Config;

/// <summary>Story006 AC1–3: actual imported fixtures and independent invalid-input controls.</summary>
public class SeedLoaderTests
{
    private readonly SeedProfileLoader _loader = new();

    [Fact]
    public void Two_profile_versions_load_through_one_loader_and_report_explicit_versions()
    {
        var first = _loader.LoadJson(Fixture("0.1"));
        var second = _loader.LoadJson(Fixture("0.2"));
        Assert.Equal("0.1", first.ConfigVersion);
        Assert.Equal("0.2", second.ConfigVersion);
        Assert.True(first.ParsedConfigVersion!.CompareTo(second.ParsedConfigVersion) < 0);
        Assert.Equal("0.1.0", first.Metadata.Version);
    }

    [Fact]
    public void Actual_imported_seed_loads_cleanly_and_repeated_loads_are_structurally_equal()
    {
        var json = Fixture("0.1");
        var first = _loader.LoadJson(json);
        var second = _loader.LoadJson(json);
        Assert.Empty(SeedValidator.Validate(first));
        Assert.Equal(4, first.Products.Count);
        Assert.Equal(36, first.ItemDefinitions.Count);
        Assert.Equal(4, first.LootPools.Count);
        Assert.Equal(10, first.Fixtures.Count);
        Assert.Equal(8, first.NpcModels.Count);
        Assert.Equal("DEV_ONLY_NOT_PRODUCTION_BALANCED", first.Metadata.Status);
        Assert.Equal(JsonSerializer.Serialize(first, SeedProfileJson.Options),
            JsonSerializer.Serialize(second, SeedProfileJson.Options));
        using var original = JsonDocument.Parse(json);
        using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(first, SeedProfileJson.Options));
        Assert.True(JsonElement.DeepEquals(original.RootElement, roundTrip.RootElement));
    }

    [Fact]
    public void Imported_card_weights_match_the_final_source_correction()
    {
        var profile = _loader.LoadJson(Fixture("0.1"));
        var pool = Assert.Single(profile.LootPools, p => p.PoolId == "POOL-DMO-S1");
        Assert.Equal(new[] { 1100, 1100, 1100, 1100, 1100, 933, 933, 934, 600, 600, 400, 100 },
            pool.Rewards.Select(r => r.Weight));
    }

    [Fact]
    public void Missing_version_is_rejected_even_when_source_metadata_has_a_version()
    {
        var node = Baseline();
        node.Remove("configVersion");
        Assert.Contains("configVersion", Failure(node).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad-version")]
    [InlineData("-1.2")]
    [InlineData("0")]
    [InlineData("0.1.2")]
    [InlineData("1. 2")]
    public void Unparseable_version_names_the_field_and_offending_value(string value)
    {
        var node = Baseline();
        node["configVersion"] = value;
        var exception = Failure(node);
        Assert.Contains("configVersion", exception.Message);
        Assert.Contains($"'{value}'", exception.Message);
    }

    [Theory]
    [InlineData(9999)]
    [InlineData(10001)]
    public void Invalid_pool_total_names_the_pool_entries_and_actual_total(int total)
    {
        var node = Baseline();
        node["lootPools"]![0]!["rewards"]![0]!["weight"] = 1834 + total - 10000;
        var exception = Failure(node);
        Assert.Contains("POOL-PHO-S1", exception.Message);
        Assert.Contains("PHO-01", exception.Message);
        Assert.Contains("weight", exception.Message);
        Assert.Contains(total.ToString(), exception.Message);
    }

    [Fact]
    public void Zero_weight_is_accepted_when_pool_total_remains_exactly_10000()
    {
        var node = Baseline();
        var rewards = node["lootPools"]![0]!["rewards"]!;
        rewards[0]!["weight"] = 0;
        rewards[1]!["weight"] = 3667;
        Assert.Empty(SeedValidator.Validate(_loader.LoadJson(node.ToJsonString())));
    }

    [Fact]
    public void Negative_weight_is_rejected_even_when_total_is_10000()
    {
        var node = Baseline();
        var rewards = node["lootPools"]![0]!["rewards"]!;
        rewards[0]!["weight"] = -1;
        rewards[1]!["weight"] = 3668;
        var exception = Failure(node);
        Assert.Contains("lootPools['POOL-PHO-S1'].rewards['PHO-01'].weight", exception.Message);
        Assert.Contains("-1", exception.Message);
    }

    [Theory]
    [InlineData("supplierCost")]
    [InlineData("recommendedPrice")]
    [InlineData("minPrice")]
    [InlineData("maxPrice")]
    public void Negative_cost_names_the_product_and_exact_cost_field(string field)
    {
        var node = Baseline();
        node["products"]![0]![field] = -1;
        Assert.Contains($"products['BBOX-PHO-01'].{field}", Failure(node).Message);
    }

    [Fact]
    public void Unresolved_sku_names_the_referring_pool_field_and_missing_id()
    {
        var node = Baseline();
        node["lootPools"]![0]!["sourceSkuId"] = "SKU-MISSING";
        var exception = Failure(node);
        Assert.Contains("lootPools['POOL-PHO-S1'].sourceSkuId", exception.Message);
        Assert.Contains("SKU-MISSING", exception.Message);
    }

    [Fact]
    public void Multiple_semantic_defects_are_all_reported_in_one_deterministic_result()
    {
        var node = Baseline();
        node["configVersion"] = "invalid";
        node["products"]![0]!["supplierCost"] = -2;
        node["lootPools"]![0]!["sourceSkuId"] = "MISSING-SKU";
        node["lootPools"]![0]!["rewards"]![0]!["weight"] = 1833;
        var first = Failure(node);
        Assert.Equal(4, first.Errors.Count);
        Assert.Contains("configVersion", first.Message);
        Assert.Contains("BBOX-PHO-01", first.Message);
        Assert.Contains("MISSING-SKU", first.Message);
        Assert.Contains("9999", first.Message);
        Assert.Equal(first.Message, Failure(node).Message);
    }

    [Fact]
    public void Profile_cannot_redefine_basis_points_to_make_an_invalid_pool_pass()
    {
        var node = Baseline();
        node["metadata"]!["weightScale"] = 100;
        node["economy"]!["PERCENT_LOOT_SUM"] = 100;
        foreach (var pool in node["lootPools"]!.AsArray())
        {
            pool!["totalWeightExpected"] = 100;
            foreach (var reward in pool["rewards"]!.AsArray()) reward!["weight"] = 0;
            pool["rewards"]![0]!["weight"] = 100;
        }
        var exception = Failure(node);
        Assert.Contains("metadata.weightScale", exception.Message);
        Assert.Contains("must total exactly 10000, got 100", exception.Message);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Malformed_or_nonobject_document_returns_a_descriptive_validation_failure(string json)
    {
        Assert.NotEmpty(Assert.Throws<SeedValidationException>(() => _loader.LoadJson(json)).Errors);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("economy")]
    [InlineData("products")]
    [InlineData("itemDefinitions")]
    [InlineData("lootPools")]
    [InlineData("fixtures")]
    [InlineData("npcModels")]
    public void Null_required_member_is_a_validation_error_not_a_null_reference(string field)
    {
        var node = Baseline();
        node[field] = null;
        Assert.Contains(field, Failure(node).Message);
    }

    [Fact]
    public void Serializer_options_cannot_introduce_hidden_mutable_loader_state()
    {
        Assert.True(SeedProfileJson.Options.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => SeedProfileJson.Options.PropertyNameCaseInsensitive = true);
    }

    private SeedValidationException Failure(JsonObject node) =>
        Assert.Throws<SeedValidationException>(() => _loader.LoadJson(node.ToJsonString()));

    private static JsonObject Baseline() => JsonNode.Parse(Fixture("0.1"))!.AsObject();

    private static string Fixture(string version)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"dev-seed.v{version}.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
