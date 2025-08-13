using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ProcessOrder.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ProcessOrder.Services
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private IConnection _connection;
        private bool _disposed;
        private readonly object _connectionLock = new();

        public RabbitMQSettings Settings => _settings;

        public RabbitMQService(IOptions<RabbitMQSettings> options, ILogger<RabbitMQService> logger)
        {
            _settings = options.Value;
            _logger = logger;

            _retryPolicy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    _settings.RetryCount.GetValueOrDefault(3),
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) => _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Retrying in {TimeTotalSeconds}s...", time.TotalSeconds));
        }

        public IConnection GetConnection()
        {
            if (_connection?.IsOpen == true) return _connection;

            lock (_connectionLock)
            {
                if (_connection?.IsOpen == true) return _connection;

                _retryPolicy.ExecuteAsync(async () =>
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.HostName,
                        Port = 5672,
                        UserName = _settings.UserName,
                        Password = _settings.Password,
                        VirtualHost = "/",
                        DispatchConsumersAsync = true,
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(30)
                    };

                    _connection?.Dispose();
                    _connection = factory.CreateConnection();
                    _connection.ConnectionShutdown += OnConnectionShutdown;

                    _logger.LogInformation("Successfully connected to RabbitMQ");

                    await Task.CompletedTask;
                }).GetAwaiter().GetResult();
            }

            return _connection;
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", e.ReplyText);
            if (_disposed) return;

            try
            {
                GetConnection();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to RabbitMQ after shutdown");
            }
        }

        public void Publish(Order order)
        {
            using var channel = GetConnection().CreateModel();
            channel.QueueDeclare(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            var message = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(
                exchange: "",
                routingKey: _settings.QueueName,
                basicProperties: properties,
                body: body);

            _logger.LogDebug("Message published to queue {QueueName}", _settings.QueueName);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _connection?.Close();
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ connection closed");
        }
    }
}
