# Øredev 2022

Agent vs Agent is a multiplayer hypermedia based text-only adventure game inspired by the Spy vs Spy game for the C64. 

The game server is written in F# with Giraffe. You will need .NET installed. The version of .NET I have installed is 5.0. I haven't tried it with other versions.

The game client is written in Node. You will need Node and npm installed, and to do the `npm install` thing.

To start the game server: navigate to the HyperAgents folder and type `dotnet run`.

To start a game client: navigate to the Client folder and type `node run.js`. You can start as many clients as you like, since this is web scale. 

The client and the server communicate over HTTP. Resources are represented using the Siren hypermedia format. The client understands enough of Siren to support navigation based on links and state transfer based on actions. 

To list available links at any given point, use the command `game.links()`. Similarly use `game.actions()` to list actions, and `game.look()` to display any Siren properties intended to tell the story of the game. 

You can follow available links with the `go` command. The list of links is a zero-based array. To choose the second link in the list, use the command `game.go(1)`. 

Similarly, you can perform actions with the `do` command. Actions are named, so commands will look like `game.do("verb-phrase")`. Actions may require you to provide some data as a second parameter. It works much like an HTML form.

In the game client, type `game.hyperagents()` and press enter. This should take you to the starting resource for the game. The starting resource offers an action to register an agent. Since registering an agent is something that causes state to change, it's going to be an action. So you type `game.do("start-agent", { agent : "color" })`. This will register an agent with the specified color with the game. 

Sometimes, like when you register an agent with the game, the game server will respond with a redirect to some URL specified in the HTTP Location header. The client understands enough of HTTP to make use of such headers. Use the `game.follow()` command to follow Location headers. 

That's the gist of it. Traverse the game world using the links offered, be on the lookout for the secret file and rival agents. Consider placing bombs on links between rooms to make life harder for your rivals. Grab the secret file and find the get-away plane. 
