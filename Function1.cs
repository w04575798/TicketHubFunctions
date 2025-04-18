using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TicketHubFunction
{
    public class Function1
    {
        [Function(nameof(Function1))]
        public async Task Run(
            [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] string base64Message,
            FunctionContext context)
        {
            var log = context.GetLogger<Function1>();

            // 1) Log raw incoming Base64 payload
            log.LogWarning($"RAW queue message (Base64): {base64Message}");

            // 2) Decode the Base64 message to UTF-8 string
            string rawMessage;
            try
            {
                var messageBytes = Convert.FromBase64String(base64Message);
                rawMessage = Encoding.UTF8.GetString(messageBytes);
            }
            catch (FormatException ex)
            {
                log.LogError($"Base64 decoding failed: {ex.Message}");
                throw;
            }

            log.LogInformation($"Decoded raw message: {rawMessage}");

            // 3) Attempt to deserialize the decoded message
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            Ticket? ticket;
            try
            {
                ticket = JsonSerializer.Deserialize<Ticket>(rawMessage, options);
            }
            catch (Exception ex)
            {
                log.LogError($"Deserialization exception: {ex.Message}");
                throw;
            }

            if (ticket == null)
            {
                log.LogError("Ticket is null after deserialization — check field names!");
                return;
            }

            log.LogInformation($"Deserialized ticket for {ticket.Name}, Email: {ticket.Email}");

            // 4) Database insert
            string? connStr = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connStr))
            {
                log.LogError("SQL connection string missing!");
                throw new InvalidOperationException("No SqlConnectionString in env vars");
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                log.LogInformation("SQL connection opened.");

                var query = @"
                    INSERT INTO Tickets
                      (ConcertId, Email, Name, Phone, Quantity,
                       CreditCard, Expiration, SecurityCode,
                       Address, City, Province, PostalCode, Country)
                    VALUES
                      (@ConcertId, @Email, @Name, @Phone, @Quantity,
                       @CreditCard, @Expiration, @SecurityCode,
                       @Address, @City, @Province, @PostalCode, @Country)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ConcertId", ticket.ConcertId);
                cmd.Parameters.AddWithValue("@Email", ticket.Email);
                cmd.Parameters.AddWithValue("@Name", ticket.Name);
                cmd.Parameters.AddWithValue("@Phone", ticket.Phone);
                cmd.Parameters.AddWithValue("@Quantity", ticket.Quantity);
                cmd.Parameters.AddWithValue("@CreditCard", ticket.CreditCard);
                cmd.Parameters.AddWithValue("@Expiration", ticket.Expiration);
                cmd.Parameters.AddWithValue("@SecurityCode", ticket.SecurityCode);
                cmd.Parameters.AddWithValue("@Address", ticket.Address);
                cmd.Parameters.AddWithValue("@City", ticket.City);
                cmd.Parameters.AddWithValue("@Province", ticket.Province);
                cmd.Parameters.AddWithValue("@PostalCode", ticket.PostalCode);
                cmd.Parameters.AddWithValue("@Country", ticket.Country);

                await cmd.ExecuteNonQueryAsync();
                log.LogInformation("Ticket inserted successfully.");
            }
            catch (Exception ex)
            {
                log.LogError($"SQL error: {ex.Message}");
                throw;
            }
        }
    }
}
