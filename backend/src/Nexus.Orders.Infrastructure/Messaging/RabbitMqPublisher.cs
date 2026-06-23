using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Nexus.Orders.Application.Interfaces;
using Nexus.Orders.Application.Models;

namespace Nexus.Orders.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly ConnectionFactory _factory;

    public RabbitMqPublisher(ConnectionFactory factory)
    {
        _factory = factory;
    }

    public void PublishOrder(SalesOrder order)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange: "sap.orders", type: ExchangeType.Direct, durable: true);
        channel.QueueDeclare(queue: "sap-order-queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueBind(queue: "sap-order-queue", exchange: "sap.orders", routingKey: "it");

        var message = JsonSerializer.Serialize(order);
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "sap.orders", routingKey: "it", basicProperties: null, body: body);
        Console.WriteLine($"[Nexus Infrastructure] Dispatched purchase order {order.PoNumber} to RabbitMQ Server.");
    }
}
