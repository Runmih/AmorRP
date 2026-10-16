using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record GroupDeletion([property: JsonRequired] string ConfirmName);
