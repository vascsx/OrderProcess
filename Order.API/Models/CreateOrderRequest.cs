using System.ComponentModel.DataAnnotations;

namespace OrderAPI.Models
{
    public class CreateOrderRequest
    {
        [Required(ErrorMessage = "Customer name is required")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Order value is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }
    }
}