using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TicketHubFunction
{
    [Function(nameof(Function1))]
    public async Task Run(
    [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] string rawMessage,
    FunctionContext context)
    {
        var log = context.GetLogger<Function1>();

        // Log the raw incoming payload (base64 encoded)
        log.LogWarning($"RAW queue message (Base64): {rawMessage}");

        // Decode the base64 message
        byte[] decodedBytes = Convert.FromBase64String(rawMessage);
        string decodedMessage = Encoding.UTF8.GetString(decodedBytes);

        // Log the decoded message
        log.LogWarning($"Decoded message: {decodedMessage}");

        // Attempt to deserialize the JSON message
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        Ticket? ticket;
        try
        {
            ticket = JsonSerializer.Deserialize<Ticket>(decodedMessage, options);
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

        // Database insert logic
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
