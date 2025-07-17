using Microsoft.AspNetCore.Mvc;
using OrderAPI.Models;
using OrderAPI.Services;

namespace OrderAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
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
            var validationResult = ValidateOrder(order);

            if (validationResult != null)
                return BadRequest(new { message = validationResult });

            _rabbitMQService.Publish(order);

            return Ok(new { message = "Pedido enviado com sucesso!" });
        }

        /// <summary>
        /// Valida os campos do pedido.
        /// </summary>
        /// <param name="order">Pedido recebido</param>
        /// <returns>Mensagem de erro ou null se válido</returns>
        private string? ValidateOrder(Order order)
        {
            if (order == null)
                return "O corpo da requisição está vazio.";

            if (string.IsNullOrWhiteSpace(order.OrderId))
                return "O campo 'OrderId' é obrigatório.";

            if (string.IsNullOrWhiteSpace(order.Product))
                return "O campo 'Product' é obrigatório.";

            if (order.Quantity <= 0)
                return "A quantidade deve ser maior que zero.";

            return null;
        }
    }
}
