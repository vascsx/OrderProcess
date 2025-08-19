using OrderAPI.Enum;

namespace OrderAPI.Entities
{
    public record OrderResponse(
    int OrderId,
    string CustomerName,
    decimal Value,
    OrderStatus Status
  );

}
