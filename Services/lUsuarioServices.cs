using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    // IUsuarioService.cs
    public interface IUsuarioService
    {
        Task<Usuario?> AuthenticateAsync(string email, string senha);
        Task<Usuario> CreateAsync(string email, string senha, string nome);
        Task<bool> EmailExistsAsync(string email);

        // novos métodos para recuperação de senha
        Task<bool> SetRecoveryCodeAsync(string email, string code); // salva code em recuperacaoSenha
        Task<bool> VerifyRecoveryCodeAsync(string email, string code); // verifica se code bate
        Task<bool> UpdatePasswordWithHashAsync(string email, string newHash); // atualiza senha para hash e limpa recuperacaoSenha
    }

}
