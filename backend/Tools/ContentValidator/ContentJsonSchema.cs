using System.Text.Json;

namespace CurioDistrict.Tools.ContentValidator;

/// <summary>Explicit field allowlists prevent unsupported reference-bearing data being silently dropped.</summary>
internal static class ContentJsonSchema
{
    private const JsonValueKind Text = JsonValueKind.String;
    private const JsonValueKind Number = JsonValueKind.Number;
    private const JsonValueKind Object = JsonValueKind.Object;
    private const JsonValueKind Array = JsonValueKind.Array;
    private const JsonValueKind Boolean = JsonValueKind.True;

    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Root = new Dictionary<string, JsonValueKind>
    {
        ["configVersion"] = Text, ["metadata"] = Object, ["economy"] = Object,
        ["products"] = Array, ["itemDefinitions"] = Array, ["lootPools"] = Array,
        ["fixtures"] = Array, ["npcModels"] = Array
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Metadata = new Dictionary<string, JsonValueKind>
    {
        ["product"] = Text, ["profileId"] = Text, ["version"] = Text, ["status"] = Text,
        ["currency"] = Text, ["weightScale"] = Number, ["source"] = Text, ["notes"] = Array
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Economy = new Dictionary<string, JsonValueKind>
    {
        ["currency"] = Text, ["starting_wallet"] = Number, ["district_tick_seconds"] = Number,
        ["district_shops_cap"] = Number, ["district_tick_demand"] = Number, ["staff_wage_cashier"] = Number,
        ["staff_wage_restocker"] = Number, ["staff_wage_purchasing"] = Number,
        ["wallet_min_reserve"] = Number, ["supplier_spend_max_per_tick"] = Number,
        ["npc_item_resale"] = Boolean, ["PERCENT_LOOT_SUM"] = Number
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Product = new Dictionary<string, JsonValueKind>
    {
        ["skuId"] = Text, ["kind"] = Text, ["seriesId"] = Text, ["name"] = Text,
        ["supplierCost"] = Number, ["recommendedPrice"] = Number, ["minPrice"] = Number,
        ["maxPrice"] = Number, ["stackable"] = Boolean, ["poolId"] = Text, ["milestone"] = Text
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Item = new Dictionary<string, JsonValueKind>
    {
        ["definitionId"] = Text, ["kind"] = Text, ["seriesId"] = Text, ["displayName"] = Text,
        ["rarity"] = Text, ["prefabId"] = Text, ["localizationKey"] = Text,
        ["presentation"] = Text, ["references"] = Array
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Pool = new Dictionary<string, JsonValueKind>
    {
        ["poolId"] = Text, ["version"] = Text, ["sourceSkuId"] = Text,
        ["totalWeightExpected"] = Number, ["rewards"] = Array
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Reward = new Dictionary<string, JsonValueKind>
    {
        ["definitionId"] = Text, ["rarity"] = Text, ["weight"] = Number
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Fixture = new Dictionary<string, JsonValueKind>
    {
        ["fixtureId"] = Text, ["displayName"] = Text, ["grid"] = Object,
        ["capacitySeed"] = Number, ["compatibleType"] = Text, ["firstStage"] = Text
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Grid = new Dictionary<string, JsonValueKind>
    {
        ["w"] = Number, ["h"] = Number
    };
    internal static readonly IReadOnlyDictionary<string, JsonValueKind> Npc = new Dictionary<string, JsonValueKind>
    {
        ["modelId"] = Text, ["type"] = Text, ["archetype"] = Text,
        ["description"] = Text, ["milestone"] = Text
    };
}

internal sealed class ContentJsonReader(string file, ContentValidationState state)
{
    internal bool CheckObject(JsonElement value, string path, IReadOnlyDictionary<string, JsonValueKind> schema)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Error(path, "Expected an object.");
            return false;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            var memberPath = path + "." + property.Name;
            if (!seen.Add(property.Name)) Error(memberPath, "Duplicate JSON member is not supported.");
            if (!schema.TryGetValue(property.Name, out var kind))
                Error(memberPath, $"Unknown member '{property.Name}'; unsupported data must not be silently ignored.");
            else if (property.Value.ValueKind != kind &&
                     !(kind == JsonValueKind.True && property.Value.ValueKind == JsonValueKind.False))
                Error(memberPath, $"Expected {kind}, got {property.Value.ValueKind}.");
            else if (kind == JsonValueKind.Number && !property.Value.TryGetInt64(out _))
                Error(memberPath, "Expected an integer representable as Int64.");
        }
        return true;
    }

    internal static IEnumerable<JsonElement> Members(JsonElement value, string field) =>
        value.EnumerateObject().Where(property => property.NameEquals(field)).Select(property => property.Value);

    internal static IEnumerable<string?> TextValues(JsonElement value, string field)
    {
        var found = false;
        foreach (var member in Members(value, field))
        {
            found = true;
            yield return member.ValueKind == JsonValueKind.String ? member.GetString() : null;
        }
        if (!found) yield return null;
    }

    internal string? Identifier(JsonElement value, string field, string type, string path)
    {
        var ids = new List<string>();
        foreach (var id in TextValues(value, field))
        {
            if (string.IsNullOrWhiteSpace(id))
                state.Add("Identifier", file, path + "." + field,
                    $"Missing or blank {type} identifier '{field}'.");
            else
            {
                state.Declare(type, id, file, path + "." + field);
                ids.Add(id);
            }
        }
        return ids.Count == 0 ? null : string.Join(" / ", ids);
    }

    internal IEnumerable<(JsonElement Value, string Path)> Elements(JsonElement value, string field, string path)
    {
        foreach (var array in Members(value, field))
            foreach (var element in ArrayElements(array, path + "." + field))
                yield return element;
    }

    internal static IEnumerable<(JsonElement Value, string Path)> ArrayElements(JsonElement array, string path)
    {
        if (array.ValueKind != JsonValueKind.Array) yield break;
        var index = 0;
        foreach (var value in array.EnumerateArray()) yield return (value, $"{path}[{index++}]");
    }

    internal void StringArray(JsonElement array, string path, Action<string?, string>? action = null)
    {
        if (array.ValueKind != JsonValueKind.Array) return;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var memberPath = $"{path}[{index++}]";
            if (value.ValueKind != JsonValueKind.String) Error(memberPath, "Expected a string.");
            else action?.Invoke(value.GetString(), memberPath);
        }
    }

    internal void Error(string path, string message) => state.Add("Schema", file, path, message);
}
