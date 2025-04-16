using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TicketHubFunction
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Function1))]
        public async Task Run([QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
            string json = message.MessageText;

            // Deserialize the message JSON into a Ticket object
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            Ticket? ticket = JsonSerializer.Deserialize<Ticket>(json, options);

            if (ticket == null)
            {
                _logger.LogError("Ticket is null");
                return;
            }
            _logger.LogInformation($"Deserialized ticket for {ticket.Name}, Email: {ticket.Email}");

            // Get connection string from app settings
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("SQL connection string is missing from environment variables.");
                throw new InvalidOperationException("SQL connection string is not set in the environment variables.");
            }
            _logger.LogInformation("SQL connection string retrieved from environment variables.");

            try
            {
                // Opening SQL connection
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    _logger.LogInformation("Opening SQL connection...");
                    await conn.OpenAsync();
                    _logger.LogInformation("SQL connection opened successfully.");

                    var query = "INSERT INTO Tickets (ConcertId, Email, Name, Phone, Quantity, CreditCard, Expiration, SecurityCode, Address, City, Province, PostalCode, Country) " +
                                "VALUES (@ConcertId, @Email, @Name, @Phone, @Quantity, @CreditCard, @Expiration, @SecurityCode, @Address, @City, @Province, @PostalCode, @Country)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        _logger.LogInformation("Preparing to insert ticket into database...");

                        // Adding parameters to prevent SQL injection
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

                        // Executing the SQL command
                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("Ticket inserted successfully into the database.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SQL error: {ex.Message}");
            }
        }
    }
}
