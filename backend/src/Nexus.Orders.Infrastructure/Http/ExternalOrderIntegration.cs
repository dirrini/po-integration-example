using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Orders.Application.Interfaces;
using Nexus.Orders.Application.Models;

namespace Nexus.Orders.Infrastructure.Http;

public class ExternalOrderIntegration : IExternalOrderIntegration
{
    private readonly HttpClient _httpClient;

    public ExternalOrderIntegration(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
                    projectExternalCode = "SAP-PROJ-001",
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

        var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GraphQL product integration failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        ThrowIfGraphQlErrors(responseBody);

        Console.WriteLine($"[External Product API] Integrated purchase order {order.PoNumber}: {responseBody}");
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

public sealed class GraphQlIntegrationException : Exception
{
    public GraphQlIntegrationException(string message, bool isRetryable)
        : base(message)
    {
        IsRetryable = isRetryable;
    }

    public bool IsRetryable { get; }
}

