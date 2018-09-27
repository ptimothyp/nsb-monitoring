using NServiceBus;
using NServiceBus.Persistence.Sql;
using NServiceBus.Transport.SQLServer;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;

public static class Program
{
    static async Task Main()
    {
        Console.Title = "Samples.Sql.Receiver";

        #region ReceiverConfiguration

        var endpointConfiguration = new EndpointConfiguration("Samples.Sql.Receiver");
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.SendHeartbeatTo(
            serviceControlQueue: "Particular.ServiceControl",
            frequency: TimeSpan.FromSeconds(15),
            timeToLive: TimeSpan.FromSeconds(30));
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        var metricsOptions = endpointConfiguration.EnableMetrics();
        metricsOptions.SendMetricDataToServiceControl("Particular.Monitoring",
            TimeSpan.FromSeconds(10));
        var connection = @"Data Source=(local);Database=NServiceBus;Integrated Security=True;Max Pool Size=100";


        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connection);
        transport.DefaultSchema("receiver");
        transport.UseSchemaForQueue("error", "dbo");
        transport.UseSchemaForQueue("audit", "dbo");
        transport.UseSchemaForQueue("Particular.ServiceControl", "dbo");
        transport.UseSchemaForQueue("Particular.Monitoring", "dbo");
        transport.UseSchemaForQueue("Samples.Sql.Sender", "sender");
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        transport.UseNativeDelayedDelivery().DisableTimeoutManagerCompatibility();

        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(OrderAccepted), "Samples.Sql.Sender");
        routing.RegisterPublisher(typeof(OrderSubmitted).Assembly, "Samples.Sql.Sender");

        var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
        var dialect = persistence.SqlDialect<SqlDialect.MsSqlServer>();
        dialect.Schema("receiver");
        persistence.ConnectionBuilder(
            connectionBuilder: () =>
            {
                return new SqlConnection(connection);
            });
        persistence.TablePrefix("");
        var subscriptions = persistence.SubscriptionSettings();
        subscriptions.CacheFor(TimeSpan.FromMinutes(1));

        #endregion

        SqlHelper.CreateSchema(connection, "receiver");
        var allText = File.ReadAllText("Startup.sql");
        SqlHelper.ExecuteSql(connection, allText);
        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}