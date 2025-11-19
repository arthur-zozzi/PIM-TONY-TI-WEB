// Models/Usuario.cs
namespace TonyTI_Web.Models
{
    public class Usuario
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string? Nome { get; set; }
        public string? Foto { get; set; }
        public string? RecuperacaoSenha { get; set; }
        public string? Perfil { get; set; }
    }
}
