using System.Net.Http.Headers;
using System.Text.Json;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.CdsHooks;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Services.Common;

/// <summary>
/// Calls back into the EHR FHIR server (CDS Hooks fhirServer + fhirAuthorization) when the
/// EHR did not send a prefetch the CRD service needs. Also used to push CDex Tasks to a provider.
/// </summary>
public class EhrFhirClient
{
    private readonly HttpClient _http;
    private readonly ILogger<EhrFhirClient> _log;

    public EhrFhirClient(HttpClient http, ILogger<EhrFhirClient> log) { _http = http; _log = log; }

    public async Task<Bundle?> SearchAsync(CdsHookRequest hook, string relativeQuery, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(hook.FhirServer) || hook.FhirAuthorization == null) return null;
        var url = $"{hook.FhirServer.TrimEnd('/')}/{relativeQuery}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", hook.FhirAuthorization.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypes.FhirJson));
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("EHR query {Url} returned {Status}", url, response.StatusCode);
                return null;
            }
            var json = await response.Content.ReadAsStringAsync(ct);
            return FhirJson.Parse(json) as Bundle;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "EHR query {Url} failed", url);
            return null;
        }
    }

    /// <param name="headers">Raw "Name: value" headers (e.g. Subscription.channel.header).</param>
    public async Task<bool> PostAsync(string endpoint, Resource resource, CancellationToken ct, IEnumerable<string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(FhirJson.Serialize(resource, false), System.Text.Encoding.UTF8, MediaTypes.FhirJson)
            };
            foreach (var header in headers ?? Enumerable.Empty<string>())
            {
                var idx = header.IndexOf(':');
                if (idx > 0) request.Headers.TryAddWithoutValidation(header[..idx].Trim(), header[(idx + 1)..].Trim());
            }
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _log.LogWarning("POST {Endpoint} returned {Status}", endpoint, response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "POST {Endpoint} failed", endpoint);
            return false;
        }
    }
}
