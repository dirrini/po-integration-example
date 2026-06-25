using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Nexus.Orders.Application.Interfaces;
using Nexus.Orders.Application.Models;

namespace Nexus.Orders.Infrastructure.Http;

public class ExternalOrderIntegration : IExternalOrderIntegration
{
    private readonly HttpClient _httpClient;
    private readonly ExternalProductsSettings _settings;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public ExternalOrderIntegration(
        HttpClient httpClient,
        ExternalProductsSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task SendOrderAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            query = """
                mutation UpsertExternalProduct($input: UpsertExternalProductInput!) {
                  upsertExternalProduct(input: $input) {
                    id
                    externalCode
                    status
                    vendor
                    materialCode
                    quantity
                    materialDescription
                    deliveryDate
                    projectId
                  }
                }
                """,
            variables = new
            {
                input = new
                {
                    projectExternalCode = order.ProjectExternalCode,
                    externalCode = order.PoNumber,
                    status = "OPEN",
                    vendor = order.Vendor,
                    materialCode = order.MaterialCode,
                    quantity = order.Quantity,
                    materialDescription = order.MaterialDescription,
                    deliveryDate = order.DeliveryDate
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetAccessTokenAsync(cancellationToken));

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GraphQL product integration failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        ThrowIfGraphQlErrors(responseBody);

        Console.WriteLine($"[External Product API] Integrated purchase order {order.PoNumber}: {responseBody}");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _accessToken;
            }

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _settings.TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret
                })
            };

            var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
            var tokenResponseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                throw new GraphQlIntegrationException(
                    $"Integration token request failed with {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}: {tokenResponseBody}",
                    isRetryable: IsRetryableTokenStatusCode(tokenResponse.StatusCode));
            }

            var tokenPayload = JsonSerializer.Deserialize<IntegrationTokenResponse>(
                tokenResponseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrWhiteSpace(tokenPayload?.AccessToken) ||
                !DateTimeOffset.TryParse(tokenPayload.ExpiresAt, out var expiresAt))
            {
                throw new InvalidOperationException(
                    $"Integration token response is invalid: {tokenResponseBody}");
            }

            _accessToken = tokenPayload.AccessToken;
            _accessTokenExpiresAt = expiresAt;

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static bool IsRetryableTokenStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500 ||
            statusCode is HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests;
    }

    private static void ThrowIfGraphQlErrors(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0)
        {
            return;
        }

        var isPermanentFailure = errors.EnumerateArray()
            .Select(GetGraphQlErrorCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Any(IsPermanentGraphQlError);

        throw new GraphQlIntegrationException(
            $"GraphQL product integration returned errors: {responseBody}",
            isRetryable: !isPermanentFailure);
    }

    private static string? GetGraphQlErrorCode(JsonElement error)
    {
        if (error.TryGetProperty("extensions", out var extensions) &&
            extensions.TryGetProperty("code", out var code) &&
            code.ValueKind == JsonValueKind.String)
        {
            return code.GetString();
        }

        return null;
    }

    private static bool IsPermanentGraphQlError(string code)
    {
        return code is "UNAUTHENTICATED" or "FORBIDDEN" or "BAD_USER_INPUT" or "NOT_FOUND";
    }
}

public sealed class ExternalProductsSettings
{
    public required string GraphqlUrl { get; init; }
    public required string TokenUrl { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}

internal sealed class IntegrationTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
}

public sealed class GraphQlIntegrationException : Exception
{
    public GraphQlIntegrationException(string message, bool isRetryable)
        : base(message)
    {
        IsRetryable = isRetryable;
    }

    public bool IsRetryable { get; }
}

