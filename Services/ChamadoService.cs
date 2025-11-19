// Services/ChamadoService.cs
using Microsoft.Data.SqlClient;
using TonyTI_Web.Data;
using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    public class ChamadoService : IChamadoService
    {
        private readonly ISqlConnectionFactory _factory;
        public ChamadoService(ISqlConnectionFactory factory) { _factory = factory; }

        public async Task<IEnumerable<Chamado>> GetAllAsync()
        {
            var list = new List<Chamado>();
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Nome, Email, Telefone, Urgencia, Descricao, DataAbertura, Status FROM Chamados ORDER BY DataAbertura DESC";

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new Chamado
                {
                    Id = rdr.GetInt32(0),
                    Nome = rdr.GetString(1),
                    Email = rdr.GetString(2),
                    Telefone = rdr.GetString(3),
                    Urgencia = rdr.GetString(4),
                    Descricao = rdr.GetString(5),
                    DataAbertura = rdr.GetDateTime(6),
                    Status = rdr.GetString(7)
                });
            }
            return list;
        }

        public async Task<Chamado?> GetByIdAsync(int id)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Nome, Email, Telefone, Urgencia, Descricao, DataAbertura, Status FROM Chamados WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Chamado
                {
                    Id = rdr.GetInt32(0),
                    Nome = rdr.GetString(1),
                    Email = rdr.GetString(2),
                    Telefone = rdr.GetString(3),
                    Urgencia = rdr.GetString(4),
                    Descricao = rdr.GetString(5),
                    DataAbertura = rdr.GetDateTime(6),
                    Status = rdr.GetString(7)
                };
            }
            return null;
        }

        public async Task<Chamado> CreateAsync(Chamado novo)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Chamados (Nome, Email, Telefone, Urgencia, Descricao, DataAbertura, Status) OUTPUT INSERTED.Id VALUES (@nome,@email,@tel,@urg,@desc,@data,@status)";
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@nome", novo.Nome));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@email", novo.Email));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@tel", novo.Telefone));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@urg", novo.Urgencia));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@desc", novo.Descricao));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@data", DateTime.UtcNow));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@status", novo.Status));

            var idObj = await cmd.ExecuteScalarAsync();
            var id = Convert.ToInt32(idObj);
            novo.Id = id;
            novo.DataAbertura = DateTime.UtcNow;
            return novo;
        }

        public async Task<bool> AddRespostaAsync(int id, string resposta, string tecnico)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Chamados SET RespostaTecnico = @resp, Status = 'Respondido' WHERE Id = @id";
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@resp", resposta));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@id", id));

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Chamados SET Status = @status WHERE Id = @id";
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@status", status));
            cmd.Parameters.Add(cmd.CreateParameterWithValue("@id", id));

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }
}
