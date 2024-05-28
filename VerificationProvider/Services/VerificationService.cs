using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VerificationProvider.Data.Contexts;
using VerificationProvider.Functions;
using VerificationProvider.Models;

namespace VerificationProvider.Services;

public class VerificationService(ILogger<VerificationService> logger, IServiceProvider serviceProvider) : IVerificationService
{
    private readonly ILogger<VerificationService> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;


    public VerificationRequest UnpackVerificationRequest(ServiceBusReceivedMessage message)
    {
        try
        {
            var verificationRequest = JsonConvert.DeserializeObject<VerificationRequest>(message.Body.ToString());
            if (verificationRequest != null && !string.IsNullOrEmpty(verificationRequest.Email))
                return verificationRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.UnpackVerificationRequest() :: {ex.Message}");
        }
        return null!;
    }

    public string GenerateCode()
    {
        try
        {
            var rnd = new Random();
            var code = rnd.Next(100000, 999999);

            return code.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.GenerateCode() :: {ex.Message}");
        }
        return null!;
    }

    public async Task<bool> SaveVerificationRequest(VerificationRequest verificationRequest, string code)
    {
        try
        {
            using var context = _serviceProvider.GetRequiredService<DataContext>();

            var exisitingRequest = await context.VerificationRequests.FirstOrDefaultAsync(x => x.Email == verificationRequest.Email);
            if (exisitingRequest != null)
            {
                exisitingRequest.Code = code;
                exisitingRequest.ExpiryDate = DateTime.Now.AddMinutes(5);
                context.Entry(exisitingRequest).State = EntityState.Modified;
            }
            else
            {
                context.VerificationRequests.Add(new Data.Entities.VerificationRequestEntity() { Email = verificationRequest.Email, Code = code });
            }
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.SaveVerificationRequest() :: {ex.Message}");
        }
        return false;
    }

    public EmailRequest GenerateEmailRequest(VerificationRequest verificationRequest, string code)
    {
        try
        {
            if (!string.IsNullOrEmpty(verificationRequest.Email) && !string.IsNullOrEmpty(code))
            {
                var emailRequest = new EmailRequest()
                {
                    To = verificationRequest.Email,
                    Subject = $"Verification Code {code}",
                    HtmlBody = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <title>Email Verification</title>
                    </head>
                        <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0;'>
                            <div style='max-width: 600px; margin: 50px auto; background-color: #ffffff; padding: 20px; border-radius: 10px; box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);'>
                                <div style='text-align: center; padding-bottom: 20px;'>
                                    <h1 style='color: #333333;'>Email Verification</h1>
                                </div>
                                <div style='text-align: center; font-size: 16px; color: #333333;'>
                                    <p>Hello,</p>
                                    <p>Thank you for registering {verificationRequest.Email}. Please use the verification code below to complete your registration:</p>
                                    <div style='font-size: 24px; font-weight: bold; color: #007BFF; margin: 20px 0;'>{code}</div>
                                    <p>If you did not request this code, please ignore this email.</p>
                                </div>
                                <div style='text-align: center; font-size: 12px; color: #999999; padding-top: 20px;'>
                                    <p>&copy; Silicon. All rights reserved.</p>
                                </div>
                            </div>
                        </body>
                    </html>
                    ",
                    PlainText = $"Please use the verification code to complete your registration: {code}."
                };
                return emailRequest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.GenerateEmailRequest() :: {ex.Message}");
        }
        return null!;
    }

    public string GenerateServiceBusEmailRequest(EmailRequest emailRequest)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(emailRequest);
            if (!string.IsNullOrEmpty(payload))
            {
                return payload;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.GenerateServiceBusEmailRequest() :: {ex.Message}");
        }
        return null!;
    }
}
