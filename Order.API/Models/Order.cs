using OrderAPI.Enum;
using System.Text.Json.Serialization;

namespace OrderAPI.Models
{
    public class Order
    {
        public int OrderId { get; set; }
        public int Value { get; set; }
        public string Customer { get; set; }

        [JsonIgnore]
        public OrderStatus Status { get; set; }

    }

}
