using System.Text.Json;
using System.Text.Json.Serialization;

namespace CurioDistrict.Config;

/// <summary>
/// A versioned DEV_SEED configuration profile (story 006, FND-006).
///
/// The model mirrors the source document (doc 29's machine-readable catalog)
/// field for field: every source field name and value is preserved, so a
/// profile round-trips to the same JSON it was loaded from. The one
/// <em>addition</em> is <see cref="ConfigVersion"/> — the explicit profile
/// version required by story 006 AC1 under ADR-0020. It is read from the profile and never
/// inferred; <see cref="Metadata"/> keeps the source document's own
/// <c>version</c> field ("0.1.0") exactly as authored, and the two are
/// separate values by design.
/// </summary>
public sealed record SeedProfile
{
    /// <summary>
    /// The explicit profile version. Required: a profile without it is
    /// rejected (AC1) — the runtime never infers it from any other field.
    /// </summary>
    [JsonPropertyName("configVersion")]
    public required string ConfigVersion { get; init; }

    /// <summary>The source document's own metadata, preserved verbatim.</summary>
    [JsonPropertyName("metadata")]
    public required SeedProfileMetadata Metadata { get; init; }

    /// <summary>The economy tunables, preserved verbatim.</summary>
    [JsonPropertyName("economy")]
    public required SeedEconomy Economy { get; init; }

    /// <summary>The purchasable products (the SKU-bearing entities).</summary>
    [JsonPropertyName("products")]
    public required IReadOnlyList<SeedProduct> Products { get; init; }

    /// <summary>The item definitions a loot pool can reward.</summary>
    [JsonPropertyName("itemDefinitions")]
    public required IReadOnlyList<SeedItemDefinition> ItemDefinitions { get; init; }

    /// <summary>The loot pools, each of which must total the weight scale.</summary>
    [JsonPropertyName("lootPools")]
    public required IReadOnlyList<SeedLootPool> LootPools { get; init; }

    /// <summary>The shop fixtures (shelves, racks, and decorations).</summary>
    [JsonPropertyName("fixtures")]
    public required IReadOnlyList<SeedFixture> Fixtures { get; init; }

    /// <summary>The NPC and employee behaviour models.</summary>
    [JsonPropertyName("npcModels")]
    public required IReadOnlyList<SeedNpcModel> NpcModels { get; init; }

    /// <summary>
    /// The parsed <see cref="ConfigVersion"/>. Set by the loader once the
    /// version has been parsed and validated; <c>null</c> on a profile that
    /// has not been through the loader.
    /// </summary>
    [JsonIgnore]
    public SeedConfigVersion? ParsedConfigVersion { get; init; }
}

/// <summary>The source document's metadata block, preserved verbatim.</summary>
public sealed record SeedProfileMetadata
{
    /// <summary>The product name the profile belongs to.</summary>
    [JsonPropertyName("product")]
    public required string Product { get; init; }

    /// <summary>The profile id as authored in the source.</summary>
    [JsonPropertyName("profileId")]
    public required string ProfileId { get; init; }

    /// <summary>
    /// The source document's own version string (e.g. "0.1.0"). This is the
    /// <em>source</em> version and is distinct from the profile's explicit
    /// <c>configVersion</c>; it is preserved exactly as authored.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// The balance status marker as authored (e.g.
    /// "DEV_ONLY_NOT_PRODUCTION_BALANCED"). A DEV_SEED profile is not
    /// production-balanced content.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>The currency identifier as authored.</summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    /// <summary>
    /// The loot weight scale — every pool must total exactly this many
    /// basis points (10,000 in the source profile).
    /// </summary>
    [JsonPropertyName("weightScale")]
    public required int WeightScale { get; init; }

    /// <summary>The provenance string for the source of the data.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>The authoring notes, preserved verbatim.</summary>
    [JsonPropertyName("notes")]
    public required IReadOnlyList<string> Notes { get; init; }
}

/// <summary>The economy tunables, preserved verbatim (source field names).</summary>
public sealed record SeedEconomy
{
    /// <summary>The currency name as authored.</summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    /// <summary>The starting wallet balance as authored.</summary>
    [JsonPropertyName("starting_wallet")]
    public required int StartingWallet { get; init; }

    /// <summary>The district tick duration in seconds as authored.</summary>
    [JsonPropertyName("district_tick_seconds")]
    public required int DistrictTickSeconds { get; init; }

    /// <summary>The per-district shop cap as authored.</summary>
    [JsonPropertyName("district_shops_cap")]
    public required int DistrictShopsCap { get; init; }

    /// <summary>The per-tick district demand as authored.</summary>
    [JsonPropertyName("district_tick_demand")]
    public required int DistrictTickDemand { get; init; }

    /// <summary>The cashier wage as authored.</summary>
    [JsonPropertyName("staff_wage_cashier")]
    public required int StaffWageCashier { get; init; }

    /// <summary>The restocker wage as authored.</summary>
    [JsonPropertyName("staff_wage_restocker")]
    public required int StaffWageRestocker { get; init; }

    /// <summary>The purchasing-clerk wage as authored.</summary>
    [JsonPropertyName("staff_wage_purchasing")]
    public required int StaffWagePurchasing { get; init; }

    /// <summary>The minimum wallet reserve as authored.</summary>
    [JsonPropertyName("wallet_min_reserve")]
    public required int WalletMinReserve { get; init; }

    /// <summary>The per-tick supplier spend cap as authored.</summary>
    [JsonPropertyName("supplier_spend_max_per_tick")]
    public required int SupplierSpendMaxPerTick { get; init; }

    /// <summary>Whether NPC items resell, as authored.</summary>
    [JsonPropertyName("npc_item_resale")]
    public required bool NpcItemResale { get; init; }

    /// <summary>
    /// The authored expected loot sum. It must agree with the profile's
    /// weight scale — a mismatch is a defect, because the two would tell
    /// the runtime different truths about the same pool.
    /// </summary>
    [JsonPropertyName("PERCENT_LOOT_SUM")]
    public required int PercentLootSum { get; init; }
}

/// <summary>A purchasable product (the SKU-bearing entity).</summary>
public sealed record SeedProduct
{
    /// <summary>The SKU identifier — the key every loot reference resolves against.</summary>
    [JsonPropertyName("skuId")]
    public required string SkuId { get; init; }

    /// <summary>The product kind as authored.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The series identifier as authored.</summary>
    [JsonPropertyName("seriesId")]
    public required string SeriesId { get; init; }

    /// <summary>The display name as authored.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The supplier cost — must not be negative.</summary>
    [JsonPropertyName("supplierCost")]
    public required int SupplierCost { get; init; }

    /// <summary>The recommended retail price as authored.</summary>
    [JsonPropertyName("recommendedPrice")]
    public required int RecommendedPrice { get; init; }

    /// <summary>The minimum allowed price as authored.</summary>
    [JsonPropertyName("minPrice")]
    public required int MinPrice { get; init; }

    /// <summary>The maximum allowed price as authored.</summary>
    [JsonPropertyName("maxPrice")]
    public required int MaxPrice { get; init; }

    /// <summary>Whether the product stacks, as authored.</summary>
    [JsonPropertyName("stackable")]
    public required bool Stackable { get; init; }

    /// <summary>The pool identifier this product draws from, as authored.</summary>
    [JsonPropertyName("poolId")]
    public required string PoolId { get; init; }

    /// <summary>The milestone the product belongs to, as authored.</summary>
    [JsonPropertyName("milestone")]
    public required string Milestone { get; init; }
}

/// <summary>An item definition a loot pool may reward.</summary>
public sealed record SeedItemDefinition
{
    /// <summary>The definition identifier — the key every loot reward resolves against.</summary>
    [JsonPropertyName("definitionId")]
    public required string DefinitionId { get; init; }

    /// <summary>The item kind as authored.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The series identifier as authored.</summary>
    [JsonPropertyName("seriesId")]
    public required string SeriesId { get; init; }

    /// <summary>The display name as authored.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>The rarity as authored.</summary>
    [JsonPropertyName("rarity")]
    public required string Rarity { get; init; }

    /// <summary>The prefab path as authored (the art is planned, not delivered).</summary>
    [JsonPropertyName("prefabId")]
    public required string PrefabId { get; init; }

    /// <summary>The localization key as authored.</summary>
    [JsonPropertyName("localizationKey")]
    public required string LocalizationKey { get; init; }

    /// <summary>The presentation mode as authored.</summary>
    [JsonPropertyName("presentation")]
    public required string Presentation { get; init; }
}

/// <summary>A loot pool: the weighted reward table behind a product's SKU.</summary>
public sealed record SeedLootPool
{
    /// <summary>The pool identifier.</summary>
    [JsonPropertyName("poolId")]
    public required string PoolId { get; init; }

    /// <summary>The pool's own version marker as authored (e.g. "v1").</summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// The SKU that sources this pool — it must resolve to a defined
    /// product's <c>skuId</c>.
    /// </summary>
    [JsonPropertyName("sourceSkuId")]
    public required string SourceSkuId { get; init; }

    /// <summary>
    /// The authored expected total weight. It must equal the profile's
    /// weight scale and the pool's actual sum.
    /// </summary>
    [JsonPropertyName("totalWeightExpected")]
    public required int TotalWeightExpected { get; init; }

    /// <summary>The weighted rewards in the pool.</summary>
    [JsonPropertyName("rewards")]
    public required IReadOnlyList<SeedLootReward> Rewards { get; init; }
}

/// <summary>One weighted reward entry in a loot pool.</summary>
public sealed record SeedLootReward
{
    /// <summary>
    /// The rewarded item's definition identifier — it must resolve to a
    /// defined item definition's <c>definitionId</c>.
    /// </summary>
    [JsonPropertyName("definitionId")]
    public required string DefinitionId { get; init; }

    /// <summary>The rarity as authored.</summary>
    [JsonPropertyName("rarity")]
    public required string Rarity { get; init; }

    /// <summary>
    /// The entry's weight in basis points. It must not be negative, and the
    /// pool's weights must sum to the weight scale exactly.
    /// </summary>
    [JsonPropertyName("weight")]
    public required int Weight { get; init; }
}

/// <summary>A shop fixture (a shelf, rack, or decoration).</summary>
public sealed record SeedFixture
{
    /// <summary>The fixture identifier.</summary>
    [JsonPropertyName("fixtureId")]
    public required string FixtureId { get; init; }

    /// <summary>The display name as authored.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>The grid footprint as authored.</summary>
    [JsonPropertyName("grid")]
    public required SeedFixtureGrid Grid { get; init; }

    /// <summary>The starting capacity as authored.</summary>
    [JsonPropertyName("capacitySeed")]
    public required int CapacitySeed { get; init; }

    /// <summary>The compatible product type as authored.</summary>
    [JsonPropertyName("compatibleType")]
    public required string CompatibleType { get; init; }

    /// <summary>The milestone the fixture first appears in, as authored.</summary>
    [JsonPropertyName("firstStage")]
    public required string FirstStage { get; init; }
}

/// <summary>A fixture's grid footprint.</summary>
public sealed record SeedFixtureGrid
{
    /// <summary>The grid width as authored.</summary>
    [JsonPropertyName("w")]
    public required int W { get; init; }

    /// <summary>The grid height as authored.</summary>
    [JsonPropertyName("h")]
    public required int H { get; init; }
}

/// <summary>An NPC or employee behaviour model.</summary>
public sealed record SeedNpcModel
{
    /// <summary>The model identifier.</summary>
    [JsonPropertyName("modelId")]
    public required string ModelId { get; init; }

    /// <summary>The model type as authored (CUSTOMER / EMPLOYEE).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>The archetype name as authored.</summary>
    [JsonPropertyName("archetype")]
    public required string Archetype { get; init; }

    /// <summary>The behaviour description as authored.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>The milestone the model belongs to, as authored.</summary>
    [JsonPropertyName("milestone")]
    public required string Milestone { get; init; }
}

/// <summary>
/// The JSON options the loader reads and writes profiles with.
/// </summary>
public static class SeedProfileJson
{
    /// <summary>
    /// The one serializer options instance for profiles: the source field
    /// names are pinned by the records' <see cref="JsonPropertyName"/>
    /// attributes, so a profile keeps the source document's exact shape.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false, WriteIndented = true };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
