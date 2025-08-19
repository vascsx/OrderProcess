using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderAPI.Models
{
    public class ApiResponse<T>
    {
        public string Message { get; set; }
        public T Data { get; set; }
        public string Code { get; set; }
        public DateTime Timestamp { get; set; }
        public IEnumerable<string> Errors { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success") => new()
        {
            Message = message,
            Data = data,
            Code = "SUCCESS",
            Timestamp = DateTime.UtcNow
        };

        public static ApiResponse<T> Error(string message, string code, IEnumerable<string> errors = null) => new()
        {
            Message = message,
            Code = code,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }
}
