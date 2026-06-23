using Nexus.Orders.Application.Models;

namespace Nexus.Orders.Application.Interfaces;

public interface IMessagePublisher
{
    void PublishOrder(SalesOrder order);
}