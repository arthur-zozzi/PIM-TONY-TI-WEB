// Services/SmtpEmailSender.cs
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TonyTI_Web.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _from;

        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
        {
            _config = config;
            _logger = logger;

            _host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host não configurado");
            _user = _config["Smtp:User"] ?? throw new InvalidOperationException("Smtp:User não configurado");
            _pass = _config["Smtp:Pass"] ?? throw new InvalidOperationException("Smtp:Pass não configurado");
            _from = _config["Smtp:From"] ?? _user;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("destinatário inválido", nameof(to));
            if (string.IsNullOrWhiteSpace(subject)) subject = "(sem assunto)";
            body ??= string.Empty;

            // Construir a mensagem (não logar o corpo)
            using var msg = new MailMessage();
            msg.From = new MailAddress(_from);
            msg.To.Add(new MailAddress(to));
            msg.Subject = subject;
            msg.Body = body;
            msg.IsBodyHtml = false;

            // Tentativas com configurações diferentes (587 STARTTLS e 465 SSL)
            var attempts = new[]
            {
                new { Port = 587, EnableSsl = true, UseStartTls = true, Desc = "PORT 587 (STARTTLS)" },
                new { Port = 465, EnableSsl = true, UseStartTls = false, Desc = "PORT 465 (SSL)" }
            };

            Exception lastEx = null;
            foreach (var attempt in attempts)
            {
                try
                {
                    _logger.LogInformation("Tentando envio de e-mail: {Desc} -> Host={Host}", attempt.Desc, _host);

                    using var client = new SmtpClient(_host, attempt.Port)
                    {
                        EnableSsl = attempt.EnableSsl,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(_user, _pass),
                        Timeout = 15000
                    };

                    // Nota: SmtpClient no .NET usa STARTTLS automaticamente em 587 quando EnableSsl=true e o servidor suportar.
                    await client.SendMailAsync(msg);
                    _logger.LogInformation("E-mail enviado com sucesso para {To} via {Desc}", to, attempt.Desc);
                    return;
                }
                catch (SmtpException smtpEx)
                {
                    lastEx = smtpEx;
                    _logger.LogWarning(smtpEx, "Falha SMTP usando {Desc}: StatusCode={StatusCode} Response={Response}", attempt.Desc, smtpEx.StatusCode, smtpEx.Message);
                    // tentar próxima configuração
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    _logger.LogWarning(ex, "Erro ao enviar usando {Desc}: {Message}", attempt.Desc, ex.Message);
                }
            }

            // se chegou aqui, todas as tentativas falharam
            _logger.LogError(lastEx, "Todas as tentativas de envio de e-mail falharam para {To}. Verifique credenciais e acesso de rede.", to);

            // relançar a exceção para o controller mostrar/persistir o erro
            throw new InvalidOperationException("Falha no envio de e-mail SMTP. Verifique logs para detalhes.", lastEx);
        }
    }
}
