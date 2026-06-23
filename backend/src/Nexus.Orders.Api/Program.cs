using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nexus.Orders.Application.Interfaces;
using Nexus.Orders.Application.Models;
using Nexus.Orders.Infrastructure.Http;
using Nexus.Orders.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod();
    });
});

var graphQlUrl = builder.Configuration["ExternalProducts:GraphqlUrl"] ?? "http://host.docker.internal:4000/graphql";
var graphQlApiKey = builder.Configuration["ExternalProducts:ApiKey"] ?? "dev-sap-api-key-123";
var rabbitMqHost = builder.Configuration["RabbitMq:HostName"] ?? "rabbitmq";
var rabbitMqUserName = builder.Configuration["RabbitMq:UserName"] ?? "guest";
var rabbitMqPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddHttpClient<IExternalOrderIntegration, ExternalOrderIntegration>(client =>
{
    client.BaseAddress = new Uri(graphQlUrl);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphQlApiKey);
});
builder.Services.AddSingleton(_ => new ConnectionFactory
{
    HostName = rabbitMqHost,
    UserName = rabbitMqUserName,
    Password = rabbitMqPassword
});
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<RabbitMqWorkerManager>();

var app = builder.Build();
app.UseCors("AllowAngular");

var workerManager = app.Services.GetRequiredService<RabbitMqWorkerManager>();
_ = Task.Run(workerManager.Initialize);

app.MapGet("/api/worker/stop", (RabbitMqWorkerManager worker) =>
{
    worker.Stop();
    return Results.Ok("Consumer stopped. Messages will now pile up cleanly as READY.");
});

app.MapGet("/api/worker/start", (RabbitMqWorkerManager worker) =>
{
    worker.Start();
    return Results.Ok("Consumer started. Draining backlog...");
});

app.MapPost("/api/mulesoft/orders", ([FromBody] SalesOrder order, IMessagePublisher publisher) =>
{
    if (order == null || string.IsNullOrWhiteSpace(order.PoNumber) || string.IsNullOrWhiteSpace(order.Vendor) || string.IsNullOrWhiteSpace(order.MaterialCode) || string.IsNullOrWhiteSpace(order.MaterialDescription) || string.IsNullOrWhiteSpace(order.DeliveryDate) || order.Quantity <= 0)
    {
        return Results.BadRequest("Invalid Order Format");
    }

    try
    {
        order.Status = "released";
        publisher.PublishOrder(order);
        return Results.Accepted("/api/mulesoft/orders", new { status = $"Queued for SAP Processing: {order.PoNumber}" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Gateway Engine Error: {ex.Message}");
    }
});

app.Run();

public sealed class RabbitMqWorkerManager : IDisposable
{
    private const string QueueName = "sap-order-queue";
    private const string ExchangeName = "sap.orders";
    private const string RoutingKey = "it";

    private readonly IExternalOrderIntegration _externalIntegration;
    private readonly ConnectionFactory _factory;

    public RabbitMqWorkerManager(IExternalOrderIntegration externalIntegration, ConnectionFactory factory)
    {
        _externalIntegration = externalIntegration;
        _factory = factory;
    }

    private readonly object _syncRoot = new();

    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;
    private string? _consumerTag;

    public void Initialize()
    {
        Thread.Sleep(10000);

        try
        {
            lock (_syncRoot)
            {
                if (_channel != null)
                {
                    return;
                }

                _connection = _factory.CreateConnection();
                _channel = _connection.CreateModel();
                DeclareRabbitTopology(_channel);
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += async (_, ea) => await ProcessMessageAsync(ea);

                Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Worker Error]: {ex.Message}");
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!string.IsNullOrEmpty(_consumerTag) && _channel?.IsOpen == true)
            {
                _channel.BasicCancel(_consumerTag);
                _consumerTag = null;
            }
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (!string.IsNullOrEmpty(_consumerTag) || _channel?.IsOpen != true || _consumer == null)
            {
                return;
            }

            _consumerTag = _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: _consumer);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea)
    {
        if (_channel == null)
        {
            return;
        }

        try
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var incomingOrder = JsonSerializer.Deserialize<SalesOrder>(message);

            if (!IsProcessable(incomingOrder))
            {
                Console.WriteLine($"[RabbitMQ Worker] Discarded invalid purchase order payload: {message}");
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                return;
            }

            var order = incomingOrder!;
            Console.WriteLine($"[RabbitMQ Worker] Picked up Purchase Order {order.PoNumber} from queue.");
            await _externalIntegration.SendOrderAsync(order);
            Console.WriteLine($"[SAP RFC Connector] SUCCESS: Integrated Purchase Order {order.PoNumber} for vendor: {order.Vendor}");

            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (GraphQlIntegrationException ex) when (!ex.IsRetryable)
        {
            Console.WriteLine($"[Worker Error - Permanent]: {ex.Message}");
            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Worker Error - Retryable]: {ex.Message}");
            _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static bool IsProcessable(SalesOrder? order)
    {
        return order != null
            && !string.IsNullOrWhiteSpace(order.PoNumber)
            && !string.IsNullOrWhiteSpace(order.Vendor)
            && !string.IsNullOrWhiteSpace(order.MaterialCode)
            && !string.IsNullOrWhiteSpace(order.MaterialDescription)
            && !string.IsNullOrWhiteSpace(order.DeliveryDate)
            && order.Quantity > 0;
    }

    private static void DeclareRabbitTopology(IModel channel)
    {
        channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey);
    }
}



