# Chatlog wplace husichat (filtered: ticket node system)

````text
[07:45, 23/10/2026] Neow: DELETED
[07:45, 23/10/2026] Neow: We should decide the ticketing
[07:46, 23/10/2026] Neow: And also kinda decide on the architecture as a whole
[07:52, 23/10/2026] Neow: So the way I understand it is that fancy ahh Slack looking client creates a ticket, sends it to a router, the router either is preprogrammed or uses Jev to find out what agent container suits the ticket best, then the ticket gets routed to there
[07:52, 23/10/2026] Neow: How do they all access resources? Does the ticket include the permissions
[07:52, 23/10/2026] Alex Alternator: So the way I understand it is that fancy ahh Slack looking client creates a ticket, sends it to a router, the router either is preprogrammed or uses Jev to find out what agent container suits the ticket best, then the ticket gets routed to there
That or you just assign it manually
[07:53, 23/10/2026] Alex Alternator: The router also handles auth and DELETED bc it's the only one exposed to the public internet
[07:56, 23/10/2026] Neow: if you tell it to code smt, I assume it has full permissions within its containerised venv, so it creates the files and registers them in the project? So the container replies to the client/router that it made file A and where file A is? And the next agent whose ticket involves the same project already has the files loaded into context? (Their metadata)
[07:57, 23/10/2026] Alex Alternator: Basically the agent just spins up a branch on git
[07:58, 23/10/2026] Alex Alternator: Also permissions are managed by the node itself
[07:58, 23/10/2026] Alex Alternator: So if you don't want the node running shell commands that's the job of the node
[07:59, 23/10/2026] Neow: Yeah I assume
[07:59, 23/10/2026] Neow: then we get nice rollback options aswell
[07:59, 23/10/2026] Neow: especialy since a coder would have a local repo
[07:59, 23/10/2026] Neow: We cant upload all types of files to git
[07:59, 23/10/2026] Neow: Do we care?
If we want a video gen container for example
[07:59, 23/10/2026] Alex Alternator: We cant upload all types of files to git
Why can't you upload an mp4?
[08:00, 23/10/2026] Neow: I mean you can but not a normally sized one
[08:00, 23/10/2026] Neow: unless we chunk it ig
[08:00, 23/10/2026] Neow: size limit
[08:00, 23/10/2026] Alex Alternator: Why?
[08:00, 23/10/2026] Alex Alternator: Yeah
[08:00, 23/10/2026] Alex Alternator: Fair
[08:00, 23/10/2026] Alex Alternator: But like honestly that seems like the most solvable problem ever
[08:01, 23/10/2026] Neow: Im not scared we dont find a solution
[08:01, 23/10/2026] Neow: Maybe we just make "Vit"
[08:01, 23/10/2026] Neow: Git for videos
[08:01, 23/10/2026] Alex Alternator: I had so little problems with uploading big files to git
[08:02, 23/10/2026] Alex Alternator: That I'm sure the agents will not have that problem too
[08:02, 23/10/2026] Neow: It was a consideration ig
[08:04, 23/10/2026] Neow: I still dont have the clearest picture of the system
For example ClankWorks, it needs up to several coding agents (for paralellism mainly), a reviewer and perhaps a researcher
All those are orchestrated per default. Does that mean the system stores the existence of BOTH nodes and meshes, in the sense that you can send a ticket to a node, but can also directly spin up a mesh like CW
[08:04, 23/10/2026] Neow: and then those have their own internal routing?
[08:05, 23/10/2026] Alex Alternator: What happens inside the node idc
[08:05, 23/10/2026] Neow: I mean is CW one node or multiple stringed together
[08:05, 23/10/2026] Alex Alternator: Anything can happen inside the node as long as it completes the ticket
[08:05, 23/10/2026] Alex Alternator: A node is just something that has the specific http endpoints needed to complete a ticket
[08:06, 23/10/2026] Neow: ok
then it truly doesnt matter ig
[08:06, 23/10/2026] Alex Alternator: Inside there can be 500 agents
Or one
Or it can be Dave sitting next to the printer
[08:06, 23/10/2026] Neow: DELETED
looks inside
[08:07, 23/10/2026] Neow: DELETED
[08:07, 23/10/2026] Neow: ok what more must be decide?
[08:07, 23/10/2026] Alex Alternator: How a ticket looks like
[08:08, 23/10/2026] Alex Alternator: What http endpoints
[08:08, 23/10/2026] Neow: Wanna ask chat, we look over it and go "yada yada this" and tell chat
[08:08, 23/10/2026] Alex Alternator: Or we use brain rq to finish the architecture
[08:08, 23/10/2026] Neow: ok
[08:08, 23/10/2026] Neow: What is the system callled?
[08:08, 23/10/2026] Alex Alternator: Idk
[08:09, 23/10/2026] Neow: Unified Services Kit
idk
unisk
[08:09, 23/10/2026] Neow: UniSK
[08:10, 23/10/2026] Neow: /unisk/node_id/endpoint_purpose/possible query stuff
[08:10, 23/10/2026] Alex Alternator: Well it will have to do a whole lot of stuff
[08:11, 23/10/2026] Neow: thats what the endpoint purpose is for
[08:11, 23/10/2026] Alex Alternator: First of all the router will need endpoints for auth, listing all available nodes, getting the status on a node
Assigning a ticket
[08:11, 23/10/2026] Neow: we need an internal sys node I assume
[08:11, 23/10/2026] Alex Alternator: Who manages the node
Whoever manages the system
[08:12, 23/10/2026] Alex Alternator: Nodes are self hosted
[08:12, 23/10/2026] Neow: it needs some exposed enpoints
the internal one would also be a whole ahh program
[08:12, 23/10/2026] Neow: Whoever manages the system
isnt the sytem just docker
[08:13, 23/10/2026] Alex Alternator: Basically the only exposed thing is the router
[08:13, 23/10/2026] Alex Alternator: The rest is shielded behind the lan
[08:13, 23/10/2026] Neow: Srry I mean it needs to expose it to the router
[08:13, 23/10/2026] Neow: we can scam the internal routing system from docker if all nodes are self hosted
[08:14, 23/10/2026] Alex Alternator: Which internal routing?
[08:14, 23/10/2026] Neow: node name routing
[08:15, 23/10/2026] Neow: but whatever IMO we should keep regular IP bc what if you want one node to run in AWS and the rest on the IntelNuc
[08:15, 23/10/2026] Alex Alternator: They just have to be in the same lan
[08:15, 23/10/2026] Neow: Now lets lock in and get a V1 committed bc other we get le DELETED
[08:15, 23/10/2026] Alex Alternator: DELETED
[08:18, 23/10/2026] Neow: They just have to be in the same lan
so basically i can be running a bunch of nodes on my laptop and you can access them over BBB wifi?
[08:19, 23/10/2026] Alex Alternator: I mean not bbb wifi bc bbb wifi is goofy
[08:19, 23/10/2026] Alex Alternator: But any other wifi would work
[08:19, 23/10/2026] Neow: ngl on the hackathon we ought to bring our own mini network
[08:19, 23/10/2026] Neow: some cables and a switch and ap
[08:20, 23/10/2026] Neow: DELETED
[08:20, 23/10/2026] Neow: DELETED
[08:20, 23/10/2026] Alex Alternator: Tho the client doesn't have to be on the network
[08:20, 23/10/2026] Alex Alternator: The client just needs to be plugged into a router
[08:21, 23/10/2026] Alex Alternator: Or many routers
[08:26, 23/10/2026] Alex Alternator: ok so next up i'd say we'll have to define the exact http requests between the 3 elements
[08:27, 23/10/2026] Alex Alternator: We have the client, router, and node
[08:27, 23/10/2026] Alex Alternator: DELETED
[08:29, 23/10/2026] Alex Alternator: DELETED
[08:31, 23/10/2026] Alex Alternator: DELETED
[08:31, 23/10/2026] Alex Alternator: DELETED
[08:32, 23/10/2026] Neow: DELETED
[08:41, 23/10/2026] Neow: DELETED
[08:41, 23/10/2026] Alex Alternator: today we'll have to define the API
decide on the tech stack
and set up everyones folder on github
[08:42, 23/10/2026] Alex Alternator: so today we'll mainly present the tech stack and argue that a good architecture is half the job
[08:42, 23/10/2026] Neow: btw bzw GitHub, is this all a bunch of branches on a repo or is there like a GH projects where you can have bundled repos
[08:42, 23/10/2026] Alex Alternator: for us
or for the tool?
[08:42, 23/10/2026] Neow: For the tool? but we are te tool
[08:43, 23/10/2026] Alex Alternator: btw bzw GitHub, is this all a bunch of branches on a repo or is there like a GH projects where you can have bundled repos
i would say for now we just link one github repo
and every node creates a branch
[08:43, 23/10/2026] Neow: fairs
[08:43, 23/10/2026] Alex Alternator: we can expand this later if needed
[08:43, 23/10/2026] Neow: yeah
we can force a chatter to migrate it all LOL
thats what theyre the best for
[08:44, 23/10/2026] Alex Alternator: we use the node to upgrade the node
[08:44, 23/10/2026] Neow: DELETED
[08:44, 23/10/2026] Alex Alternator: DELETED
[08:44, 23/10/2026] Neow: DELETED
[08:44, 23/10/2026] Alex Alternator: DELETED
[08:44, 23/10/2026] Neow: DELETED
[08:45, 23/10/2026] Alex Alternator: tho he would have to set up the frontend project today
[08:46, 23/10/2026] Neow: DELETED
[08:46, 23/10/2026] Alex Alternator: DELETED
[08:46, 23/10/2026] Alex Alternator: now we'll have to do the interaction stuff
[08:46, 23/10/2026] Neow: DELETED
[08:47, 23/10/2026] Alex Alternator: ok so how do the node and router interact with each other
[08:47, 23/10/2026] Neow: does the router accept a call, and then in code generate a new call to a container?
[08:48, 23/10/2026] Alex Alternator: the router is mainly a proxy
[08:48, 23/10/2026] Neow: or does it also decide wether to let the call traverse
[08:48, 23/10/2026] Alex Alternator: the router isn't smart
he just checks if you're logged into the account properly and if so if you have access to these nodes
and then it just lets anything through
[08:49, 23/10/2026] Neow: when we come up with a new node idea, we just register it with the router via a post
and then it just lets anything through
yeah thats what I meant to ask srry
[08:49, 23/10/2026] Alex Alternator: yeah basaically
do you want to do call?
[08:49, 23/10/2026] Neow: DELETED
[08:50, 23/10/2026] Neow: DELETED
[08:50, 23/10/2026] Alex Alternator: ok so when a new node is spun up i think it will have to contact the router to let it know it exists
[08:51, 23/10/2026] Neow: to create a node we can register it in any network since the router is exposed to the big wide web but for the actual node code etc can we also install that on whatever network the whole system is running via remote, or MUST node creation be done locally?
[08:51, 23/10/2026] Alex Alternator: the node must be in the same network as the router
but the client can be anywhere
[08:52, 23/10/2026] Neow: I guess for a container we can just upload it to the docker reg from anywhere and tell the system via remote that it needs to pull X container
[08:52, 23/10/2026] Alex Alternator: sure ig
but like why
[08:52, 23/10/2026] Neow: Then it comes to the same question of why is the client outside the network
[08:53, 23/10/2026] Neow: if you need work done and the container for it doesnt exist
[08:53, 23/10/2026] Alex Alternator: that's like a whole seperate system
[08:53, 23/10/2026] Neow: we can ignore it for now
[08:53, 23/10/2026] Alex Alternator: the idea is that you can like host the containers on AWS or something
where they can get max resources they need
and your client only connects to it
[08:54, 23/10/2026] Alex Alternator: you can just see what nodes you have available and use them
[08:54, 23/10/2026] Alex Alternator: if you want more just ask your sysadmin
[08:55, 23/10/2026] Neow: and that will likely be you in hackathon scenario
[08:55, 23/10/2026] Neow: DELETED
[08:56, 23/10/2026] Neow: ok so how do the node and router interact with each other
Im just going to fire away here

I guess we have a few main things:
Tasks
Reports
results
[08:57, 23/10/2026] Alex Alternator: wait i'm working on the node2server registrations now
[08:57, 23/10/2026] Neow: the router gives the node tasks, but wait: for a job that requires multiple disciplines, must we then have a rigid node that includes all the things or can a node do a part of the work and another continues
[08:58, 23/10/2026] Alex Alternator: what?
a job is only one discipline
that's why jobs exist
[08:58, 23/10/2026] Neow: who splits it, the client itself must know?
[08:58, 23/10/2026] Alex Alternator: yup
[08:59, 23/10/2026] Alex Alternator: the client runs a kind of coordinator superagent
[08:59, 23/10/2026] Neow: mmmm thats like the antithesis of vibe coding
I was gonna say
indeed we need that agent
[08:59, 23/10/2026] Alex Alternator: yup
later we can expand on that
[08:59, 23/10/2026] Vincent Le Vries: DELETED
[09:00, 23/10/2026] Vincent Le Vries: DELETED
[09:00, 23/10/2026] Neow: DELETED
[09:00, 23/10/2026] Alex Alternator: later we can expand on that
but for mvp it just makes the most sense to just have a minimal superagent who larps as a scrum master or idk
[09:00, 23/10/2026] Alex Alternator: ```json
{
  "name" : "my-node-12"
  "host" : "192.168.0.25:5556"
}
```
[09:00, 23/10/2026] Vincent Le Vries: DELETED
[09:00, 23/10/2026] Vincent Le Vries: DELETED
[09:01, 23/10/2026] Alex Alternator: ```json
{
  "name" : "my-node-12"
  "host" : "192.168.0.25:5556"
}
```
ok so currently we have this for registration
[09:01, 23/10/2026] Neow: unique names
[09:01, 23/10/2026] Alex Alternator: already there
[09:01, 23/10/2026] Neow: yes
[09:01, 23/10/2026] Alex Alternator: under name
but maybe an ID?
[09:01, 23/10/2026] Neow: thats why I said it
but honestly unique names are prob fine
[09:01, 23/10/2026] Alex Alternator: smart
[09:01, 23/10/2026] Alex Alternator: nah id is better bc it's unique
[09:02, 23/10/2026] Alex Alternator: name can get DELETED up really easily especially across multiple networks
[09:02, 23/10/2026] Neow: sure ok
I dont see quite how but an ID is always unique anyways
[09:02, 23/10/2026] Alex Alternator: i'm also thinking maybe a type (so like coder, researcher, etc.)
[09:02, 23/10/2026] Neow: yes
designation
[09:02, 23/10/2026] Neow: is it free or pre decided of a list which ones there are?
[09:03, 23/10/2026] Alex Alternator: i'd say we use it like a docker image tag
[09:03, 23/10/2026] Neow: because technically the client can just let the superagent go "what the HECK is a flux capacitor"
[09:03, 23/10/2026] Vincent Le Vries: DELETED
[09:03, 23/10/2026] Neow: DELETED
[09:03, 23/10/2026] Alex Alternator: so basically saying "hey i'm keinstein:coderagent-1.1"
[09:03, 23/10/2026] Vincent Le Vries: DELETED
[09:03, 23/10/2026] Alex Alternator: What can I do now?
create a folder in the repo
git clone the entire thing
checkout to a new branch called frontend setup
initialize the frontend project (choose a nice framework like svelte)
[09:03, 23/10/2026] Neow: ah stimmt LOL
[09:04, 23/10/2026] Neow: are we going to keep an accesslist for nodes that specific clients made?
what if guy A doesnt want guy B to use his anime girl image generator
[09:04, 23/10/2026] Alex Alternator: don't put it in the DELETED network
[09:04, 23/10/2026] Neow: DELETED
[09:05, 23/10/2026] Alex Alternator: aha nvm i see
since auth goes over github either way
[09:05, 23/10/2026] Neow: DELETED
[09:05, 23/10/2026] Alex Alternator: DELETED
[09:06, 23/10/2026] Alex Alternator: but i think most permissions would be managed on the router
[09:07, 23/10/2026] Alex Alternator: and not the node itself
[09:07, 23/10/2026] Neow: definitely
[09:07, 23/10/2026] Alex Alternator: bc either you can have access to all nodes or no node
for mvp at least
[09:07, 23/10/2026] Alex Alternator: ok so registration looks like this now:
[09:07, 23/10/2026] Alex Alternator: register:
POST [router-ip]:2048
```json
{
  "name" : "my-node-12"
  "id" : "alwjehflksja3a342y"
  "host" : "192.168.0.25:5556"
  "type" : "keinstein0:research-agent-1.3"
}
```
[09:08, 23/10/2026] Neow: DELETED
[09:08, 23/10/2026] Alex Alternator: this then just gets stored internally
ez
[09:08, 23/10/2026] Neow: DELETED
[09:08, 23/10/2026] Alex Alternator: DELETED
[09:09, 23/10/2026] Neow: register:
POST [router-ip]:2048
```json
{
  "name" : "my-node-12"
  "id" : "alwjehflksja3a342y"
  "host" : "192.168.0.25:5556"
  "type" : "keinstein0:research-agent-1.3"
}
```
or we can appent a dict to this with userId:permission (enum)
[09:10, 23/10/2026] Alex Alternator: ez
[09:10, 23/10/2026] Neow: what shall the permission levels be?

Lets assume no user id entry means no visibility of the node
[09:10, 23/10/2026] Alex Alternator: yeah
[09:10, 23/10/2026] Neow: smt like
use
modify
full
[09:11, 23/10/2026] Alex Alternator: but that's something the client2router have to manage
[09:11, 23/10/2026] Alex Alternator: also honestly we can just have a whitelist of allowed users
[09:12, 23/10/2026] Alex Alternator: like what in the world is the difference between someone with use and modify abilities
[09:12, 23/10/2026] Neow: the router can just filter the nodes and store into memory and serve the client only that
[09:12, 23/10/2026] Alex Alternator: yeah but the router just shows every node
[09:12, 23/10/2026] Neow: use is just delegating tasks to the node and modify allows you to change its code or whatever and full allows deletion and permission changes etc
[09:13, 23/10/2026] Alex Alternator: you just do that on the host machine directly i'd say
[09:13, 23/10/2026] Neow: yeah but the router just shows every node
every user is authenticated right?
[09:13, 23/10/2026] Alex Alternator: yup
[09:13, 23/10/2026] Neow: so why can the router not know who not to show what node to
[09:13, 23/10/2026] Alex Alternator: why would it be relevant
[09:13, 23/10/2026] Vincent Le Vries: DELETED
[09:13, 23/10/2026] Alex Alternator: why would it be relevant
if you don't want someone to use your node put it behind a different router
[09:13, 23/10/2026] Vincent Le Vries: DELETED
[09:14, 23/10/2026] Neow: DELETED
[09:14, 23/10/2026] Neow: DELETED
[09:14, 23/10/2026] Vincent Le Vries: DELETED
[09:14, 23/10/2026] Neow: DELETED
[09:14, 23/10/2026] Alex Alternator: DELETED
[09:14, 23/10/2026] Neow: DELETED
[09:14, 23/10/2026] Alex Alternator: also we now know the tech stack
[09:15, 23/10/2026] Alex Alternator: DELETED
[09:15, 23/10/2026] Neow: what is the repo link
[09:15, 23/10/2026] Alex Alternator: DELETED
[09:15, 23/10/2026] Alex Alternator: DELETED
[09:15, 23/10/2026] Neow: DELETED
[09:15, 23/10/2026] Alex Alternator: DELETED
[09:15, 23/10/2026] Vincent Le Vries: DELETED
[09:16, 23/10/2026] Alex Alternator: DELETED
[09:16, 23/10/2026] Alex Alternator: https://github.com/Keinstein0/Lernperiode-12
[09:16, 23/10/2026] Vincent Le Vries: DELETED
[09:16, 23/10/2026] Alex Alternator: DELETED
[09:16, 23/10/2026] Vincent Le Vries: DELETED
[09:16, 23/10/2026] Alex Alternator: DELETED
[09:17, 23/10/2026] Alex Alternator: DELETED
[09:17, 23/10/2026] Vincent Le Vries: DELETED
[09:17, 23/10/2026] Alex Alternator: DELETED
[09:17, 23/10/2026] Neow: DELETED
[09:17, 23/10/2026] Neow: what is the repo
[09:17, 23/10/2026] Alex Alternator: https://github.com/Keinstein0/Lernperiode-12
[09:18, 23/10/2026] Alex Alternator: DELETED
[09:18, 23/10/2026] Alex Alternator: DELETED
[09:19, 23/10/2026] Neow: DELETED
[09:19, 23/10/2026] Neow: DELETED
[09:20, 23/10/2026] Neow: DELETED
[09:20, 23/10/2026] Neow: DELETED
[09:20, 23/10/2026] Alex Alternator: DELETED
[09:21, 23/10/2026] Neow: DELETED
[09:22, 23/10/2026] Neow: DELETED
[09:22, 23/10/2026] Alex Alternator: DELETED
[09:43, 23/10/2026] Alex Alternator: DELETED
````
