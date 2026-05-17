# LegacyJsonConverter

This utility rewrites old mesh JSON files that store `Position` as strings like `"138, 442"` into the `System.Numerics.Vector2` object format used by the Raylib port.

## Run

```bash
dotnet run --project LegacyJsonConverter -- PhysicsCSAlevlProject/JSONStructures
```

It updates files in place and preserves the rest of the JSON structure.
