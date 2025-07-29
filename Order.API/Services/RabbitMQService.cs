using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderAPI.Models;
using RabbitMQ.Client;

namespace OrderAPI.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly RabbitMQSettings _settings;
        private readonly IConnection _connection;

        public RabbitMQService(IOptions<RabbitMQSettings> options)
        {
            _settings = options.Value;

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = factory.CreateConnection();
        }

        public void Publish(Order order)
        {
            using var channel = _connection.CreateModel();

            channel.QueueDeclare(queue: _settings.QueueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var json = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(json);

            channel.BasicPublish(exchange: "", routingKey: _settings.QueueName, body: body);
        }
    }
}
