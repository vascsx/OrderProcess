using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderAPI.Models;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace OrderAPI.Services
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

            EnsureConnection();
        }

        private void EnsureConnection()
        {
            if (_connection?.IsOpen == true) return;

            lock (_connectionLock)
            {
                if (_connection?.IsOpen == true) return;

                _retryPolicy.ExecuteAsync(async () =>
                {
                    try
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
                            NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
                            RequestedConnectionTimeout = TimeSpan.FromSeconds(60),
                            ContinuationTimeout = TimeSpan.FromSeconds(60),
                            HandshakeContinuationTimeout = TimeSpan.FromSeconds(60)
                        };

                        _connection?.Dispose();
                        _connection = factory.CreateConnection();
                        _connection.ConnectionShutdown += OnConnectionShutdown;

                        _logger.LogInformation("Successfully connected to RabbitMQ");
                        await Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create RabbitMQ connection");
                        throw;
                    }
                }).GetAwaiter().GetResult();
            }
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", e.ReplyText);
            if (_disposed) return;

            try
            {
                EnsureConnection();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to RabbitMQ after shutdown");
            }
        }

        public IConnection GetConnection()
        {
            EnsureConnection();
            return _connection;
        }

        public void Publish(Order order)
        {
            EnsureConnection();

            try
            {
                using var channel = _connection.CreateModel();
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to RabbitMQ");
                throw new RabbitMQPublishException("Failed to publish message", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            try
            {
                _connection?.Close();
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ connection");
            }
        }
    }

    public class RabbitMQPublishException : Exception
    {
        public RabbitMQPublishException(string message, Exception inner)
            : base(message, inner) { }
    }
}
