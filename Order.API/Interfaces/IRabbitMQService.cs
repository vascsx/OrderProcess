
using OrderAPI.Models;
using RabbitMQ.Client;

public interface IRabbitMQService : IDisposable
{
    RabbitMQSettings Settings { get; }
    Task<IConnection> GetConnectionAsync();
    Task PublishAsync(Order order); 
}