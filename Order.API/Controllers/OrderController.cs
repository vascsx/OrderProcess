using Microsoft.AspNetCore.Mvc;
using OrderAPI.Enum;
using OrderAPI.Models;

namespace OrderAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger _logger;
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

            order.Status = OrderStatus.Created;

            try
            {
                _rabbitMQService.Publish(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar o pedido no RabbitMQ.");
                return StatusCode(500, new { message = "Erro ao enviar o pedido para o sistema de mensagens." });
            }

            return Ok(new { message = "Pedido enviado com sucesso!" });
        }


        /// <summary>
        /// Valida os campos do pedido.
        /// </summary>
        /// <param name="order">Pedido recebido</param>
        /// <returns>Mensagem de erro ou null se válido</returns>
        private string?  ValidateOrder(Order order)
        {
            if (order == null)
                return "O corpo da requisição está vazio.";

            if (string.IsNullOrWhiteSpace(order.Customer))
                return "O campo 'Customer' é obrigatório.";

            if (order.Value <= 0)
                return "O valor deve ser maior que zero.";

            return null;
        }
    }
}
