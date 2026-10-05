namespace PawConnect.Core.Entities;

/// <summary>
/// An uploaded animal photo. Stored in the database (bytea) so every App Service slot and
/// instance sees the same files. Size is capped at upload time.
/// </summary>
public class AnimalPhoto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int SizeBytes { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime UploadedAt { get; set; }
}
