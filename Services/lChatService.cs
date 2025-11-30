using System.Threading.Tasks;

namespace TonyTI_Web.Services
{
    public interface IChatService
    {
        /// <summary>
        /// Envia a pergunta do usuário para o serviço de IA e retorna a resposta.
        /// Deve aplicar validações de escopo (apenas tecnologia).
        /// </summary>
        Task<string> AskAsync(string userEmail, string question);
    }
}
