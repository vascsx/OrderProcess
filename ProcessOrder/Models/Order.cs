using ProcessOrder.Enum;
using System.Text.Json.Serialization;

namespace ProcessOrder.Models
{

    public class Order
    {
        public int OrderId { get; set; }
        public int Value { get; set; }
        public string Customer { get; set; }

        public OrderStatus Status { get; set; }

    }
}
