using NServiceBus;
using NServiceBus.Transport.SQLServer;
using System;
using System.Threading.Tasks;

public static class Program
{
    static Random random;

    public static async Task Main()
    {
        random = new Random();

        Console.Title = "Samples.Sql.Sender";
        var endpointConfiguration = new EndpointConfiguration("Samples.Sql.Sender");
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.SendHeartbeatTo(
            serviceControlQueue: "Particular.ServiceControl",
            frequency: TimeSpan.FromSeconds(15),
            timeToLive: TimeSpan.FromSeconds(30));
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        var metricsOptions = endpointConfiguration.EnableMetrics();
        metricsOptions.SendMetricDataToServiceControl("Particular.Monitoring",
            TimeSpan.FromSeconds(10));
        #region SenderConfiguration

        var connection = @"Data Source=(local);Database=NServiceBus;Integrated Security=True;Max Pool Size=100";
        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connection);
        transport.DefaultSchema("sender");
        transport.UseSchemaForQueue("error", "dbo");
        transport.UseSchemaForQueue("audit", "dbo");
        transport.UseSchemaForQueue("Particular.ServiceControl", "dbo");
        transport.UseSchemaForQueue("Particular.Monitoring", "dbo");
        transport.UseNativeDelayedDelivery().DisableTimeoutManagerCompatibility();

        endpointConfiguration.UsePersistence<InMemoryPersistence>();

        #endregion

        SqlHelper.CreateSchema(connection, "sender");

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press enter to send a message");
        Console.WriteLine("Press any key to exit");

        while (true)
        {
            var key = Console.ReadKey();
            Console.WriteLine();

            if (key.Key != ConsoleKey.Enter)
            {
                break;
            }

            var orderSubmitted = new OrderSubmitted
            {
                OrderId = Guid.NewGuid(),
                Value = random.Next(100)
            };
            await endpointInstance.Publish(orderSubmitted)
                .ConfigureAwait(false);
            Console.WriteLine("Published OrderSubmitted message");
        }
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}