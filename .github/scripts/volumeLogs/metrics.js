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

const runLogsQueryClient = async () => {
  const query = `AzureDiagnostics`;
  const result = await metricsQueryClient.listMetricNamespaces();
  return result;
};

// runLogsQueryClient()
//   .then((r) => console.log(JSON.stringify(r)))
//   .catch((error) => console.error(error));

console.log(
  `subscriptions/${subscriptionId}/resourceGroups/${rgName}/providers/${provider}/${namespace}`
);

const metrics = metricsQueryClient.listMetricNamespaces(
  `subscriptions/${subscriptionId}/resourceGroups/${rgName}/providers/${provider}/${namespace}`
);

console.log(await metrics.next());
