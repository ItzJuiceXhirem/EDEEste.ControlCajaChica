using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    internal sealed class IdentityNoOpEmailSender : IEmailSender, IEmailSender<Usuario>
    {
        private readonly IEmailSender emailSender = new NoOpEmailSender();

        public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
            emailSender.SendEmailAsync(email, subject, htmlMessage);

        public Task SendConfirmationLinkAsync(Usuario user, string email, string confirmationLink) =>
            SendEmailAsync(email, "Confirm email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(Usuario user, string email, string resetLink) =>
            SendEmailAsync(email, "Reset password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        public Task SendPasswordResetCodeAsync(Usuario user, string email, string resetCode) =>
            SendEmailAsync(email, "Reset password code", $"Your reset code is: {resetCode}");
    }
}
