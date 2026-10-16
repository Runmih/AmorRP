namespace AmorRP.Server.Infrastructure.Security;

public sealed class ApiFault(int status, string code, string title) : Exception(title)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static void Require(bool valid, int status, string code, string title)
    { if (!valid) throw new ApiFault(status, code, title); }
    public static void Text(string? value, int max, bool required = true)
    { Require(value != null && (!required || !string.IsNullOrWhiteSpace(value)) && value.Length <= max
        && !value.Any(c => char.IsControl(c) && c != '\n'), 422, "invalid_request", "Invalid text field."); }
}
