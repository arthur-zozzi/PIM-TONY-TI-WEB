using System.ComponentModel.DataAnnotations;

namespace TonyTI_Web.Models
{
    // Modelo utilizado para cadastro de novos usuários
    public class Register
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; }                 // E-mail do usuário

        [Required(ErrorMessage = "Informe o nome.")]
        public string Nome { get; set; }                  // Nome completo

        [Required(ErrorMessage = "Informe a senha.")]
        [MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres.")]
        [RegularExpression(@"^(?=.*\p{L})(?=.*\d).+$",
            ErrorMessage = "A senha deve conter pelo menos uma letra e um número.")]
        public string Senha { get; set; }                 // Senha com validações básicas

        [Required(ErrorMessage = "Confirme a senha.")]
        [Compare("Senha", ErrorMessage = "As senhas não conferem.")]
        public string ConfirmarSenha { get; set; }        // Confirmação da senha
    }
}
