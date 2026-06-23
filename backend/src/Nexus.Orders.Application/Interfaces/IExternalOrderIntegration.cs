using Nexus.Orders.Application.Models;

namespace Nexus.Orders.Application.Interfaces;

public interface IExternalOrderIntegration
{
    Task SendOrderAsync(SalesOrder order, CancellationToken cancellationToken = default);
}