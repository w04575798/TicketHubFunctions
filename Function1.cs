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
            _logger.LogInformation($"C# Hello: {ticket.Name}!");

            // Get connection string from app settings
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("SQL connection string is not set in the environment variables.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(); // Note the ASYNC

                var query = "INSERT INTO Tickets (ConcertId, Email, Name, Phone, Quantity, CreditCard, Expiration, SecurityCode, Address, City, Province, PostalCode, Country) " +
                            "VALUES (@ConcertId, @Email, @Name, @Phone, @Quantity, @CreditCard, @Expiration, @SecurityCode, @Address, @City, @Province, @PostalCode, @Country)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
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

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
