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
            [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] string rawMessage,
            FunctionContext context)

        {
            var log = context.GetLogger<Function1>();

            // 1) Log the raw incoming payload (just in case we need to debug)
            log.LogWarning($"RAW queue message: {rawMessage}");

            // If the message is Base64-encoded, we need to decode it:
            byte[] messageBytes = Convert.FromBase64String(rawMessage);
            string decodedMessage = Encoding.UTF8.GetString(messageBytes);

            // 2) Attempt deserialization
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            Ticket? ticket;
            try
            {
                ticket = JsonSerializer.Deserialize<Ticket>(decodedMessage, options);
            }
            catch (Exception ex)
            {
                log.LogError($"Deserialization exception: {ex.Message}");
                throw; // Let it retry or move to poison queue
            }

            if (ticket == null)
            {
                log.LogError("Ticket is null after deserialization—check field names!");
                return;
            }

            log.LogInformation($"Deserialized ticket for {ticket.Name}, Email: {ticket.Email}");

            // 3) Database insert
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
                throw; // Let it retry or eventually move to poison queue
            }
        }
    }
}
