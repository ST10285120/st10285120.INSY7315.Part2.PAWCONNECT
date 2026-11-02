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

[Route("api/shifts")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Staff)]
public class ShiftsApiController : ApiControllerBase
{
    private readonly ShiftService _service;
    private readonly IShiftRepository _shifts;
    private readonly IClock _clock;
    private readonly BranchAccess _access;

    public ShiftsApiController(ShiftService service, IShiftRepository shifts, IClock clock, BranchAccess access)
    {
        _service = service;
        _shifts = shifts;
        _clock = clock;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> Upcoming([FromQuery] bool openOnly = false, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var shifts = await _shifts.GetUpcomingAsync(_clock.UtcNow, await _access.BranchFilterAsync(User, ct), ct);
        return shifts.Where(s => !openOnly || !s.IsFull).Select(s => ShiftDto.From(s, userId)).ToList();
    }

    [HttpPost("{id:guid}/signup")]
    public async Task<IActionResult> SignUp(Guid id, CancellationToken ct)
    {
        var result = await _service.SignUpAsync(id, User.GetUserId(), ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpDelete("{id:guid}/signup")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelAsync(id, User.GetUserId(), ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }
}
