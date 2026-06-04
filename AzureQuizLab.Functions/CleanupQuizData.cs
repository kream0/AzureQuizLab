using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AzureQuizLab.Functions;

public class CleanupQuizData
{
    private readonly ILogger<CleanupQuizData> _logger;

    public CleanupQuizData(ILogger<CleanupQuizData> logger)
    {
        _logger = logger;
    }

    [Function("CleanupQuizData")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            var cmd = new SqlCommand("INSERT INTO Logs (Message, LogDate) VALUES (@msg, GETDATE())", conn);

            cmd.Parameters.AddWithValue("@msg", "C# Timer trigger function executed");
            cmd.ExecuteNonQuery();
        }

        _logger.LogInformation("Ecriture en base effectuee");
    }
}
