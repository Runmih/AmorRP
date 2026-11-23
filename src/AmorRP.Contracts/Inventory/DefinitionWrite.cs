namespace AmorRP.Contracts.Inventory;

public sealed record DefinitionWrite(string TypeId, string Name, Guid CategoryId, string Description, bool Enabled, PotionData Potion, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? GroupId = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MoodleEffect? Effect = null);
