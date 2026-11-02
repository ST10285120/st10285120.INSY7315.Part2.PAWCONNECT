using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public record AnimalPhotoInfo(Guid Id, Guid AnimalId, bool IsPrimary, DateTime UploadedAt);
