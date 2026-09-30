using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.CdsHooks;
using DaVinci.PriorAuth.Services.Crd;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaVinci.PriorAuth.Controllers;

/// <summary>
/// CRD - CDS Hooks endpoints.
///   GET  /cds-services          discovery
///   POST /cds-services/{id}     hook call, e.g. /cds-services/crd-order-sign
/// </summary>
[ApiController]
[Route("cds-services")]
public class CdsServicesController : ControllerBase
{
    private readonly ICrdService _crd;
    public CdsServicesController(ICrdService crd) { _crd = crd; }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Discovery() => new JsonResult(_crd.Discovery(), CdsJson.Options);

    [HttpPost("{serviceId}")]
    [Authorize(Policy = Policies.CrdHooks)]
    public async Task<IActionResult> Invoke(string serviceId, [FromBody] CdsHookRequest request, CancellationToken ct)
    {
        var hook = CrdHooks.All.FirstOrDefault(h => CrdHooks.ServiceId(h) == serviceId);
        if (hook == null) return NotFound(new { error = $"Unknown CDS service '{serviceId}'." });

        if (!string.Equals(request.Hook, hook, StringComparison.Ordinal))
            return BadRequest(new { error = $"Service '{serviceId}' handles hook '{hook}', but the request says '{request.Hook}'." });

        var response = await _crd.HandleHookAsync(hook, request, ct);
        return new JsonResult(response, CdsJson.Options);
    }
}
