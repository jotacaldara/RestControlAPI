using System.Net;
using System.Net.Mail;


namespace RestControlAPI.Services
{
    public interface IEmailService
    {
        Task SendPaymentReminderAsync(string toEmail, string restaurantName, string planName, decimal amount, DateOnly dueDate);
    }


public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendPaymentReminderAsync(string toEmail, string restaurantName, string planName, decimal amount, DateOnly dueDate)
        {
            // NOTA: Em produção, use SendGrid, Mailgun ou Amazon SES. 
            // Aqui usamos SMTP básico (ex: Gmail) para teste.

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("seu-email@gmail.com", "sua-senha-de-app"),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("financeiro@restflow.com", "RestFlow Financeiro"),
                Subject = $"Pagamento de Subscrição - {restaurantName}",
                Body = $@"
                <h1>Olá, {restaurantName}!</h1>
                <p>A sua subscrição do plano <strong>{planName}</strong> vence em <strong>{dueDate}</strong>.</p>
                <p>Valor a pagar: <strong>{amount.ToString("C2")}</strong></p>
                <p>Por favor, aceda à sua área de cliente para regularizar.</p>
                <br>
                <p>Obrigado,<br>Equipa RestFlow</p>",
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
