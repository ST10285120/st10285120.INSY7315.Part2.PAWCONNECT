using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class ApplicationDetailVm
{
    public required AdoptionApplication Application { get; init; }
    public bool FeeRecorded { get; init; }
    public bool IsAdmin { get; init; }
    public bool CanStartReview => Application.Status == ApplicationStatus.Submitted;
    public bool CanDecide => Application.Status == ApplicationStatus.UnderReview;
    public bool CanReject => ApplicationStateMachine.CanTransition(Application.Status, ApplicationStatus.Rejected);
    public bool IsApproved => Application.Status == ApplicationStatus.Approved;
    public bool CanComplete => IsApproved && Application.HomeVisitCompletedAt != null && FeeRecorded;
}
