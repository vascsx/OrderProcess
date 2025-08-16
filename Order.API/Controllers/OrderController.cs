using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderAPI.DataBase;
using OrderAPI.Enum;
using OrderAPI.Models;
using System;
using System.Threading.Tasks;

namespace OrderAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<OrderController> _logger;
        private readonly AppDbContext _db;

        public OrderController(
            IRabbitMQService rabbitMQService,
            ILogger<OrderController> logger,
            AppDbContext db)
        {
            _rabbitMQService = rabbitMQService;
            _logger = logger;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            var order = new Order
            {
                CustomerName = request.CustomerName,
                Value = request.Value,
                OrderStatus = OrderStatus.Created,
            };

            var validationResult = ValidateOrder(order);

            if (validationResult != null)
                return BadRequest(new { message = validationResult });

            try
            {
                await _db.Order.AddAsync(order);
                await _db.SaveChangesAsync();

                _rabbitMQService.Publish(order);

                _logger.LogInformation($"Pedido {order.Id} criado com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pedido");
                return StatusCode(500, new { message = "Erro ao processar pedido" });
            }

            return Ok(new
            {
                message = "Pedido criado com sucesso!",
                orderId = order.Id
            });
        }

        [HttpGet("{orderId}/status/{status}")]
        public async Task<IActionResult> GetByOrderIdByStatus(int orderId, OrderStatus status)
        {
            try
            {
                var order = await _db.Order.FindAsync(orderId);

                if (order == null)
                    return NotFound(new { message = $"Pedido {orderId} não encontrado." });

                if (order.OrderStatus != status)
                    return BadRequest(new { message = $"O pedido {orderId} existe, mas o status atual é '{order.OrderStatus}' e não '{status}'." });

                return Ok(new
                {
                    message = "Pedido encontrado com sucesso!",
                    orderId = order.Id,
                    customer = order.CustomerName,
                    value = order.Value,
                    status = order.OrderStatus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar pedido {orderId} por status");
                return StatusCode(500, new { message = "Erro interno ao buscar pedido" });
            }
        }

        private string? ValidateOrder(Order order)
        {
            if (order == null)
                return "O corpo da requisição está vazio.";

            if (string.IsNullOrWhiteSpace(order.CustomerName))
                return "O campo 'Customer' é obrigatório.";

            if (order.Value <= 0)
                return "O valor deve ser maior que zero.";

            return null;
        }
    }
}