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
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

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

            _ = InitializeConnectionAsync();
        }

        private async Task InitializeConnectionAsync()
        {
            try
            {
                await EnsureConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
            }
        }

        private async Task EnsureConnectionAsync()
        {
            if (_connection?.IsOpen == true) return;

            await _connectionLock.WaitAsync();
            try
            {
                if (_connection?.IsOpen == true) return;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.HostName,
                        Port = _settings.Port ?? 5672,
                        UserName = _settings.UserName,
                        Password = _settings.Password,
                        VirtualHost = _settings.VirtualHost ?? "/",
                        DispatchConsumersAsync = true,
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
                        RequestedConnectionTimeout = TimeSpan.FromSeconds(60),
                        ContinuationTimeout = TimeSpan.FromSeconds(60),
                        HandshakeContinuationTimeout = TimeSpan.FromSeconds(60)
                    };

                    _connection?.Dispose();
                    _connection = factory.CreateConnection();

                    _logger.LogInformation("Successfully connected to RabbitMQ");
                    await Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create RabbitMQ connection");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<IConnection> GetConnectionAsync()
        {
            await EnsureConnectionAsync();
            return _connection;
        }

        public async Task PublishAsync(Order order)
        {
            await EnsureConnectionAsync();

            IModel channel = null;
            try
            {
                channel = _connection.CreateModel();
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
            finally
            {
                channel?.Close();
                channel?.Dispose();
            }
        }

        public void Publish(Order order)
        {
            PublishAsync(order).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            try
            {
                _connection?.Close();
                _connection?.Dispose();
                _connectionLock.Dispose();
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