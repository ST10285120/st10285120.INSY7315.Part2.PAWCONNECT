using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public record AnimalDetailDto(AnimalDto Animal, List<MedicalRecordDto>? MedicalHistory);
