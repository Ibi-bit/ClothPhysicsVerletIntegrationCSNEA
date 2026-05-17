using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project LegacyJsonConverter -- <path-to-JSONStructures-folder>");
    return;
}

string rootFolder = Path.GetFullPath(args[0]);
if (!Directory.Exists(rootFolder))
{
    Console.Error.WriteLine($"Folder not found: {rootFolder}");
    return;
}

var jsonFiles = Directory.GetFiles(rootFolder, "*.json", SearchOption.AllDirectories);
int updatedFiles = 0;

foreach (string filePath in jsonFiles)
{
    string originalText = await File.ReadAllTextAsync(filePath);
    string updatedText = ConvertLegacyMeshJson(originalText);

    if (!string.Equals(originalText, updatedText, StringComparison.Ordinal))
    {
        await File.WriteAllTextAsync(filePath, updatedText);
        updatedFiles++;
        Console.WriteLine($"Updated {filePath}");
    }
}

Console.WriteLine($"Done. Updated {updatedFiles} file(s).");

static string ConvertLegacyMeshJson(string input)
{
    JToken root = JToken.Parse(input);
    ConvertLegacyTokens(root);
    return root.ToString(Formatting.Indented);
}

static void ConvertLegacyTokens(JToken token)
{
    if (token is JObject obj)
    {
        string? typeName = obj["$type"]?.Value<string>();
        if (
            typeName != null
            && typeName.StartsWith("Microsoft.Xna.Framework.Rectangle", StringComparison.Ordinal)
        )
        {
            ConvertLegacyRectangleObject(obj);
            return;
        }

        foreach (JProperty property in obj.Properties().ToList())
        {
            if (TryConvertLegacyVectorToken(property.Value, out JToken convertedVector))
            {
                property.Value = convertedVector;
            }
            else
            {
                ConvertLegacyTokens(property.Value);
            }
        }
    }
    else if (token is JArray array)
    {
        for (int i = 0; i < array.Count; i++)
        {
            JToken child = array[i];
            if (TryConvertLegacyVectorToken(child, out JToken convertedVector))
            {
                array[i] = convertedVector;
            }
            else
            {
                ConvertLegacyTokens(child);
            }
        }
    }
}

static void ConvertLegacyRectangleObject(JObject rectangleObject)
{
    float x = rectangleObject["X"]?.Value<float>() ?? 0f;
    float y = rectangleObject["Y"]?.Value<float>() ?? 0f;
    float width = rectangleObject["Width"]?.Value<float>() ?? 0f;
    float height = rectangleObject["Height"]?.Value<float>() ?? 0f;

    rectangleObject.Remove("$type");
    rectangleObject.Remove("X");
    rectangleObject.Remove("Y");
    rectangleObject.Remove("Width");
    rectangleObject.Remove("Height");

    rectangleObject["X"] = x;
    rectangleObject["Y"] = y;
    rectangleObject["Width"] = width;
    rectangleObject["Height"] = height;
}

static bool TryConvertLegacyVectorToken(JToken value, out JToken convertedToken)
{
    convertedToken = value;

    if (value.Type == JTokenType.String && TryParseLegacyVector2(value.Value<string>()!, out float x, out float y))
    {
        convertedToken = new JObject
        {
            ["$type"] = "System.Numerics.Vector2, System.Private.CoreLib",
            ["X"] = x,
            ["Y"] = y,
        };
        return true;
    }

    if (value is JObject existingObject)
    {
        bool looksLikeVector =
            (existingObject["X"] != null || existingObject["x"] != null)
            && (existingObject["Y"] != null || existingObject["y"] != null)
            && existingObject.Properties().Count() <= 4;

        if (looksLikeVector)
        {
            float vectorX = existingObject["X"]?.Value<float>() ?? existingObject["x"]?.Value<float>() ?? 0f;
            float vectorY = existingObject["Y"]?.Value<float>() ?? existingObject["y"]?.Value<float>() ?? 0f;
            convertedToken = new JObject
            {
                ["$type"] = "System.Numerics.Vector2, System.Private.CoreLib",
                ["X"] = vectorX,
                ["Y"] = vectorY,
            };
            return true;
        }
    }

    return false;
}

static bool TryParseLegacyVector2(string value, out float x, out float y)
{
    x = 0f;
    y = 0f;

    string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 2)
    {
        return false;
    }

    return float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
}
