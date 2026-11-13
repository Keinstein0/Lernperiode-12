# Test Scenarios
- nodes can be registered to allow two way communication (tested using mock node)
- client can query for all nodes
 - node status correct
- client can query for a single node
 - node status correct
 - node does not exist
 - node irresponsive
- post tasks to node
 - get all tasks returns (created) task
 - task assigned to mock node correctly
 - task rejected on nonexisting node
 - task rejected on busy/unavaliable node (note: maybe eventually buffer these?)


