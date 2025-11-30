// Models/Chamado.cs
using System;

namespace TonyTI_Web.Models
{
    // Modelo que representa um chamado no sistema de suporte
    public class Chamado
    {
        public int Id { get; set; }                     // Identificador do chamado
        public string? Nome { get; set; }               // Nome do solicitante
        public string? Email { get; set; }              // E-mail para contato
        public string? Telefone { get; set; }           // Telefone para retorno
        public string? Urgencia { get; set; }           // Nível de urgência do problema
        public string? Descricao { get; set; }          // Descrição do chamado
        public DateTime? DataAbertura { get; set; }     // Data de abertura do chamado
        public string? Status { get; set; }             // Status atual do chamado
        public string? RespostaTecnico { get; set; }    // Resposta ou solução do técnico
        public string? Anexo { get; set; }              // Caminho do anexo enviado pelo usuário
    }
}
