
using ProcessOrder.Models;
using RabbitMQ.Client;

public interface IRabbitMQService
{
    void Publish(Order order);
    IConnection GetConnection();

}