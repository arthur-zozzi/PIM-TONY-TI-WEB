using System.Collections.Generic;
using System.Threading.Tasks;
using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    // Interface do serviço de chamados (contrato para implementação)
    public interface IChamadoService
    {
        Task<IEnumerable<Chamado>> GetAllAsync(); // Retorna todos os chamados
        Task<IEnumerable<Chamado>> GetByEmailAsync(string email); // Filtra por e-mail
        Task<Chamado?> GetByIdAsync(int id); // Busca por Id
        Task<int> CreateAsync(Chamado model); // Cria novo chamado e retorna Id
        Task<bool> UpdateAsync(Chamado model); // Atualiza chamado existente

        // Atualiza apenas a resposta do técnico e o status do chamado
        Task<bool> UpdateRespostaAsync(int chamadoId, string respostaTecnico, string novoStatus = "Respondido");
    }
}
