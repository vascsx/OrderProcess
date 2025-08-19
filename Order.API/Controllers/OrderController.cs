using Microsoft.AspNetCore.Mvc;
using OrderAPI.DataBase;
using OrderAPI.Enum;
using OrderAPI.Models;
using System.Text.Json;

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
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CreateOrder([FromBody] JsonElement requestBody)
    {
        try
        {
            var (isValid, request, errorResponse) = await ValidateAndDeserializeRequest(requestBody);
            if (!isValid)
            {
                return BadRequest(errorResponse);
            }

            var order = await CreateAndSaveOrder(request);

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

    [HttpGet("{orderId}/status/{status}")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> GetOrderStatus(int orderId, OrderStatus status)
    {
        try
        {
            var order = await _db.Order.FindAsync(orderId);

            if (order == null)
            {
                return NotFound(ApiResponse<OrderResponse>.Error(
                    $"Pedido {orderId} não encontrado",
                    "NOT_FOUND"));
            }

            if (order.OrderStatus != status)
            {
                return BadRequest(ApiResponse<OrderResponse>.Error(
                    $"Status mismatch. Current: {order.OrderStatus}, Requested: {status}",
                    "STATUS_MISMATCH"));
            }

            return Ok(ApiResponse<OrderResponse>.Success(
                new OrderResponse(order.Id, order.CustomerName, order.Value, order.OrderStatus),
                "Pedido encontrado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao buscar pedido {orderId}");
            return StatusCode(500, ApiResponse<OrderResponse>.Error(
                "Erro interno ao buscar pedido",
                "SERVER_ERROR"));
        }
    }

    #region Private Methods

    private async Task<(bool isValid, CreateOrderRequest request, ApiResponse<OrderResponse> errorResponse)>
        ValidateAndDeserializeRequest(JsonElement requestBody)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var request = requestBody.Deserialize<CreateOrderRequest>(options) ??
                         throw new InvalidOperationException("Request body is null");

            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.CustomerName))
                validationErrors.Add("O campo 'CustomerName' é obrigatório");

            if (request.Value <= 0)
                validationErrors.Add("O valor deve ser maior que zero");

            if (validationErrors.Any())
            {
                return (false, null, ApiResponse<OrderResponse>.Error(
                    "Dados inválidos",
                    "VALIDATION_ERROR",
                    validationErrors));
            }

            return (true, request, null);
        }
        catch (JsonException ex) when (ex.Path != null)
        {
            string fieldName = ex.Path.Split('.').Last();
            var errorMsgs = GetFieldSpecificErrorMessage(fieldName, ex.Message);

            return (false, null, ApiResponse<OrderResponse>.Error(
                "Erro de validação",
                "INVALID_FIELD",
                errorMsgs.ToArray())); 
        }
        catch (JsonException ex)
        {
            return (false, null, ApiResponse<OrderResponse>.Error(
                "Formato JSON inválido",
                "INVALID_JSON",
                new[] { ex.Message }));
        }
        catch (InvalidOperationException ex)
        {
            return (false, null, ApiResponse<OrderResponse>.Error(
                "Requisição inválida",
                "EMPTY_REQUEST",
                new[] { ex.Message }));
        }
    }

    private List<string> GetFieldSpecificErrorMessage(string fieldName, string originalMessage)
    {
        return fieldName switch
        {
            nameof(CreateOrderRequest.CustomerName) => new List<string>
        {
            "O campo 'CustomerName' deve ser uma string válida",
            originalMessage
        },
            nameof(CreateOrderRequest.Value) => new List<string>
        {
            "O campo 'Value' deve ser um número decimal positivo",
            originalMessage
        },
            _ => new List<string> { $"Erro no campo '{fieldName}': {originalMessage}" }
        };
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

public class ApiResponse<T>
{
    public string Message { get; set; }
    public T Data { get; set; }
    public string Code { get; set; }
    public DateTime Timestamp { get; set; }
    public IEnumerable<string> Errors { get; set; }

    public static ApiResponse<T> Success(T data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Message = message,
            Data = data,
            Code = "SUCCESS",
            Timestamp = DateTime.UtcNow
        };
    }

    public static ApiResponse<T> Error(string message, string code, IEnumerable<string> errors = null)
    {
        return new ApiResponse<T>
        {
            Message = message,
            Code = code,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }
}

public record OrderResponse(int OrderId, string CustomerName, decimal Value, OrderStatus Status);

#endregion