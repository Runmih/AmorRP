namespace AmorRP.Plugin.Services.Api;
public sealed record CommandRequest(string Method, string Path, string? Json, string? ETag, string? UploadBase64 = null);
