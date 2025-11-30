// Models/Usuario.cs
namespace TonyTI_Web.Models
{
    // Modelo que representa um usuário do sistema
    public class Usuario
    {
        public string Email { get; set; } = string.Empty;   // E-mail usado para login
        public string Senha { get; set; } = string.Empty;   // Senha do usuário
        public string? Nome { get; set; }                   // Nome completo
        public string? Foto { get; set; }                   // Caminho da foto de perfil
        public string? RecuperacaoSenha { get; set; }       // Código para recuperação de senha
        public string? Perfil { get; set; }                 // Perfil de acesso (Admin/Usuário)
    }
}
