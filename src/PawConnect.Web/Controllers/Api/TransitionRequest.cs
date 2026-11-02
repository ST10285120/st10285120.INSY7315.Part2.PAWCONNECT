using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public class TransitionRequest
{
    /// <summary>Target status: UnderReview, Approved, Rejected or Adopted.</summary>
    [Required] public ApplicationStatus Status { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}
