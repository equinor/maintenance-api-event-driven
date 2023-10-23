import { ServiceBusAdministrationClient } from "@azure/service-bus";
import "dotenv/config";

const serviceBusAdministrationClient = new ServiceBusAdministrationClient(
  process.env.SERVICE_BUS_CONNECTION_STRING
);

const run = async () => {
  const queues = serviceBusAdministrationClient.listQueues();
  let i = 1;
  let queueItem = await queues.next();
  while (!queueItem.done) {
    console.log(`Queue ${i++}: ${queueItem.value.name}`);
    const queue =
      await serviceBusAdministrationClient.getQueueRuntimeProperties(
        queueItem.value.name
      );

    console.table({
      "Total messages": queue.totalMessageCount,
      "Active messages": queue.activeMessageCount,
      "Transferred messages": queue.transferMessageCount,
      "Scheduled messages": queue.scheduledMessageCount,
      "Transferred dead letter messages": queue.transferDeadLetterMessageCount,
    });

    queueItem = await queues.next();
  }

  const topics = serviceBusAdministrationClient.listTopics();

  i = 1;
  let topicItem = await topics.next();
  while (!topicItem.done) {
    console.log(`Topic ${i++}: ${topicItem.value.name}`);
    const topic =
      await serviceBusAdministrationClient.getTopicRuntimeProperties(
        topicItem.value.name
      );
    console.log(JSON.stringify(topic));

    const subscriptions = serviceBusAdministrationClient.listSubscriptions(
      topic.name
    );

    i = 1;
    let subscriptionItem = await subscriptions.next();
    while (!subscriptionItem.done) {
      console.log(
        `Subscription ${i++}: ${subscriptionItem.value.subscriptionName}`
      );
      const subscription =
        await serviceBusAdministrationClient.getSubscriptionRuntimeProperties(
          topic.name,
          subscriptionItem.value.subscriptionName
        );
      si;
      console.log(JSON.stringify(subscription));
      subscriptionItem = await subscriptions.next();
    }

    topicItem = await topics.next();
  }
};

run().catch((error) => console.error(error));
