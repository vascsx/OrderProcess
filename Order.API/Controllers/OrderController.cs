using Microsoft.AspNetCore.Mvc;
using OrderAPI.Models;

namespace OrderAPI.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IRabbitMQService _rabbitMQService;

        public OrderController(IRabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost]
        public IActionResult CreateOrder([FromBody] Order order)
        {
            _rabbitMQService.Publish(order);
            return Ok(new { message = "Pedido enviado com sucesso!" });
        }
    }

}
