using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TonyTI_Web.Data;
using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    // Serviço para operações CRUD de chamados usando conexão ADO.NET fornecida pela fábrica
    public class ChamadoService : IChamadoService
    {
        private readonly ISqlConnectionFactory _factory;

        public ChamadoService(ISqlConnectionFactory factory)
        {
            _factory = factory;
        }

        // Retorna todos os chamados (ordenados pela data de abertura, mais recentes primeiro)
        public async Task<IEnumerable<Chamado>> GetAllAsync()
        {
            var list = new List<Chamado>();

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Nome, Email, Telefone, Urgencia, Descricao, 
                       DataAbertura, Status, RespostaTecnico, Anexo
                FROM Chamados
                ORDER BY DataAbertura DESC";

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(ReadChamadoFromReader(rdr));

            return list;
        }

        // Retorna chamados filtrados por e-mail
        public async Task<IEnumerable<Chamado>> GetByEmailAsync(string email)
        {
            var list = new List<Chamado>();

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Nome, Email, Telefone, Urgencia, Descricao, 
                       DataAbertura, Status, RespostaTecnico, Anexo
                FROM Chamados
                WHERE Email = @Email
                ORDER BY DataAbertura DESC";

            AddParam(cmd, "@Email", email);

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(ReadChamadoFromReader(rdr));

            return list;
        }

        // Busca um chamado por Id
        public async Task<Chamado?> GetByIdAsync(int id)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Nome, Email, Telefone, Urgencia, Descricao, 
                       DataAbertura, Status, RespostaTecnico, Anexo
                FROM Chamados
                WHERE Id = @Id";

            AddParam(cmd, "@Id", id);

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
                return ReadChamadoFromReader(rdr);

            return null;
        }

        // Insere um novo chamado e retorna o Id gerado (0 em caso de falha)
        public async Task<int> CreateAsync(Chamado model)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Chamados
                (Nome, Email, Telefone, Urgencia, Descricao, DataAbertura, Status, RespostaTecnico, Anexo)
                VALUES
                (@Nome, @Email, @Telefone, @Urgencia, @Descricao, @DataAbertura, @Status, @RespostaTecnico, @Anexo);
                SELECT SCOPE_IDENTITY();";

            AddParam(cmd, "@Nome", model.Nome);
            AddParam(cmd, "@Email", model.Email);
            AddParam(cmd, "@Telefone", model.Telefone);
            AddParam(cmd, "@Urgencia", model.Urgencia);
            AddParam(cmd, "@Descricao", model.Descricao);
            // usa UTC por consistência de logs
            AddParam(cmd, "@DataAbertura", model.DataAbertura ?? DateTime.UtcNow);
            AddParam(cmd, "@Status", model.Status ?? "Aberto");
            AddParam(cmd, "@RespostaTecnico", model.RespostaTecnico);
            AddParam(cmd, "@Anexo", model.Anexo);

            var scalar = await cmd.ExecuteScalarAsync();

            if (scalar == null || scalar == DBNull.Value)
            {
                // nenhum id retornado
                return 0;
            }

            try
            {
                // SCOPE_IDENTITY costuma vir como decimal — converter com segurança
                var asDecimal = Convert.ToDecimal(scalar);
                return Convert.ToInt32(asDecimal);
            }
            catch
            {
                // fallback: tentar conversão direta para int
                try
                {
                    return Convert.ToInt32(scalar);
                }
                catch
                {
                    return 0;
                }
            }
        }

        // Atualiza um chamado existente
        public async Task<bool> UpdateAsync(Chamado model)
        {
            if (model.Id <= 0) return false;

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Chamados SET
                    Nome = @Nome,
                    Email = @Email,
                    Telefone = @Telefone,
                    Urgencia = @Urgencia,
                    Descricao = @Descricao,
                    Status = @Status,
                    RespostaTecnico = @RespostaTecnico,
                    Anexo = @Anexo
                WHERE Id = @Id";

            AddParam(cmd, "@Nome", model.Nome);
            AddParam(cmd, "@Email", model.Email);
            AddParam(cmd, "@Telefone", model.Telefone);
            AddParam(cmd, "@Urgencia", model.Urgencia);
            AddParam(cmd, "@Descricao", model.Descricao);
            AddParam(cmd, "@Status", model.Status);
            AddParam(cmd, "@RespostaTecnico", model.RespostaTecnico);
            AddParam(cmd, "@Anexo", model.Anexo);
            AddParam(cmd, "@Id", model.Id);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // Atualiza apenas a resposta do técnico e o status
        public async Task<bool> UpdateRespostaAsync(int chamadoId, string respostaTecnico, string novoStatus = "Respondido")
        {
            if (chamadoId <= 0) return false;

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Chamados
                SET RespostaTecnico = @respostaTecnico,
                    Status = @status
                WHERE Id = @id";

            AddParam(cmd, "@respostaTecnico", respostaTecnico);
            AddParam(cmd, "@status", novoStatus);
            AddParam(cmd, "@id", chamadoId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // Helpers -----------------------------

        // Constrói um objeto Chamado a partir do IDataRecord lido do reader
        private static Chamado ReadChamadoFromReader(IDataRecord r)
        {
            return new Chamado
            {
                Id = Convert.ToInt32(r["Id"]),
                Nome = GetNullableString(r, "Nome"),
                Email = GetNullableString(r, "Email"),
                Telefone = GetNullableString(r, "Telefone"),
                Urgencia = GetNullableString(r, "Urgencia"),
                Descricao = GetNullableString(r, "Descricao"),
                DataAbertura = r["DataAbertura"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["DataAbertura"]),
                Status = GetNullableString(r, "Status"),
                RespostaTecnico = GetNullableString(r, "RespostaTecnico"),
                Anexo = GetNullableString(r, "Anexo")
            };
        }

        // Retorna string ou null ao ler colunas que podem ser DBNull
        private static string? GetNullableString(IDataRecord r, string name)
        {
            return r[name] == DBNull.Value ? null : r[name].ToString();
        }

        // Adiciona parâmetro ao comando, convertendo null para DBNull.Value
        private static void AddParam(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
