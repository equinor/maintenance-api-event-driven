import { ClientSecretCredential } from "@azure/identity";
import {
  LogsQueryClient,
  MetricsQueryClient,
  Durations,
} from "@azure/monitor-query";
import "dotenv/config";

const tenantId = process.env.TENANT_ID;
const clientId = process.env.CLIENT_ID;
const clientSecret = process.env.CLIENT_SECRET;

const subscriptionId = process.env.SUBSCRIPTION_ID;
const rgName = process.env.RESOURCE_GROUP_NAME;
const provider = process.env.PROVIDER;
const namespace = process.env.NAMESPACE;
const resourceName = process.env.RESOURCE_NAME;

const credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

const metricsQueryClient = new MetricsQueryClient(credential);

const logAnalyticsWorkspaceId = process.env.LAW_ID;

const metricsResourceId = `subscriptions/${subscriptionId}/resourceGroups/${rgName}/providers/${provider}/namespaces/${namespace}`

const runLogsQueryClient = async () => {
  let metricsResult = await getMetricsForQueue(metricsResourceId,"all-maintenance-events"); 
  metricsResult = await getMetricsForQueue(metricsResourceId,"flow-maintenance-events");
  metricsResult = await getMetricsForQueue(metricsResourceId,"maintenance-events");
  //const result = await getMetricDefinitions();
  return 1;
};


//Get the important metrics for a single queue/topic
const getMetricsForQueue = async(metricsResourceId, queueName) => {
  const metricsResponse = await metricsQueryClient.queryResource(
    metricsResourceId,
    ["IncomingMessages","ActiveMessages", "CompleteMessage","DeadletteredMessages"],
    {
      granularity: "P1D",
      timespan: { duration: Durations.fiveMinutes },
      filter: `EntityName eq '${queueName}'`
    }
  );
  const metrics = metricsResponse.metrics;
  console.log(`Metrics:`, JSON.stringify(metrics, undefined, 2));
  return metrics;    
}

//List out all the metric 
const getMetricsDefinition = async() => {
  const metricsIterator = metricsQueryClient.listMetricDefinitions(metricsResourceId);

  let metricDefinition = await metricsIterator.next();
  let metricNames = [];
  for await (metricDefinition of metricsIterator) {
    if(metricDefinition.name){
      console.log(metricDefinition);
      metricNames.push(metricDefinition.name);
    }
  }
  return metricNames;
}

runLogsQueryClient()
  .then((r) => console.log(JSON.stringify(r)))
  .catch((error) => console.error(error));



/* Metric names for ServiceBus
ServerErrors
UserErrors
ThrottledRequests
IncomingRequests
IncomingMessages
OutgoingMessages
ActiveConnections
ConnectionsOpened
ConnectionsClosed
Size
ActiveMessages
Messages
DeadletteredMessages
ScheduledMessages
CompleteMessage
AbandonMessage
NamespaceCpuUsage
NamespaceMemoryUsage
PendingCheckpointOperationCount
ServerSendLatency
CPUXNS
WSXNS
*/
/* Example metric definition
{
  isDimensionRequired: false,
  resourceId: '/subscriptions/ed495c55-b1f3-4532-964b-ae4aa4ae2b1d/resourceGroups/s390-mapi-event-qa/providers/Microsoft.ServiceBus/namespaces/maintenance-events-qa',
  namespace: 'Microsoft.ServiceBus/namespaces',
  unit: 'Count',
  primaryAggregationType: 'Total',
  supportedAggregationTypes: [ 'None', 'Average', 'Minimum', 'Maximum', 'Total', 'Count' ],
  id: '/subscriptions/ed495c55-b1f3-4532-964b-ae4aa4ae2b1d/resourceGroups/s390-mapi-event-qa/providers/Microsoft.ServiceBus/namespaces/maintenance-events-qa/providers/microsoft.insights/metricdefinitions/IncomingMessages',
  description: 'Incoming Messages for Microsoft.ServiceBus.',
  name: 'IncomingMessages',
  metricAvailabilities: [
    { granularity: 'PT1M', retention: 'P93D' },
    { granularity: 'PT5M', retention: 'P93D' },
    { granularity: 'PT15M', retention: 'P93D' },
    { granularity: 'PT30M', retention: 'P93D' },
    { granularity: 'PT1H', retention: 'P93D' },
    { granularity: 'PT6H', retention: 'P93D' },
    { granularity: 'PT12H', retention: 'P93D' },
    { granularity: 'P1D', retention: 'P93D' }
  ],
  dimensions: [ 'EntityName' ]
}
*/