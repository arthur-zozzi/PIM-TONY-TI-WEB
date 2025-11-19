// Models/ErrorViewModel.cs
namespace TonyTI_Web.Models
{
    public class ErrorViewModel
    {
        // RequestId usado pelo template padrão de Error.cshtml
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
