using ProcessOrder.Enum;

namespace ProcessOrder.Models
{
    public class OrderLog 
    {
        public long OrderLogId { get; set; }
        public long OrderId { get; set; }

        public OrderStatus Status { get; set; }
        public DateTime SentAt { get; set; }
    }
}