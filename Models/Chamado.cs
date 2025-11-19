using System;

namespace TonyTI_Web.Models
{
    public class Chamado
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Urgencia { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public DateTime DataAbertura { get; set; }
        public string Status { get; set; } = "Aberto";
        public string? RespostaTecnico { get; set; }
    }
}
