namespace Andalos.API.DTOs.Common
{
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponseDto<T> SuccessResponse(T data, string message = "تمت العملية بنجاح")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponseDto<T> FailResponse(string message)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Data = default
            };
        }
    }
}