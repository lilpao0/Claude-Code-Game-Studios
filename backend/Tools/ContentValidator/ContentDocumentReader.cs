using System.Text.Json;

namespace CurioDistrict.Tools.ContentValidator;

internal sealed class ContentDocumentReader(string file, ContentValidationState state)
{
    private readonly ContentJsonReader _json = new(file, state);

    internal void Read(JsonElement root)
    {
        if (!_json.CheckObject(root, "$", ContentJsonSchema.Root)) return;
        ReadContext(root);
        foreach (var (value, path) in _json.Elements(root, "products", "$")) ReadProduct(value, path);
        foreach (var (value, path) in _json.Elements(root, "itemDefinitions", "$")) ReadItem(value, path);
        foreach (var (value, path) in _json.Elements(root, "lootPools", "$"))
        {
            state.PoolCount++;
            ReadPool(value, path);
        }
        foreach (var (value, path) in _json.Elements(root, "fixtures", "$")) ReadFixture(value, path);
        foreach (var (value, path) in _json.Elements(root, "npcModels", "$"))
            if (_json.CheckObject(value, path, ContentJsonSchema.Npc))
                _json.Identifier(value, "modelId", "NPC model", path);
    }

    private void ReadContext(JsonElement root)
    {
        foreach (var metadata in ContentJsonReader.Members(root, "metadata"))
        {
            if (!_json.CheckObject(metadata, "$.metadata", ContentJsonSchema.Metadata)) continue;
            foreach (var notes in ContentJsonReader.Members(metadata, "notes"))
                _json.StringArray(notes, "$.metadata.notes");
        }
        foreach (var economy in ContentJsonReader.Members(root, "economy"))
            _json.CheckObject(economy, "$.economy", ContentJsonSchema.Economy);
    }

    private void ReadProduct(JsonElement product, string path)
    {
        if (!_json.CheckObject(product, path, ContentJsonSchema.Product)) return;
        var id = _json.Identifier(product, "skuId", "SKU", path);
        ReferMembers(product, "poolId", "pool", path, $"Product '{id ?? "<missing>"}'");
    }

    private void ReadItem(JsonElement item, string path)
    {
        if (!_json.CheckObject(item, path, ContentJsonSchema.Item)) return;
        var id = _json.Identifier(item, "definitionId", "item", path);
        foreach (var references in ContentJsonReader.Members(item, "references"))
            _json.StringArray(references, path + ".references", (target, referencePath) =>
                state.Refer("item", target, file, referencePath, $"Item '{id ?? "<missing>"}'"));
    }

    private void ReadFixture(JsonElement fixture, string path)
    {
        if (!_json.CheckObject(fixture, path, ContentJsonSchema.Fixture)) return;
        _json.Identifier(fixture, "fixtureId", "fixture", path);
        foreach (var grid in ContentJsonReader.Members(fixture, "grid"))
            _json.CheckObject(grid, path + ".grid", ContentJsonSchema.Grid);
    }

    private void ReadPool(JsonElement pool, string path)
    {
        if (!_json.CheckObject(pool, path, ContentJsonSchema.Pool)) return;
        var id = _json.Identifier(pool, "poolId", "pool", path);
        var entryIds = new List<string>();
        var arrays = ContentJsonReader.Members(pool, "rewards").ToArray();
        if (arrays.Length == 0 || arrays.Any(array => array.ValueKind != JsonValueKind.Array))
            _json.Error(path + ".rewards", $"Loot pool '{id ?? "<missing>"}' requires a rewards array.");
        foreach (var rewards in arrays.Where(array => array.ValueKind == JsonValueKind.Array))
            entryIds.AddRange(ReadRewards(rewards, path, id));
        ReferMembers(pool, "sourceSkuId", "SKU", path,
            $"Loot pool '{id ?? "<missing>"}', reward entries [{string.Join(", ", entryIds)}]");
        foreach (var expected in ContentJsonReader.Members(pool, "totalWeightExpected"))
            CheckExpectedTotal(expected, path, id);
    }

    private void CheckExpectedTotal(JsonElement expected, string path, string? poolId)
    {
        if (expected.ValueKind == JsonValueKind.Number && expected.TryGetInt64(out var total) && total != 10_000)
            state.Add("PoolWeight", file, path + ".totalWeightExpected",
                FormattableString.Invariant($"Loot pool '{poolId ?? "<missing>"}' expected total must be 10000, got {total}."));
    }

    private void ReferMembers(JsonElement value, string field, string type, string path, string description)
    {
        foreach (var target in ContentJsonReader.TextValues(value, field))
            state.Refer(type, target, file, path + "." + field, description);
    }

    private IReadOnlyList<string> ReadRewards(JsonElement rewards, string path, string? poolId)
    {
        var total = new PoolTotal();
        foreach (var (reward, rewardPath) in ContentJsonReader.ArrayElements(rewards, path + ".rewards"))
            ReadReward(reward, rewardPath, poolId, total);
        if (!total.IsComplete || total.Value != 10_000)
            state.Add("PoolWeight", file, path + ".rewards",
                total.IsComplete
                    ? FormattableString.Invariant($"Loot pool '{poolId ?? "<missing>"}' entries [{string.Join(", ", total.EntryIds)}] must total exactly 10000, got {total.Value}.")
                    : $"Loot pool '{poolId ?? "<missing>"}' entries [{string.Join(", ", total.EntryIds)}] cannot establish an exact 10000 total because a weight is invalid or ambiguous.");
        return total.EntryIds;
    }

    private void ReadReward(JsonElement reward, string path, string? poolId, PoolTotal total)
    {
        if (!_json.CheckObject(reward, path, ContentJsonSchema.Reward))
        {
            total.IsComplete = false;
            return;
        }
        var itemIds = ContentJsonReader.TextValues(reward, "definitionId").ToArray();
        total.EntryIds.AddRange(itemIds.Select(id => id ?? "<missing>"));
        foreach (var itemId in itemIds)
            state.Refer("item", itemId, file, path + ".definitionId",
                $"Loot pool '{poolId ?? "<missing>"}', reward entry '{itemId ?? "<missing>"}'");
        var weights = ContentJsonReader.Members(reward, "weight").ToArray();
        if (weights.Length != 1) total.IsComplete = false;
        if (weights.Length == 0)
            _json.Error(path + ".weight", $"Loot pool '{poolId ?? "<missing>"}' requires an Int64 integer weight.");
        foreach (var weight in weights)
            ReadWeight(weight, path, poolId, string.Join(" / ", itemIds), total);
    }

    private void ReadWeight(JsonElement weight, string path, string? poolId, string itemId, PoolTotal total)
    {
        if (weight.ValueKind != JsonValueKind.Number || !weight.TryGetInt64(out var amount))
        {
            _json.Error(path + ".weight", $"Loot pool '{poolId ?? "<missing>"}', reward '{itemId}' requires an Int64 integer weight.");
            total.IsComplete = false;
            return;
        }
        if (amount < 0)
            state.Add("PoolWeight", file, path + ".weight",
                FormattableString.Invariant($"Loot pool '{poolId ?? "<missing>"}', reward '{itemId}' has negative weight {amount}."));
        try { total.Value = checked(total.Value + amount); }
        catch (OverflowException)
        {
            state.Add("PoolWeight", file, path + ".weight",
                $"Loot pool '{poolId ?? "<missing>"}' weight total exceeds the Int64 range at reward '{itemId}'.");
            total.IsComplete = false;
        }
    }

    private sealed class PoolTotal
    {
        internal long Value { get; set; }
        internal bool IsComplete { get; set; } = true;
        internal List<string> EntryIds { get; } = [];
    }
}
