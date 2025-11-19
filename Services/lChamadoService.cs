using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    public interface IChamadoService
    {
        Task<IEnumerable<Chamado>> GetAllAsync();
        Task<Chamado?> GetByIdAsync(int id);
        Task<Chamado> CreateAsync(Chamado novo);
        Task<bool> AddRespostaAsync(int id, string resposta, string tecnico);
        Task<bool> UpdateStatusAsync(int id, string status);
    }
}
