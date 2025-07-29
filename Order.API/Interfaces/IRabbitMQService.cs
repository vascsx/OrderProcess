
using OrderAPI.Models;

public interface IRabbitMQService
{
    void Publish(Order order);
}