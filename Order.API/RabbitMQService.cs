
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using OrderAPI.Models;
using RabbitMQ.Client;

namespace OrderAPI
{
    public interface IRabbitMQService
    {
        void Publish(Order order);
    }

    public class RabbitMQService : IRabbitMQService
    {
        public void Publish(Order order)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var json = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(json);

            channel.BasicPublish(exchange: "", routingKey: "orders", body: body);
        }
    }

}
