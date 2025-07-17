
using System.Data.Common;
using System.Runtime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using OrderAPI.Models;
using RabbitMQ.Client;

namespace OrderAPI.Services
{
    public interface IRabbitMQService
    {
        void Publish(Order order);
    }

    public class RabbitMQService : IRabbitMQService
    {
        private readonly RabbitMQSettings _settings;
        private IConnection _connection;

        public void Publish(Order order)
        {

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password
            };
            _connection = factory.CreateConnection();
            using var channel = _connection.CreateModel();

            channel.QueueDeclare(queue: "orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var json = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(json);

            channel.BasicPublish(exchange: "", routingKey: "orders", body: body);
        }
    }

}
