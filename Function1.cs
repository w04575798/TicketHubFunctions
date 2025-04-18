using System;
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
            [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] BinaryData rawData,
            FunctionContext context)
        {
            var log = context.GetLogger<Function1>();

            string rawMessage = rawData.ToString();
            log.LogWarning($"RAW queue message: {rawMessage}");

            Ticket? ticket;
            try
            {
                ticket = rawData.ToObjectFromJson<Ticket>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Deserialization failed: {ex.Message}");
                return;
            }

            if (ticket == null)
            {
                log.LogError("Deserialized ticket is null.");
                return;
            }

            log.LogInformation($"Ticket for {ticket.Name}, Email: {ticket.Email}");

            string? connStr = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                log.LogError("Missing SQL connection string in environment variables.");
                throw new InvalidOperationException("Missing SqlConnectionString.");
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                log.LogInformation("Connected to SQL.");

                string query = @"
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
                log.LogInformation("Ticket inserted into DB.");
            }
            catch (Exception ex)
            {
                log.LogError($"SQL insert failed: {ex.Message}");
                throw;
            }
        }
    }
}
