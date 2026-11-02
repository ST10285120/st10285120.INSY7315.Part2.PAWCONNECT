using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;

namespace PawConnect.Web.Controllers.Api;

[Route("api/donations")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes)]
public class DonationsApiController : ApiControllerBase
{
    private readonly DonationService _service;
    private readonly IDonationRepository _donations;

    public DonationsApiController(DonationService service, IDonationRepository donations)
    {
        _service = service;
        _donations = donations;
    }

    [HttpPost]
    public async Task<ActionResult<DonationDto>> Pledge(DonationRequest request, CancellationToken ct)
    {
        var result = await _service.PledgeAsync(User.GetUserId(), new DonationPledge(request.Amount, request.Frequency, request.SponsorAnimalId, request.Note), ct);
        if (!result.Succeeded) return ProblemFrom(result);
        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, DonationDto.From(result.Value));
    }

    /// <summary>One donation: only its donor or staff can read it.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DonationDto>> Get(Guid id, CancellationToken ct)
    {
        var donation = await _donations.GetAsync(id, ct);
        if (donation == null || (!User.IsStaff() && donation.DonorId != User.GetUserId())) return NotFound();
        return DonationDto.From(donation);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<DonationDto>>> Mine(CancellationToken ct) =>
        (await _donations.GetForDonorAsync(User.GetUserId(), ct)).Select(DonationDto.From).ToList();
}
