using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcessOrder.DataBase;
using ProcessOrder.Enum;
using ProcessOrder.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessOrder
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQSettings _settings;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private IConnection _connection;
        private IModel _channel;

        public Worker(ILogger<Worker> logger, IOptions<RabbitMQSettings> options, IDbContextFactory<AppDbContext> contextFactory)
        {
            _logger = logger;
            _settings = options.Value;
            _contextFactory = contextFactory;

            var factory = new ConnectionFactory()
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var order = JsonSerializer.Deserialize<Order>(message);

                    _logger.LogInformation($"O Pedido recebido: ID={order.OrderId}, Customer={order.Customer}, Value={order.Value}");

                    using var db = _contextFactory.CreateDbContext();

                    var orderToUpdate = await db.Order.FindAsync(order.OrderId);

                    if (orderToUpdate != null)
                    {
                        orderToUpdate.Status = OrderStatus.Processed;

                        await db.SaveChangesAsync();

                        _logger.LogInformation($"Pedido {order.OrderId} Processado");
                    }
                    else
                    {
                        _logger.LogWarning($"Pedido {order.OrderId} não encontrado no banco.");
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar o pedido.");
                    _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
                }
            };


            _channel.BasicConsume(queue: _settings.QueueName,
                                  autoAck: false,
                                  consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
