namespace CurioDistrict.Config;

/// <summary>Collects deterministic diagnostics for version, basis-point arithmetic, costs and references.</summary>
public static class SeedValidator
{
    // Basis points define a denominator; this is a format invariant, not an economy tuning value.
    private const int BASIS_POINT_TOTAL = 10_000;

    /// <summary>Returns every defect it can enumerate, without changing the supplied profile.</summary>
    public static IReadOnlyList<SeedValidationError> Validate(SeedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = new List<SeedValidationError>();
        if (!SeedConfigVersion.TryParse(profile.ConfigVersion, out _))
            errors.Add(new("configVersion", $"Unparseable version '{profile.ConfigVersion ?? "null"}'; expected MAJOR.MINOR."));
        ValidateShape(profile, errors);
        if (profile.Metadata is not null && profile.Metadata.WeightScale != BASIS_POINT_TOTAL)
            errors.Add(new("metadata.weightScale", $"Expected {BASIS_POINT_TOTAL}, got {profile.Metadata.WeightScale}."));
        if (profile.Economy is not null && profile.Economy.PercentLootSum != BASIS_POINT_TOTAL)
            errors.Add(new("economy.PERCENT_LOOT_SUM", $"Expected {BASIS_POINT_TOTAL}, got {profile.Economy.PercentLootSum}."));

        var skus = Index(profile.Products, p => p.SkuId, "products", "skuId", errors);
        var definitions = Index(profile.ItemDefinitions, d => d.DefinitionId, "itemDefinitions", "definitionId", errors);
        ValidateCosts(profile.Products, errors);
        ValidatePools(profile.LootPools, skus, definitions, errors);
        return errors.AsReadOnly();
    }

    private static void ValidateShape(SeedProfile profile, ICollection<SeedValidationError> errors)
    {
        var members = new (string Path, object? Value)[]
        {
            ("metadata", profile.Metadata), ("economy", profile.Economy),
            ("products", profile.Products), ("itemDefinitions", profile.ItemDefinitions),
            ("lootPools", profile.LootPools), ("fixtures", profile.Fixtures), ("npcModels", profile.NpcModels)
        };
        foreach (var (path, value) in members)
            if (value is null) errors.Add(new(path, "Required object/collection must not be null."));
    }

    private static HashSet<string> Index<T>(IReadOnlyList<T>? values, Func<T, string> id,
        string path, string field, ICollection<SeedValidationError> errors) where T : class
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (values is null) return result;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is null) { errors.Add(new($"{path}[{i}]", "Entry must not be null.")); continue; }
            var key = id(values[i]);
            if (string.IsNullOrWhiteSpace(key))
                errors.Add(new($"{path}[{i}].{field}", "Identifier must not be empty."));
            else if (!result.Add(key)) errors.Add(new($"{path}[{i}].{field}", $"Duplicate identifier '{key}'."));
        }
        return result;
    }

    private static void ValidateCosts(IReadOnlyList<SeedProduct>? products, ICollection<SeedValidationError> errors)
    {
        if (products is null) return;
        foreach (var product in products)
        {
            if (product is null) continue;
            var costs = new (string Field, int Value)[]
            {
                ("supplierCost", product.SupplierCost), ("recommendedPrice", product.RecommendedPrice),
                ("minPrice", product.MinPrice), ("maxPrice", product.MaxPrice)
            };
            foreach (var (field, value) in costs)
                if (value < 0) errors.Add(new($"products['{product.SkuId}'].{field}", $"Cost/price cannot be negative (got {value})."));
        }
    }

    private static void ValidatePools(IReadOnlyList<SeedLootPool>? pools, ISet<string> skus,
        ISet<string> definitions, ICollection<SeedValidationError> errors)
    {
        if (pools is null) return;
        for (var i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            if (pool is null) { errors.Add(new($"lootPools[{i}]", "Pool must not be null.")); continue; }
            var path = $"lootPools['{pool.PoolId}']";
            if (string.IsNullOrWhiteSpace(pool.SourceSkuId) || !skus.Contains(pool.SourceSkuId))
                errors.Add(new($"{path}.sourceSkuId", $"Unresolved SKU '{pool.SourceSkuId ?? "null"}'."));
            if (pool.TotalWeightExpected != BASIS_POINT_TOTAL)
                errors.Add(new($"{path}.totalWeightExpected", $"Expected {BASIS_POINT_TOTAL}, got {pool.TotalWeightExpected}."));
            ValidateRewards(pool, path, definitions, errors);
        }
    }

    private static void ValidateRewards(SeedLootPool pool, string path, ISet<string> definitions,
        ICollection<SeedValidationError> errors)
    {
        if (pool.Rewards is null) { errors.Add(new($"{path}.rewards", "Rewards must not be null.")); return; }
        long total = 0;
        var entryIds = new List<string>();
        for (var i = 0; i < pool.Rewards.Count; i++)
        {
            var reward = pool.Rewards[i];
            if (reward is null) { errors.Add(new($"{path}.rewards[{i}]", "Reward must not be null.")); continue; }
            entryIds.Add(reward.DefinitionId ?? $"index:{i}");
            var rewardPath = $"{path}.rewards['{reward.DefinitionId}']";
            total += reward.Weight;
            if (reward.Weight < 0) errors.Add(new($"{rewardPath}.weight", $"Weight cannot be negative (got {reward.Weight})."));
            if (string.IsNullOrWhiteSpace(reward.DefinitionId) || !definitions.Contains(reward.DefinitionId))
                errors.Add(new($"{rewardPath}.definitionId", $"Unresolved item definition '{reward.DefinitionId ?? "null"}'."));
        }
        if (total != BASIS_POINT_TOTAL)
            errors.Add(new($"{path}.rewards[{string.Join(",", entryIds)}].weight", $"Weights must total exactly {BASIS_POINT_TOTAL}, got {total}."));
    }
}
