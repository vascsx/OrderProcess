using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OrderAPI.DataBase;
using OrderAPI.Entities;
using OrderAPI.Enum;
using OrderAPI.Models;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Consumes("application/json")]
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
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CreateOrder()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var requestBodyRaw = await reader.ReadToEndAsync();

            JObject requestBody;
            try
            {
                requestBody = JObject.Parse(requestBodyRaw);
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                return BadRequest(ApiResponse<OrderResponse>.Error(
                    "Formato JSON inválido",
                    "INVALID_JSON",
                    new[] { ex.Message }));
            }

            var (isValid, request, errorResponse) = ValidateAndDeserializeRequest(requestBody);
            if (!isValid)
                return BadRequest(errorResponse);

            var order = await CreateAndSaveOrder(request!);

            return Ok(ApiResponse<OrderResponse>.Success(
                new OrderResponse(order.Id, order.CustomerName, order.Value, order.OrderStatus),
                "Pedido criado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar pedido");
            return StatusCode(500, ApiResponse<OrderResponse>.Error(
                "Erro interno no servidor",
                "SERVER_ERROR"));
        }
    }


    #region Private Methods

    private (bool isValid, CreateOrderRequest? request, ApiResponse<OrderResponse>? errorResponse)
        ValidateAndDeserializeRequest(JObject requestBody)
    {
        var errors = new List<string>();

        if (!requestBody.TryGetValue("CustomerName", out var customerNameToken) || customerNameToken.Type == JTokenType.Null)
        {
            errors.Add("O campo 'CustomerName' é obrigatório");
        }
        else if (customerNameToken.Type != JTokenType.String)
        {
            errors.Add("O campo 'CustomerName' deve ser uma string válida");
        }
        else if (string.IsNullOrWhiteSpace(customerNameToken.ToString()))
        {
            errors.Add("O campo 'CustomerName' não pode estar vazio");
        }

        if (!requestBody.TryGetValue("Value", out var valueToken) || valueToken.Type == JTokenType.Null)
        {
            errors.Add("O campo 'Value' é obrigatório");
        }
        else if (!decimal.TryParse(valueToken.ToString(), out var value))
        {
            errors.Add("O campo 'Value' deve ser um número decimal válido");
        }
        else if (value <= 0)
        {
            errors.Add("O valor deve ser maior que zero");
        }
        else if (string.IsNullOrWhiteSpace(value.ToString()))
        {
            errors.Add("O campo 'Value' não pode estar vazio");
        }

        if (errors.Any())
        {
            return (false, null, ApiResponse<OrderResponse>.Error(
                "Dados inválidos",
                "VALIDATION_ERROR",
                errors));
        }

        var request = requestBody.ToObject<CreateOrderRequest>();

        return (true, request, null);
    }

    private async Task<Order> CreateAndSaveOrder(CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerName = request.CustomerName!,
            Value = request.Value,
            OrderStatus = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Order.AddAsync(order);
        await _db.SaveChangesAsync();
        await _rabbitMQService.PublishAsync(order);

        return order;
    }

    #endregion
}

#region Support Classes
public record OrderResponse(int OrderId, string CustomerName, decimal Value, OrderStatus Status);
#endregion
