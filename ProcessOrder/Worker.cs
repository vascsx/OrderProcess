using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcessOrder.DataBase;
using ProcessOrder.Enum;
using ProcessOrder.Models;
using ProcessOrder.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ProcessOrder
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private IConnection _connection;
        private IModel _channel;
        private RabbitMQSettings _settings;

        public Worker(
            ILogger<Worker> logger,
            IRabbitMQService rabbitMQService,
            IOptions<RabbitMQSettings> options,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _contextFactory = contextFactory;
            _settings = options.Value;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _connection = _rabbitMQService.GetConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var order = JsonSerializer.Deserialize<Order>(message);

                    _logger.LogInformation($"Pedido recebido: ID={order.Id}, Customer={order.CustomerName}, Value={order.Value}");

                    using var db = _contextFactory.CreateDbContext();

                    var orderToUpdate = await db.Order.FindAsync(order.Id);

                    if (orderToUpdate != null)
                    {
                        orderToUpdate.OrderStatus = OrderStatus.Processed;
                        orderToUpdate.UpdatedAt = DateTime.Now;

                        await db.SaveChangesAsync();

                        _logger.LogInformation($"Pedido {order.Id} processado");
                    }
                    else
                    {
                        _logger.LogWarning($"Pedido {order.Id} não encontrado no banco.");
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar o pedido.");
                    _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                }
            };

            _channel.BasicConsume(
                queue: _settings.QueueName,
                autoAck: false,
                consumer: consumer);

            return Task.CompletedTask;
        }
    }
}
