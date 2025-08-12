using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OrderAPI.Enum;

namespace OrderAPI.Models
{
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }

        public int Value { get; set; }
        public string Customer { get; set; }
        public OrderStatus Status { get; set; }
    }
}
