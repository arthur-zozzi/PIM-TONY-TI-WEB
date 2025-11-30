// Models/ErrorViewModel.cs
namespace TonyTI_Web.Models
{
    // Modelo padrão utilizado na exibição de erros
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }  // Identificador da requisição para rastreamento

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId); // Exibe o ID somente se existir
    }
}
