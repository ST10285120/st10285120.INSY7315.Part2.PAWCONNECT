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

[Route("api/medical-records")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Staff)]
public class MedicalRecordsApiController : ApiControllerBase
{
    private readonly MedicalRecordService _service;
    private readonly IMedicalRecordRepository _records;

    public MedicalRecordsApiController(MedicalRecordService service, IMedicalRecordRepository records)
    {
        _service = service;
        _records = records;
    }

    /// <summary>POST /api/medical-records: only volunteers and administrators can write medical records.</summary>
    [HttpPost]
    public async Task<ActionResult<MedicalRecordDto>> Create(CreateMedicalRecordRequest request, CancellationToken ct)
    {
        var result = await _service.AddAsync(request.AnimalId, User.GetUserId(), request.ToInput(), ct);
        if (!result.Succeeded) return ProblemFrom(result);
        var saved = (await _records.GetAsync(result.Value!.Id, ct))!;
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, MedicalRecordDto.From(saved));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalRecordDto>> Get(Guid id, CancellationToken ct)
    {
        var record = await _records.GetAsync(id, ct);
        return record == null ? NotFound() : MedicalRecordDto.From(record);
    }
}
