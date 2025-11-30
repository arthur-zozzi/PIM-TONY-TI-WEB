using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TonyTI_Web.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            // Leitura das configurações de SMTP
            var host = _config["Smtp:Host"]!;
            var port = int.Parse(_config["Smtp:Port"]!);
            var user = _config["Smtp:User"]!;
            var pass = _config["Smtp:Pass"]!;
            var from = _config["Smtp:From"]!;

            // Monta a estrutura básica do e-mail
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            try
            {
                // Conexão e autenticação no servidor SMTP
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(user, pass);

                // Envio do e-mail
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Registro de erro para diagnóstico
                _logger.LogError(ex, "Falha ao enviar e-mail para {Email}", to);
                throw;
            }
        }
    }
}
