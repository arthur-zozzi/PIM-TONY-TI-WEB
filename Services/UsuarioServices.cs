// Services/UsuarioService.cs
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TonyTI_Web.Data;
using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    // Serviço responsável por operações relacionadas a usuários (autenticação, criação, recuperação)
    public class UsuarioService : IUsuarioService
    {
        private readonly ISqlConnectionFactory _factory;
        private readonly ILogger<UsuarioService> _logger;

        public UsuarioService(ISqlConnectionFactory factory, ILogger<UsuarioService> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        // Autentica usuário por email e senha (suporta hash SHA256 e fallback para senha em texto)
        public async Task<Usuario?> AuthenticateAsync(string email, string senha)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogInformation("AuthenticateAsync chamado com email vazio.");
                return null;
            }

            var hash = ComputeHash(senha);
            _logger.LogDebug("AuthenticateAsync called for email={Email} (senhaLength={Len})", email, (senha ?? "").Length);

            try
            {
                await using var conn = _factory.CreateConnection();
                _logger.LogDebug("Connection string (masked): {Conn}", MaskConnectionString(conn.ConnectionString));
                await conn.OpenAsync();

                // 1) Recupera senha armazenada para o email
                string? storedPwd = null;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT senha FROM Usuarios WHERE email = @email";
                    var p = cmd.CreateParameter(); p.ParameterName = "@email"; p.Value = email; cmd.Parameters.Add(p);

                    await using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        storedPwd = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                    }
                }

                if (storedPwd == null)
                {
                    _logger.LogInformation("AuthenticateAsync: nenhum registro para email={Email}", email);
                    return null;
                }

                // Log de diagnóstico com preview mascarado
                _logger.LogDebug("StoredPwd length={Len}, preview='{PreviewStart}...{PreviewEnd}'",
                    storedPwd.Length,
                    storedPwd.Length >= 3 ? storedPwd.Substring(0, 3) : storedPwd,
                    storedPwd.Length >= 3 ? storedPwd.Substring(Math.Max(0, storedPwd.Length - 3)) : "");

                // 2) Comparação por hash
                if (string.Equals(storedPwd, hash, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Usuário autenticado por HASH: {Email}", email);
                    return await BuildUsuarioFromRowAsync(conn, email);
                }

                // 3) Fallback: senha em texto (migrar para hash)
                if (string.Equals(storedPwd, senha, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Usuário autenticado por senha PLANA (fallback). Migrando para HASH: {Email}", email);

                    try
                    {
                        await using var upd = conn.CreateCommand();
                        upd.CommandText = "UPDATE Usuarios SET senha = @hash WHERE email = @email";
                        var ph = upd.CreateParameter(); ph.ParameterName = "@hash"; ph.Value = hash; upd.Parameters.Add(ph);
                        var pe = upd.CreateParameter(); pe.ParameterName = "@email"; pe.Value = email; upd.Parameters.Add(pe);

                        var rows = await upd.ExecuteNonQueryAsync();
                        _logger.LogInformation("Senha migrada para HASH para {Email} (linhas afetadas: {Rows})", email, rows);
                    }
                    catch (Exception exUpd)
                    {
                        _logger.LogError(exUpd, "Falha ao atualizar senha para hash para {Email}", email);
                        // não interrompe a autenticação; apenas loga a falha de migração
                    }

                    return await BuildUsuarioFromRowAsync(conn, email);
                }

                // Não autenticou — log para diagnóstico
                _logger.LogWarning("Authenticate mismatch for {Email}: storedLen={StoredLen}, providedLen={ProvidedLen}", email, storedPwd.Length, (senha ?? "").Length);
                _logger.LogWarning("Stored preview='{Start}...{End}', Provided preview='{PStart}...{PEnd}'",
                    storedPwd.Length >= 4 ? storedPwd.Substring(0, 2) : storedPwd,
                    storedPwd.Length >= 4 ? storedPwd.Substring(storedPwd.Length - 2) : "",
                    (senha ?? "").Length >= 4 ? (senha ?? "").Substring(0, 2) : (senha ?? ""),
                    (senha ?? "").Length >= 4 ? (senha ?? "").Substring(Math.Max(0, (senha ?? "").Length - 2)) : "");

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao autenticar usuário {Email}", email);
                throw;
            }
        }

        // Busca dados completos do usuário após confirmar existência do email
        private static async Task<Usuario?> BuildUsuarioFromRowAsync(SqlConnection conn, string email)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT email, senha, nome, foto, recuperacaoSenha, Perfil FROM Usuarios WHERE email = @email";
            var p = cmd.CreateParameter(); p.ParameterName = "@email"; p.Value = email; cmd.Parameters.Add(p);

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Usuario
                {
                    Email = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0),
                    Senha = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                    Nome = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    Foto = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    RecuperacaoSenha = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                    Perfil = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                };
            }

            return null;
        }

        // Cria novo usuário (armazena senha como hash)
        public async Task<Usuario> CreateAsync(string email, string senha, string nome)
        {
            var hash = ComputeHash(senha);
            _logger.LogDebug("CreateAsync called for email={Email}", email);

            try
            {
                await using var conn = _factory.CreateConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Usuarios (email, senha, nome)
                    VALUES (@email, @senha, @nome)";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@email"; p1.Value = email; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@senha"; p2.Value = hash; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "@nome"; p3.Value = (object?)nome ?? DBNull.Value; cmd.Parameters.Add(p3);

                var rows = await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Usuário inserido: {Email} (linhas afetadas: {Rows})", email, rows);

                return new Usuario
                {
                    Email = email,
                    Senha = hash,
                    Nome = nome
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário {Email}", email);
                throw;
            }
        }

        // Verifica existência de email
        public async Task<bool> EmailExistsAsync(string email)
        {
            _logger.LogDebug("EmailExistsAsync called for email={Email}", email);

            try
            {
                await using var conn = _factory.CreateConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM Usuarios WHERE email = @email";
                var p = cmd.CreateParameter(); p.ParameterName = "@email"; p.Value = email; cmd.Parameters.Add(p);

                var countObj = await cmd.ExecuteScalarAsync();
                var count = Convert.ToInt32(countObj);
                _logger.LogDebug("EmailExistsAsync result for {Email}: {Count}", email, count);
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência do email {Email}", email);
                throw;
            }
        }

        // Gera hash SHA256 para a senha
        private static string ComputeHash(string senha)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(senha ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        // Salva código de recuperação no banco
        public async Task<bool> SetRecoveryCodeAsync(string email, string code)
        {
            _logger.LogInformation("SetRecoveryCodeAsync called for {Email}", email);
            try
            {
                await using var conn = _factory.CreateConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Usuarios SET recuperacaoSenha = @code WHERE email = @email";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@code"; p1.Value = code; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@email"; p2.Value = email; cmd.Parameters.Add(p2);

                var rows = await cmd.ExecuteNonQueryAsync();
                _logger.LogDebug("SetRecoveryCodeAsync affected rows={Rows} for {Email}", rows, email);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar código de recuperação para {Email}", email);
                return false;
            }
        }

        // Verifica se o código de recuperação corresponde ao registrado
        public async Task<bool> VerifyRecoveryCodeAsync(string email, string code)
        {
            _logger.LogDebug("VerifyRecoveryCodeAsync called for {Email}", email);
            try
            {
                await using var conn = _factory.CreateConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT recuperacaoSenha FROM Usuarios WHERE email = @email";
                var p = cmd.CreateParameter(); p.ParameterName = "@email"; p.Value = email; cmd.Parameters.Add(p);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return false;
                var stored = Convert.ToString(result);
                var ok = string.Equals(stored, code, StringComparison.Ordinal);
                _logger.LogDebug("VerifyRecoveryCodeAsync for {Email}: match={Match}", email, ok);
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar código de recuperação para {Email}", email);
                return false;
            }
        }

        // Atualiza senha (recebendo o hash já calculado) e limpa o código de recuperação
        public async Task<bool> UpdatePasswordWithHashAsync(string email, string newHash)
        {
            _logger.LogInformation("UpdatePasswordWithHashAsync called for {Email}", email);
            try
            {
                await using var conn = _factory.CreateConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Usuarios SET senha = @hash, recuperacaoSenha = NULL WHERE email = @email";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@hash"; p1.Value = newHash; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@email"; p2.Value = email; cmd.Parameters.Add(p2);

                var rows = await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("UpdatePasswordWithHashAsync atualizou {Rows} linhas para {Email}", rows, email);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar senha para {Email}", email);
                return false;
            }
        }

        // Remove credenciais da connection string para logs (simplificado)
        private static string MaskConnectionString(string cs)
        {
            if (string.IsNullOrEmpty(cs)) return cs;
            var lowered = cs.ToLowerInvariant();
            if (lowered.Contains("password="))
            {
                var parts = cs.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().StartsWith("password="))
                    {
                        parts[i] = "Password=****";
                    }
                }
                return string.Join(";", parts);
            }
            return cs;
        }
    }
}
